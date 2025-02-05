// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Logging;
using FreeTrainSimulator.Common.Native;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Imported.State;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

using MemoryPack;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D;
using Orts.ActivityRunner.Viewer3D.Primitives;
using Orts.Formats.Msts;
using Orts.Simulation;
using Orts.Simulation.Activities;
using Orts.Simulation.Commanding;
using Orts.Simulation.Multiplayer;
using Orts.Simulation.Multiplayer.Messaging;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class GameStateRunActivity : GameState
    {
        private static readonly char[] separatorChars = new char[] { '/', '\\' };

        private Simulator simulator;

        private static Viewer Viewer { get { return Program.Viewer; } set { Program.Viewer = value; } }
        private LoadingPrimitive loading;
        private LoadingScreenPrimitive loadingScreen;
        private LoadingBarPrimitive loadingBar;
        private TimetableLoadingBarPrimitive timetableLoadingBar;
        private Matrix loadingMatrix = Matrix.Identity;

        private string[] arguments;

        public GameStateRunActivity(string[] args)
        {
            arguments = args;
        }

        protected override void Dispose(bool disposing)
        {
            loading?.Dispose();
            loadingScreen?.Dispose();
            loadingBar?.Dispose();
            timetableLoadingBar?.Dispose();
            base.Dispose(disposing);
        }

        public override async ValueTask Restore(GameSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            await ActivityEvaluation.Instance.Restore(saveState.ActivityEvaluationState).ConfigureAwait(false);
        }

        internal override void Update(RenderFrame frame, GameTime gameTime)
        {
            UpdateLoading();

            if (loading != null)
            {
                frame.AddPrimitive(loading.Material, loading, RenderPrimitiveGroup.Overlay, ref loadingMatrix);
            }

            if (loadingScreen != null)
            {
                frame.AddPrimitive(loadingScreen.Material, loadingScreen, RenderPrimitiveGroup.Overlay, ref loadingMatrix);
            }

            if (loadingBar != null)
            {
                loadingBar.Material.Shader.LoadingPercent = loadedPercent;
                frame.AddPrimitive(loadingBar.Material, loadingBar, RenderPrimitiveGroup.Overlay, ref loadingMatrix);
            }
            if (simulator != null && simulator.TimetableMode && timetableLoadingBar != null && simulator.TimetableLoadedFraction < 0.99f)    // 0.99 to hide loading bar at end of timetable pre-run
            {
                timetableLoadingBar.Material.Shader.LoadingPercent = simulator.TimetableLoadedFraction;
                frame.AddPrimitive(timetableLoadingBar.Material, timetableLoadingBar, RenderPrimitiveGroup.Overlay, ref loadingMatrix);
            }

            base.Update(frame, gameTime);
        }

        internal override async Task Load()
        {
            // Load loading image first!
            loading ??= new LoadingPrimitive(Game);
            loadingBar ??= new LoadingBarPrimitive(Game);
            timetableLoadingBar ??= new TimetableLoadingBarPrimitive(Game);

            //// No action, check for data; for now assume any data is good data.
            //if (actionType == ActionType.None && data.Length != 0)
            //{
            //    // in multiplayer start/resume there is no "-start" or "-resume" string, so you have to discriminate
            //    actionType = activityType != ActivityType.None || options.Length == 0 ? ActionType.Start : ActionType.Resume;
            //}

            try
            {
                InitLogging(); //TODO actionType == ActionType.Test
                profileSelections = await ResolveSelectionsFromCommandLine(arguments);
                arguments = null;
                await InitLoading(profileSelections).ConfigureAwait(false);

                switch (profileSelections.GamePlayAction)
                {
                    case GamePlayAction.SingleplayerNewGame:
                    case GamePlayAction.SinglePlayerTimetableGame:
                    case GamePlayAction.MultiplayerClientGame:
                        await Start(profileSelections).ConfigureAwait(false);
                        break;
                    case GamePlayAction.SingleplayerResume:
                        await Resume(profileSelections).ConfigureAwait(false);
                        break;
                    case GamePlayAction.SingleplayerReplay:
                        await Replay(profileSelections).ConfigureAwait(false);
                        break;
                    case GamePlayAction.SingleplayerReplayFromSave:
                        await ReplayFromSave(profileSelections).ConfigureAwait(false);
                        break;
                    case GamePlayAction.SinglePlayerResumeTimetableGame:
                        break;
                    case GamePlayAction.MultiplayerClientResumeSave:
                        break;
                    case GamePlayAction.TestActivity:
                        await TestActivity(profileSelections).ConfigureAwait(false);
                        break;
                    case GamePlayAction.None:
                    default:
                        MessageBox.Show($"To start {RuntimeInfo.ProductName}, please run 'FreeTrainSimulator.exe'.\n\n"
                                + "If you are attempting to debug this component, please run 'FreeTrainSimulator.exe' and execute the scenario you are interested in. "
                                + "In the log file, the command-line arguments used will be listed at the top. "
                                + "You should then configure your debug environment to execute this component with those command-line arguments.",
                                $"{Application.ProductName}  {VersionInfo.Version}");
                        Game.Exit();
                        break;
                }
            }
            catch (Exception error) when (!Debugger.IsAttached)
            {
                Trace.WriteLine(new FatalException(error));
                if (Game.UserSettings.ErrorDialogEnabled)
                {
                    // If we had a load error but the inner error is one we handle here specially, unwrap it and discard the extra file information.
                    if (error is FileLoadException fileLoadException && (fileLoadException.InnerException is FileNotFoundException || fileLoadException.InnerException is DirectoryNotFoundException))
                        error = fileLoadException.InnerException;

                    if (error is InvalidCommandLineException invalidCommandLineException)
                        MessageBox.Show($"{RuntimeInfo.ProductName} was started with an invalid command-line. {error.Message} Arguments given:\n\n{invalidCommandLineException.ArgumentsList}",
                            $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    else if (error is MissingTrackNodeException)
                        MessageBox.Show($"{RuntimeInfo.ProductName} detected a track section which is not present in tsection.dat and cannot continue.\n\n" +
                            "Most likely you don't have the XTracks or Ytracks version needed for this route.",
                            $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    else if (error is FileNotFoundException fileNotFoundException)
                    {
                        MessageBox.Show($"An essential file is missing and {RuntimeInfo.ProductName} cannot continue.\n\n" +
                                $"    {fileNotFoundException.FileName}",
                                $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else if (error is DirectoryNotFoundException directoryNotFoundException)
                    {
                        // This is a hack to try and extract the actual file name from the exception message. It isn't available anywhere else.
                        Match match = new Regex("'([^']+)'").Match(directoryNotFoundException.Message);
                        string fileName = match.Groups[1].Success ? match.Groups[1].Value : directoryNotFoundException.Message;
                        MessageBox.Show($"An essential folder is missing and {RuntimeInfo.ProductName} cannot continue.\n\n" +
                                $"    {fileName}",
                                $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        string errorSummary = error.GetType().FullName + ": " + error.Message;
                        string logFile = RuntimeInfo.LogFile(Game.UserSettings.LogFilePath, Game.UserSettings.LogFileName);
                        DialogResult openTracker = MessageBox.Show($"A fatal error has occured and {RuntimeInfo.ProductName} cannot continue.\n\n" +
                                $"    {errorSummary}\n\n" +
                                $"This error may be due to bad data or a bug. You can help improve {RuntimeInfo.ProductName} by reporting this error in our bug tracker at https://github.com/perpetualKid/FreeTrainSimulator/issues and attaching the log file {logFile}.\n\n" +
                                ">>> Click OK to report this error on the GitHub bug tracker <<<",
                                $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                        if (openTracker == DialogResult.OK)
                            SystemInfo.OpenBrowser("https://github.com/perpetualKid/FreeTrainSimulator/issues");
                    }
                }
                // Make sure we quit after handling an error.
                Game.Exit();
                Environment.Exit(-1);
            }
            await UninitLoading().ConfigureAwait(false);
        }

        /// <summary>
        /// Run the specified activity from the beginning.
        /// This is the start for MSTS Activity or Explorer mode or Timetable mode
        /// </summary>
        private async ValueTask Start(ProfileSelectionsModel profileSelections)
        {
            await InitSimulator(Game.UserSettings, profileSelections, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);

            switch (profileSelections.ActivityType)
            {
                case ActivityType.TimeTable:
                    simulator.StartTimetable(Game.LoaderProcess.CancellationToken);
                    break;
                default:
                    simulator.Start(Game.LoaderProcess.CancellationToken);
                    break;
            }

            if (MultiPlayerManager.IsMultiPlayer())
            {
                MultiPlayerManager.Instance().Connect();
                MultiPlayerManager.Broadcast(new PlayerStateMessage(simulator.Trains[0]));
                // wait 2 seconds to see if you get a reply from server with updated position/consist data, else go on

                await Task.Delay(2000).ConfigureAwait(false);
                if (simulator.Trains[0].RequestJump)
                {
                    simulator.Trains[0].UpdateRemoteTrainPos(0);
                }
                if (Game.LoaderProcess.CancellationToken.IsCancellationRequested)
                    return;
            }

            Viewer = new Viewer(simulator, Game);
            Viewer.Initialize();

#pragma warning disable CA2000 // Dispose objects before losing scope
            Game.ReplaceState(new GameStateViewer3D(Viewer));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        /// <summary>
        /// Resume a saved game.
        /// </summary>
        private async ValueTask Resume(ProfileSelectionsModel profileSelections)
        {
            // First use the .save file to check the validity and extract the route and activity.
            GameSaveState saveState = await GameSaveState.FromFile<GameSaveState>(profileSelections.GameSaveFile, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);

            await InitSimulator(Game.UserSettings, saveState.ProfileSelections, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);
            simulator.BeforeRestore(saveState.Path, saveState.InitialLocation);

            await simulator.Restore(saveState.SimulatorSaveState).ConfigureAwait(false);

            Viewer = new Viewer(simulator, Game);
            Viewer.Initialize();
            if (MultiPlayerManager.IsMultiPlayer())
            {
                if (saveState.ProfileSelections.ActivityType == ActivityType.Activity)
                    simulator.SetPathAndConsist();
                MultiPlayerManager.Broadcast(new PlayerStateMessage(simulator.Trains[0]));
            }
            await Viewer.Restore(saveState.ViewerSaveState).ConfigureAwait(false);

            // Reload the command log
            simulator.Log.LoadLog(Path.ChangeExtension(profileSelections.GameSaveFile, "replay"));

#pragma warning disable CA2000 // Dispose objects before losing scope
            Game.ReplaceState(new GameStateViewer3D(Viewer));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        /// <summary>
        /// Replay a saved game.
        /// </summary>
        private async ValueTask Replay(ProfileSelectionsModel profileSelections)
        {
            // If "-replay" also specifies a save file then use it
            // E.g. ActivityRunner.exe -replay "yard_two 2012-03-20 22.07.36"
            // else use most recently changed *.save
            // E.g. ActivityRunner.exe -replay

            // First use the .save file to extract the route and activity.
            GameSaveState saveState = await GameSaveState.FromFile<GameSaveState>(profileSelections.GameSaveFile, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);
            await Restore(saveState).ConfigureAwait(false);

            await InitSimulator(Game.UserSettings, saveState.ProfileSelections, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);
            simulator.Start(Game.LoaderProcess.CancellationToken);
            Viewer = new Viewer(simulator, Game);
            Viewer.Initialize();

            // Load command log to replay
            simulator.ReplayCommandList = new List<ICommand>();
            string replayFile = Path.ChangeExtension(profileSelections.GameSaveFile, "replay");
            simulator.Log.LoadLog(replayFile);
            foreach (ICommand command in simulator.Log.CommandList)
            {
                simulator.ReplayCommandList.Add(command);
            }
            simulator.Log.CommandList.Clear();
            CommandLog.ReportReplayCommands(simulator.ReplayCommandList);

#pragma warning disable CA2000 // Dispose objects before losing scope
            Game.ReplaceState(new GameStateViewer3D(Viewer));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        /// <summary>
        /// Replay the last segment of a saved game.
        /// </summary>
        private async ValueTask ReplayFromSave(ProfileSelectionsModel profileSelections)
        {
            // Find previous save file and then move commands to be replayed into replay list.
            CommandLog log = new CommandLog(null);
            string logFile = profileSelections.GameSaveFile.Replace(".save", ".replay", StringComparison.OrdinalIgnoreCase);
            log.LoadLog(logFile);
            List<ICommand> replayCommandList = new List<ICommand>();

            // Scan backwards to find previous saveFile (ignore any that user has deleted).
            int count = log.CommandList.Count;
            string previousSaveFile = string.Empty;
            for (int i = count - 2; // -2 so we skip over the final save command
                    i >= 0; i--)
            {
                if (log.CommandList[i] is SaveCommand saveCommand)
                {
                    string file = Path.Combine(RuntimeInfo.UserDataFolder, saveCommand.FileStem);
                    if (!file.EndsWith(FileNameExtensions.SaveFile, StringComparison.OrdinalIgnoreCase))
                        file += FileNameExtensions.SaveFile;
                    if (File.Exists(file))
                    {
                        previousSaveFile = file;
                        // Move commands after this to the replay command list.
                        for (int j = i + 1; j < count; j++)
                        {
                            replayCommandList.Add(log.CommandList[i + 1]);
                            log.CommandList.RemoveAt(i + 1);
                        }
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(previousSaveFile))
            {
                // No save file found so just replay from start
                replayCommandList.AddRange(log.CommandList);    // copy the commands before deleting them.
                log.CommandList.Clear();

                GameSaveState saveState = await GameSaveState.FromFile<GameSaveState>(profileSelections.GameSaveFile, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);

                await InitSimulator(Game.UserSettings, saveState.ProfileSelections, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);

                simulator.Start(Game.LoaderProcess.CancellationToken);
                Viewer = new Viewer(simulator, Game);
                Viewer.Initialize();
            }
            else
            {
                GameSaveState saveState = await GameSaveState.FromFile<GameSaveState>(previousSaveFile, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);

                // Resume from previous SaveFile and then replay
                await InitSimulator(Game.UserSettings, saveState.ProfileSelections, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);
                simulator.BeforeRestore(saveState.Path, saveState.InitialLocation);
                await simulator.Restore(saveState.SimulatorSaveState).ConfigureAwait(false);

                Viewer = new Viewer(simulator, Game);
                Viewer.Initialize();
                await Viewer.Restore(saveState.ViewerSaveState).ConfigureAwait(false);
            }

            // Now Simulator exists, link the log to it in both directions
            simulator.Log = log;
            log.Simulator = simulator;
            simulator.ReplayCommandList = replayCommandList;
            CommandLog.ReportReplayCommands(simulator.ReplayCommandList);

#pragma warning disable CA2000 // Dispose objects before losing scope
            Game.ReplaceState(new GameStateViewer3D(Viewer));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        /// <summary>
        /// Tests that ActivityRunner.exe can launch a specific activity or explore.
        /// </summary>
        private async ValueTask TestActivity(ProfileSelectionsModel profileSelections)
        {
            DateTime startTime = DateTime.Now;
#pragma warning disable CA2000 // Dispose objects before losing scope
            GameStateViewer3DTest exitGameState = new GameStateViewer3DTest();
#pragma warning restore CA2000 // Dispose objects before losing scope
            try
            {
                await InitSimulator(Game.UserSettings, profileSelections, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);
                simulator.Start(Game.LoaderProcess.CancellationToken);
                Viewer = new Viewer(simulator, Game);
                Viewer.Initialize();
                Game.ReplaceState(exitGameState);
#pragma warning disable CA2000 // Dispose objects before losing scope
                Game.PushState(new GameStateViewer3D(Viewer));
#pragma warning restore CA2000 // Dispose objects before losing scope
                exitGameState.LoadTime = (DateTime.Now - startTime).TotalSeconds - Viewer.RealTime;
                exitGameState.Passed = true;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Game.ReplaceState(exitGameState);
            }
        }

        private void InitLogging(bool appendLog = false)
        {
            if (Game.UserSettings.LogLevel != TraceEventType.Critical)
            {
                string logFileName = RuntimeInfo.LogFile(Game.UserSettings.LogFilePath, Game.UserSettings.LogFileName);
                LoggingUtil.InitLogging(logFileName, Game.UserSettings.LogLevel, !Debugger.IsAttached, appendLog);
                Game.UserSettings.Log();
            }
        }

        #region Loading progress indication calculations

        private const int loadingSampleCount = 100;
        private string loadingDataKey;
        private string loadingDataFilePath;
        private long loadingBytesInitial;
        private DateTime loadingStart;
        private List<long> loadingBytesExpected;
        private List<long> loadingBytesActual;
        private TimeSpan loadingBytesSampleRate;
        private DateTime loadingNextSample = DateTime.MinValue;
        private float loadedPercent = -1f;

        private async ValueTask InitLoading(ProfileSelectionsModel profileSelections)
        {
            // Get the initial bytes; this is subtracted from all further uses of GetProcessBytesLoaded().
            loadingBytesInitial = GetProcessBytesLoaded();

            // We hash together all the appropriate arguments to the program as the key for the loading cache file.
            loadingDataKey = string.Join(" ",
                profileSelections.FolderName,
                profileSelections.RouteId,
                profileSelections.ActivityId,
                profileSelections.TimetableSet,
                profileSelections.TimetableName,
                profileSelections.TimetableTrain,
                profileSelections.GamePlayAction,
                profileSelections.ActivityType).ToUpperInvariant();
            loadingDataFilePath = RuntimeInfo.GetCacheFilePath("Load", loadingDataKey);

            // The loading of the cached data doesn't matter if anything goes wrong; we'll simply have no progress bar.
            LoadingDataState loadingData = null;
            try
            {
                if (File.Exists(loadingDataFilePath))
                    loadingData = await LoadingDataState.FromFile<LoadingDataState>(loadingDataFilePath, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is MemoryPackSerializationException)
            { }

            loadingStart = DateTime.UtcNow;
            loadingBytesExpected = loadingData?.Samples?.ToList() ?? (EnumerableExtension.PresetCollection<long>(100) as List<long>);
            loadingBytesActual = new List<long>(loadingSampleCount);
            // Using the cached loading time, pick a sample rate that will get us ~100 samples. Clamp to 100ms < x < 10,000ms.
            loadingBytesSampleRate = TimeSpan.FromMilliseconds(Math.Clamp((loadingData?.LoadingDuration.TotalMilliseconds) ?? 0 / loadingSampleCount, 100, 10000));
            loadingNextSample = loadingStart + loadingBytesSampleRate;
        }

        private void UpdateLoading()
        {
            if (loadingBytesActual == null)
                return;

            long bytes = GetProcessBytesLoaded() - loadingBytesInitial;

            // Negative indicates no progress data; this happens if the loaded bytes exceeds the cached maximum expected bytes.
            loadedPercent = -(float)(DateTime.UtcNow - loadingStart).TotalSeconds / 15;
            for (int i = 0; i < loadingSampleCount; i++)
            {
                // Find the first expected sample with more bytes. This means we're currently in the (i - 1) to (i) range.
                if (bytes <= loadingBytesExpected[i])
                {
                    // Calculate the position within the (i - 1) to (i) range using straight interpolation.
                    long expectedP = i == 0 ? 0 : loadingBytesExpected[i - 1];
                    long expectedC = loadingBytesExpected[i];
                    float index = i + (float)(bytes - expectedP) / (expectedC - expectedP);
                    loadedPercent = index / loadingSampleCount;
                    break;
                }
            }

            if (DateTime.UtcNow > loadingNextSample)
            {
                // Record a sample every time we should.
                loadingBytesActual.Add(bytes);
                loadingNextSample += loadingBytesSampleRate;
            }
        }

        private async ValueTask UninitLoading()
        {
            if (loadingDataKey == null)
                return;

            TimeSpan loadingTime = DateTime.UtcNow - loadingStart;
            long bytes = GetProcessBytesLoaded() - loadingBytesInitial;
            loadingBytesActual.Add(bytes);

            // Convert from N samples to 100 samples.
            long[] bytesActual = new long[loadingSampleCount];
            for (int i = 0; i < loadingSampleCount; i++)
            {
                float index = (float)(i + 1) / loadingSampleCount * (loadingBytesActual.Count - 1);
                double indexR = index - Math.Floor(index);
                bytesActual[i] = (int)(loadingBytesActual[(int)Math.Floor(index)] * indexR + loadingBytesActual[(int)Math.Ceiling(index)] * (1 - indexR));
            }

            long expected = loadingBytesExpected[loadingSampleCount - 1];
            long difference = bytes - expected;

            Trace.WriteLine($"Loader: Time       = {loadingTime:g} sec");
            Trace.WriteLine($"Loader: Expected   = {expected:N0} bytes");
            Trace.WriteLine($"Loader: Actual     = {bytes:N0} bytes");
            Trace.WriteLine($"Loader: Difference = {difference:N0} bytes ({(float)difference / expected:P1})");
            Trace.WriteLine(string.Empty);

            // Smoothly move all expected values towards actual values, by 10% each run. First run will just copy actual values.
            for (int i = 0; i < loadingSampleCount; i++)
                loadingBytesExpected[i] = loadingBytesExpected[i] > 0 ? loadingBytesExpected[i] * 9 / 10 + bytesActual[i] / 10 : bytesActual[i];

            // Like loading, saving the loading cache data doesn't matter if it fails. We'll just have no data to show progress with.
            try
            {
                LoadingDataState loadingData = new LoadingDataState()
                {
                    DataKey = loadingDataKey,
                    LoadingDuration = loadingTime,
                    Samples = new System.Collections.ObjectModel.Collection<long>(loadingBytesExpected),
                };
                await LoadingDataState.ToFile(loadingDataFilePath, loadingData, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);
                for (int i = 0; i < loadingSampleCount; i++)
                    loadingData.Samples.Add(loadingBytesExpected[i]);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException || exception is DirectoryNotFoundException || exception is NotSupportedException || exception is ObjectDisposedException)
            { }

            //releasing resources only needed during load
            //separatorLine = null;
            loadingBytesExpected = null;
            loadingBytesActual = null;
        }
        #endregion

        private async ValueTask InitSimulator(ProfileUserSettingsModel userSettings, ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            Task<ProfileKeyboardSettingsModel> keyboardSettingsTask = userSettings.Parent.LoadSettingsModel<ProfileKeyboardSettingsModel>(cancellationToken);
            Task<ProfileRailDriverSettingsModel> raildriverSettingsTask = userSettings.Parent.LoadSettingsModel<ProfileRailDriverSettingsModel>(cancellationToken);

            FolderModel folderModel = await profileSelections.SelectedFolder(Game.LoaderProcess.CancellationToken).ConfigureAwait(false);
            RouteModel routeModel = await folderModel.RouteModel(profileSelections.RouteId, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);

            Trace.WriteLine($"{"Mode",-12}= -{profileSelections.GamePlayAction} -{profileSelections.ActivityType}");

            switch (profileSelections.ActivityType)
            {
                case ActivityType.Explorer:
                    {
                        PathModel pathModel = await routeModel.PathModel(profileSelections.PathId, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);
                        WagonSetModel wagonSetModel = await folderModel.WagonSetModel(profileSelections.WagonSetId, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);

                        Trace.WriteLine($"{"Route",-12}= {routeModel.Name}");
                        Trace.WriteLine($"{"Path",-12}= {pathModel.Name}");
                        Trace.WriteLine($"{"Consist",-12}= {wagonSetModel.Name}");
                        Trace.WriteLine($"{"Time",-12}= {profileSelections.StartTime}");
                        Trace.WriteLine($"{"Season",-12}= {profileSelections.Season}");
                        Trace.WriteLine($"{"Weather",-12}= {profileSelections.Weather}");

                        simulator = new Simulator(userSettings, routeModel);
                        simulator.SetExplore(pathModel.SourceFile(), wagonSetModel.SourceFile(), profileSelections.StartTime.ToTimeSpan(), profileSelections.Season, profileSelections.Weather);
                        break;
                    }
                case ActivityType.ExploreActivity:
                    {
                        PathModel pathModel = await routeModel.PathModel(profileSelections.PathId, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);
                        WagonSetModel wagonSetModel = await folderModel.WagonSetModel(profileSelections.WagonSetId, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);

                        Trace.WriteLine($"{"Route",-12}= {routeModel.Name}");
                        Trace.WriteLine($"{"Path",-12}= {pathModel.Name}");
                        Trace.WriteLine($"{"Consist",-12}= {wagonSetModel.Name}");
                        Trace.WriteLine($"{"Time",-12}= {profileSelections.StartTime}");
                        Trace.WriteLine($"{"Season",-12}= {profileSelections.Season}");
                        Trace.WriteLine($"{"Weather",-12}= {profileSelections.Weather}");

                        simulator = new Simulator(userSettings, routeModel);
                        simulator.SetExploreThroughActivity(pathModel.SourceFile(), wagonSetModel.SourceFile(), profileSelections.StartTime.ToTimeSpan(), profileSelections.Season, profileSelections.Weather);
                        break;
                    }
                case ActivityType.TimeTable:
                    {
                        TimetableModel timetableModel = await routeModel.TimetableModel(profileSelections.TimetableSet, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);

                        Trace.WriteLine($"{"Timetable",-12}= {profileSelections.TimetableSet}:{profileSelections.TimetableName}");
                        Trace.WriteLine($"{"Train",-12}= {profileSelections.TimetableName}:{profileSelections.TimetableTrain}");
                        Trace.WriteLine($"{"Day",-12}= {profileSelections.TimetableDay}");
                        Trace.WriteLine($"{"Season",-12}= {profileSelections.Season}");
                        Trace.WriteLine($"{"Weather",-12}= {profileSelections.Weather}");
                        if (!string.IsNullOrEmpty(profileSelections.WeatherChanges))
                            Trace.WriteLine($"{"Weath Change",-12}= {profileSelections.WeatherChanges}");

                        simulator = new Simulator(userSettings, routeModel);
                        simulator.SetTimetableOptions(timetableModel.SourceFile(), $"{profileSelections.TimetableName}:{profileSelections.TimetableTrain}", profileSelections.Season, profileSelections.Weather, profileSelections.WeatherChanges);
                        break;
                    }
                default:
                    {
                        ActivityModel activityModel = await routeModel.ActivityModel(profileSelections.ActivityId, Game.LoaderProcess.CancellationToken).ConfigureAwait(false);

                        Trace.WriteLine($"{"Route",-12}= {routeModel.Name}");
                        Trace.WriteLine($"{"Activity",-12}= {activityModel.Name}");

                        simulator = new Simulator(userSettings, routeModel);
                        simulator.SetActivity(activityModel.SourceFile());
                        break;
                    }
            }

            if (userSettings.MultiPlayer)
            {
                MultiPlayerManager.Start(userSettings.MultiplayerHost, userSettings.MultiplayerPort, userSettings.MultiplayerUser, "1234");
            }

            Game.UserSettings.KeyboardSettings = await keyboardSettingsTask.ConfigureAwait(false);
            Game.UserSettings.RailDriverSettings = await raildriverSettingsTask.ConfigureAwait(false);
        }

        private async Task<ProfileSelectionsModel> ResolveSelectionsFromCommandLine(string[] args)
        {
            if (args.Length == 0) // just load the selections from current profile
            {
                return await Game.UserSettings.Parent.LoadSettingsModel<ProfileSelectionsModel>(Game.LoaderProcess.CancellationToken).ConfigureAwait(false);
            }

            IEnumerable<IGrouping<bool, string>> groupedArguments = args.GroupBy(argumenType => argumenType.StartsWith('-') || argumenType.StartsWith('/'));
            List<string> optionsList = groupedArguments.Where(grouping => grouping.Key).SelectMany(grouping => grouping).Select(option => option[1..]).ToList();
            string[] parameters = groupedArguments.Where(grouping => !grouping.Key).SelectMany(grouping => grouping).ToArray();

            GamePlayAction actionType = GamePlayAction.None;
            ActivityType activityType = ActivityType.None;

            _ = optionsList.Where(argument => EnumExtension.GetValue(argument, out actionType)).FirstOrDefault();
            optionsList.RemoveAll(option => string.Equals(option, actionType.ToString(), StringComparison.OrdinalIgnoreCase));
            _ = optionsList.Where(argument => EnumExtension.GetValue(argument, out activityType)).FirstOrDefault();
            optionsList.RemoveAll(option => string.Equals(option, activityType.ToString(), StringComparison.OrdinalIgnoreCase));

            ProfileSelectionsModel NewGameFromParams(string[] parameters, GamePlayAction targetActionType, ActivityType targetActivityType)
            {
                if (targetActionType is not (GamePlayAction.SingleplayerNewGame or GamePlayAction.SinglePlayerTimetableGame or GamePlayAction.MultiplayerClientGame))
                    throw new InvalidCommandLineException($"Unexpected GamePlayAction {targetActionType} to start a new game.", parameters);

                ProfileSelectionsModel selectionsModel = new ProfileSelectionsModel()
                {
                    GamePlayAction = targetActionType,
                    ActivityType = targetActivityType,
                };

                switch (activityType)
                {
                    case ActivityType.Explorer:
                    case ActivityType.ExploreActivity:
                        selectionsModel.FolderName = parameters[0];
                        selectionsModel.RouteId = parameters[1];
                        selectionsModel.PathId = parameters[2];
                        selectionsModel.WagonSetId = parameters[3];

                        selectionsModel.StartTime = TimeOnly.TryParse(parameters[4], out TimeOnly startTime) ? startTime : TimeOnly.FromTimeSpan(TimeSpan.FromHours(12));
                        selectionsModel.Season = EnumExtension.GetValue(parameters[5], out SeasonType season) ? season : SeasonType.Summer;
                        selectionsModel.Weather = EnumExtension.GetValue(parameters[6], out WeatherType weather) ? weather : WeatherType.Clear;
                        break;
                    case ActivityType.TimeTable:
                        selectionsModel.FolderName = parameters[0];
                        selectionsModel.RouteId = parameters[1];
                        selectionsModel.TimetableSet = parameters[2];
                        selectionsModel.TimetableName = parameters[3];
                        selectionsModel.TimetableTrain = parameters[4];

                        selectionsModel.TimetableDay = EnumExtension.GetValue(parameters[5], out DayOfWeek weekday) ? weekday : DayOfWeek.Monday;
                        selectionsModel.Season = EnumExtension.GetValue(parameters[6], out season) ? season : SeasonType.Summer;
                        selectionsModel.Weather = EnumExtension.GetValue(parameters[7], out weather) ? weather : WeatherType.Clear;
                        if (parameters.Length > 8)
                            selectionsModel.WeatherChanges = parameters[8];
                        break;
                    case ActivityType.Activity:
                        selectionsModel.FolderName = parameters[0];
                        selectionsModel.RouteId = parameters[1];
                        selectionsModel.ActivityId = parameters[2];
                        break;
                }
                return selectionsModel;
            }

            ProfileSelectionsModel profileSelections = null;
            string[] selectionElements;
            switch (actionType)
            {
                case GamePlayAction.SingleplayerNewGame:
                case GamePlayAction.SinglePlayerTimetableGame:
                case GamePlayAction.None:
                    switch (activityType)
                    {
                        case ActivityType.Explorer:
                        case ActivityType.ExploreActivity:
                            if ((selectionElements = parameters).Length == 7 ||
                                parameters.Length == 4 && (selectionElements = parameters[0].Split(separatorChars, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))?.Length == 4 &&
                                (selectionElements = new List<string>(selectionElements).Concat(parameters[1..]).ToArray()).Length == 7)
                            {
                                profileSelections = NewGameFromParams(selectionElements, GamePlayAction.SingleplayerNewGame, activityType);
                            }
                            else
                                throw new InvalidCommandLineException($"Mode '{activityType}' needs 4 argument: \"Folder\\Route\\Path\\WagonSet\" StartTime (HH:MM) Season (Spring, Summer, Autumn, Winter) Weather (Clear, Rain, Snow). " +
                                    $"Alternatively 7 arguments with \"Folder\" \"Route\" \"Path\" \"WagonSet\" provided individually.", parameters);
                            break;
                        case ActivityType.TimeTable:

                            if ((selectionElements = parameters).Length >= 8 ||
                                parameters.Length >= 4 && (selectionElements = parameters[0].Split(separatorChars, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))?.Length == 5 &&
                                (selectionElements = new List<string>(selectionElements).Concat(parameters[1..]).ToArray()).Length >= 8)
                            {
                                profileSelections = NewGameFromParams(selectionElements, GamePlayAction.SinglePlayerTimetableGame, ActivityType.TimeTable);
                            }
                            else
                                throw new InvalidCommandLineException($"Mode '{activityType}' needs 4 (optional 5) argument: \"Folder\\Route\\Timetable\\TimetableName\\TrainName\" Day (Monday - Sunday) Season (Spring, Summer, Autumn, Winter) Weather (Clear, Rain, Snow) [optional] WeatherChanges." +
                                    $"Alternatively 8 (optional 9) arguments with \"Folder\" \"Route\" \"Timetable\" \"TimetableName\" \"TrainName\" provided individually.", parameters);
                            break;
                        case ActivityType.Activity:
                        case ActivityType.None:
                            //expect 3 parameters, to be the name of the folder, route and the activity, or one path-like parameter separated by path-separator chat folder\route\activity
                            if ((selectionElements = parameters).Length == 3 || parameters.Length == 1 && (selectionElements = parameters[0].Split(separatorChars, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))?.Length == 3)
                            {
                                profileSelections = NewGameFromParams(selectionElements, GamePlayAction.SingleplayerNewGame, ActivityType.Activity);
                            }
                            else
                                throw new InvalidCommandLineException($"Mode '{activityType}' needs 1 argument: \"Folder\\Route\\Activity\" or 3 arguments \"Folder\" \"Route\" \"Activity\".", parameters);
                            break;
                    }
                    break;
                case GamePlayAction.MultiplayerClientGame:
                    break;
                case GamePlayAction.SingleplayerResume:
                case GamePlayAction.SingleplayerReplay:
                case GamePlayAction.SingleplayerReplayFromSave:
                case GamePlayAction.SinglePlayerResumeTimetableGame:
                case GamePlayAction.MultiplayerClientResumeSave:
                    //optional 1 parameters about save file name
                    profileSelections = new ProfileSelectionsModel()
                    {
                        GamePlayAction = actionType,
                        GameSaveFile = GetSaveFile(parameters?.Length == 1 ? parameters[0] : string.Empty)
                    };
                    break;
                default:
                    throw new InvalidCommandLineException("Invalid combination of command line arguments.", parameters);

            }

            return profileSelections;
        }

        private static string GetSaveFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                // return the latest save file
                DirectoryInfo directory = new DirectoryInfo(RuntimeInfo.UserDataFolder);
                FileInfo file = directory.EnumerateFiles("*" + FileNameExtensions.SaveFile).OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                return file == null
                    ? throw new FileNotFoundException($"No activity save file '*.save' not found in folder {directory}")
                    : file.FullName;
            }
            string saveFile = fileName;
            if (!saveFile.EndsWith(FileNameExtensions.SaveFile, StringComparison.OrdinalIgnoreCase))
            {
                saveFile += FileNameExtensions.SaveFile;
            }
            return Path.Combine(RuntimeInfo.UserDataFolder, saveFile);
        }

        private static long GetProcessBytesLoaded()
        {
            return NativeMethods.GetProcessIoCounters(Process.GetCurrentProcess().Handle, out NativeStructs.IO_COUNTERS counters)
                ? (long)counters.ReadTransferCount
                : 0;
        }
    }
}
