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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D;
using Orts.ActivityRunner.Viewer3D.Primitives;
using Orts.Common;
using Orts.Common.Info;
using Orts.Common.Logging;
using Orts.Common.Native;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Settings;
using Orts.Simulation;
using Orts.Simulation.Activities;
using Orts.Simulation.Commanding;
using Orts.Simulation.MultiPlayer;
using Orts.Simulation.World;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class GameStateRunActivity : GameState
    {

        private enum ActivityType
        {
            None,
            Activity,
            Explorer,
            ExploreActivity,
            TimeTable,
        }

        private enum ActionType
        {
            None,
            Start,
            Resume,
            Replay,
            ReplayFromSave,
            Test,
        }

        private static ActionType actionType;
        private static ActivityType activityType;

        private Simulator simulator;

        private static Viewer Viewer { get { return Program.Viewer; } set { Program.Viewer = value; } }
        private static string logFileName;
        private LoadingPrimitive loading;
        private LoadingScreenPrimitive loadingScreen;
        private LoadingBarPrimitive loadingBar;
        private TimetableLoadingBarPrimitive timetableLoadingBar;
        private Matrix loadingMatrix = Matrix.Identity;

        private static readonly string separatorLine = new string('-', 80);
        private static string[] arguments;
        private static string[] options;
        private static string[] data;

        public GameStateRunActivity(string[] args)
        {
            arguments = args;

            IEnumerable<IGrouping<bool, string>> groupedArguments = args.GroupBy(argumenType => argumenType.StartsWith('-') || argumenType.StartsWith('/'));
            List<string> optionsList = groupedArguments.Where(grouping => grouping.Key).SelectMany(grouping => grouping).Select(option => option[1..]).ToList();
            data = groupedArguments.Where(grouping => !grouping.Key).SelectMany(grouping => grouping).ToArray();

            _ = optionsList.Where(argument => EnumExtension.GetValue(argument, out activityType)).FirstOrDefault();
            optionsList.RemoveAll(option => string.Equals(option, activityType.ToString(), StringComparison.OrdinalIgnoreCase));
            _ = optionsList.Where(argument => EnumExtension.GetValue(argument, out actionType)).FirstOrDefault();
            optionsList.RemoveAll(option => string.Equals(option, actionType.ToString(), StringComparison.OrdinalIgnoreCase));

            options = optionsList.ToArray();
        }

        protected override void Dispose(bool disposing)
        {
            loading?.Dispose();
            loadingScreen?.Dispose();
            loadingBar?.Dispose();
            timetableLoadingBar?.Dispose();
            base.Dispose(disposing);
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

        internal override void Load()
        {
            // Load loading image first!
            loading ??= new LoadingPrimitive(Game);
            loadingBar ??= new LoadingBarPrimitive(Game);
            timetableLoadingBar ??= new TimetableLoadingBarPrimitive(Game);

            // No action, check for data; for now assume any data is good data.
            if (actionType == ActionType.None && data.Length != 0)
            {
                // in multiplayer start/resume there is no "-start" or "-resume" string, so you have to discriminate
                actionType = activityType != ActivityType.None || options.Length == 0 ? ActionType.Start : ActionType.Resume;
            }


            UserSettings settings = Game.Settings;

            Action doAction = () =>
            {
                // Do the action specified or write out some help.
                switch (actionType)
                {
                    case ActionType.Start:
                        InitLogging();
                        InitLoading();
                        Start(settings);
                        break;
                    case ActionType.Resume:
                        InitLogging();
                        InitLoading();
                        Resume(settings);
                        break;
                    case ActionType.Replay:
                        InitLogging();
                        InitLoading();
                        Replay(settings);
                        break;
                    case ActionType.ReplayFromSave:
                        InitLogging();
                        InitLoading();
                        ReplayFromSave(settings);
                        break;
                    case ActionType.Test:
                        InitLogging(true);
                        InitLoading();
                        Test(settings);
                        break;

                    default:
                        MessageBox.Show($"To start {RuntimeInfo.ProductName}, please run 'OpenRails.exe'.\n\n"
                                + "If you are attempting to debug this component, please run 'OpenRails.exe' and execute the scenario you are interested in. "
                                + "In the log file, the command-line arguments used will be listed at the top. "
                                + "You should then configure your debug environment to execute this component with those command-line arguments.",
                                $"{Application.ProductName}  {VersionInfo.Version}");
                        Game.Exit();
                        break;
                }
            };
            try
            {
                doAction();
            }
            catch (Exception error) when (!Debugger.IsAttached)
            {
                Trace.WriteLine(new FatalException(error));
                if (settings.ShowErrorDialogs)
                {
                    // If we had a load error but the inner error is one we handle here specially, unwrap it and discard the extra file information.
                    if (error is FileLoadException fileLoadException && (fileLoadException.InnerException is FileNotFoundException || fileLoadException.InnerException is DirectoryNotFoundException))
                        error = fileLoadException.InnerException;

                    if (error is IncompatibleSaveException incompatibleSaveException)
                    {
                        MessageBox.Show($"Save file is incompatible with this version of {RuntimeInfo.ProductName}.\n\n" +
                            $"    {incompatibleSaveException.SaveFile}\n\n" +
                            $"Saved version: {incompatibleSaveException.Version}\n" +
                            $"Current version: {VersionInfo.Version}",
                            $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else if (error is InvalidCommandLineException)
                        MessageBox.Show($"{RuntimeInfo.ProductName} was started with an invalid command-line. {error.Message} Arguments given:\n\n{string.Join("\n", data.Select(d => "\u2022 " + d).ToArray())}",
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
                        string logFile = Path.Combine(settings.LoggingPath, settings.LoggingFilename);
                        DialogResult openTracker = MessageBox.Show($"A fatal error has occured and {RuntimeInfo.ProductName} cannot continue.\n\n" +
                                $"    {errorSummary}\n\n" +
                                $"This error may be due to bad data or a bug. You can help improve {RuntimeInfo.ProductName} by reporting this error in our bug tracker at https://github.com/perpetualKid/ORTS-MG/issues and attaching the log file {logFile}.\n\n" +
                                ">>> Click OK to report this error on the GitHub bug tracker <<<",
                                $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                        if (openTracker == DialogResult.OK)
                            Process.Start(new ProcessStartInfo("https://github.com/perpetualKid/ORTS-MG/issues") { UseShellExecute = true });
                    }
                }
                // Make sure we quit after handling an error.
                Game.Exit();
                Environment.Exit(-1);
            }
            UninitLoading();
        }

        /// <summary>
        /// Run the specified activity from the beginning.
        /// This is the start for MSTS Activity or Explorer mode or Timetable mode
        /// </summary>
        private void Start(UserSettings settings)
        {
            InitSimulator(settings);

            switch (activityType)
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
                MultiPlayerManager.Notify(new MSGPlayer(MultiPlayerManager.Instance().UserName, MultiPlayerManager.Instance().Code, simulator.ConsistFileName, simulator.PathFileName, simulator.Trains[0], 0, simulator.Settings.AvatarURL).ToString());
                // wait 5 seconds to see if you get a reply from server with updated position/consist data, else go on

                System.Threading.Thread.Sleep(5000);
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
        /// Save the current game state for later resume.
        /// </summary>
        public static void Save()
        {
            Simulator simulator = Simulator.Instance;
            if (MultiPlayerManager.IsMultiPlayer() && !MultiPlayerManager.IsServer())
                return; //no save for multiplayer sessions yet

            if (ContainerManager.ActiveOperationsCounter > 0)
            // don't save if performing a container load/unload
            {
                Simulator.Instance.Confirmer.Message(ConfirmLevel.Warning, Viewer.Catalog.GetString("Game save is not allowed during container load/unload"));
                return;
            }

            // Prefix with the activity filename so that, when resuming from the Menu.exe, we can quickly find those Saves 
            // that are likely to match the previously chosen route and activity.
            // Append the current date and time, so that each file is unique.
            // This is the "sortable" date format, ISO 8601, but with "." in place of the ":" which are not valid in filenames.

            string fileStem = simulator.SaveFileName;

            using (BinaryWriter outf = simulator.Settings.LogSaveData ?
                new LoggedBinaryWriter(new FileStream(Path.Combine(RuntimeInfo.UserDataFolder, fileStem + ".save"), FileMode.Create, FileAccess.Write)) :
                new BinaryWriter(new FileStream(Path.Combine(RuntimeInfo.UserDataFolder, fileStem + ".save"), FileMode.Create, FileAccess.Write)))
            {
                // Save some version identifiers so we can validate on load.
                outf.Write(VersionInfo.Version);

                // Save heading data used in Menu.exe
                if (MultiPlayerManager.IsMultiPlayer() && MultiPlayerManager.IsServer())
                    outf.Write("$Multipl$");
                outf.Write(simulator.RouteName);
                outf.Write(simulator.PathName);

                outf.Write((int)simulator.GameTime);
                outf.Write(DateTime.Now.ToBinary());
                outf.Write(simulator.Trains[0].FrontTDBTraveller.TileX + simulator.Trains[0].FrontTDBTraveller.X / 2048);
                outf.Write(simulator.Trains[0].FrontTDBTraveller.TileZ + simulator.Trains[0].FrontTDBTraveller.Z / 2048);
                outf.Write(simulator.InitialTileX);
                outf.Write(simulator.InitialTileZ);

                // Now save the data used by ActivityRunner.exe
                outf.Write(data.Length);
                foreach (string argument in data)
                    outf.Write(argument);
                outf.Write((int)activityType);

                simulator.Save(outf);
                Viewer.Save(outf, fileStem);
                // Save multiplayer parameters
                if (MultiPlayerManager.IsMultiPlayer() && MultiPlayerManager.IsServer())
                    MultiPlayerManager.OnlineTrains.Save(outf);

                // Write out position within file so we can check when restoring.
                outf.Write(outf.BaseStream.Position);
            }

            // The Save command is the only command that doesn't take any action. It just serves as a marker.
            _ = new SaveCommand(simulator.Log, fileStem);
            simulator.Log.SaveLog(Path.Combine(RuntimeInfo.UserDataFolder, fileStem + ".replay"));

            // Copy the logfile to the save folder
            string logName = Path.Combine(RuntimeInfo.UserDataFolder, fileStem + ".txt");

            if (File.Exists(logFileName))
            {
                Trace.Flush();
                foreach (TraceListener listener in Trace.Listeners)
                    listener.Flush();
                File.Delete(logName);
                File.Copy(logFileName, logName);
            }

            //Debrief Eval
            foreach (string file in Directory.EnumerateFiles(RuntimeInfo.UserDataFolder, simulator.ActivityFileName + "*.eval"))
                File.Delete(file);//Delete all debrief eval files previously saved, for the same activity.//fileDbfEval

            using (BinaryWriter outf = new BinaryWriter(new FileStream(RuntimeInfo.UserDataFolder + $"\\{fileStem}.eval", FileMode.Create, FileAccess.Write)))
            {
                ActivityEvaluation.Save(outf);
            }
        }

        /// <summary>
        /// Resume a saved game.
        /// </summary>
        private void Resume(UserSettings settings)
        {
            // If "-resume" also specifies a save file then use it
            // E.g. ActivityRunner.exe -resume "yard_two 2012-03-20 22.07.36"
            // else use most recently changed *.save
            // E.g. ActivityRunner.exe -resume

            // First use the .save file to check the validity and extract the route and activity.
            string saveFile = GetSaveFile(data);
            string versionOrBuild = string.Empty;
            using (BinaryReader inf = settings.LogSaveData ? new LoggedBinaryReader(new FileStream(saveFile, FileMode.Open, FileAccess.Read)) : new BinaryReader(new FileStream(saveFile, FileMode.Open, FileAccess.Read)))
            {
                try // Because Restore() methods may try to read beyond the end of an out of date file.
                {
                    versionOrBuild = GetValidSaveVersionOrBuild(saveFile, inf);

                    (string PathName, float InitialTileX, float InitialTileZ, string[] Args, ActivityType ActivityType) = GetSavedValues(inf);
                    activityType = ActivityType;
                    data = Args;
                    InitSimulator(settings);
                    simulator.Restore(inf, PathName, InitialTileX, InitialTileZ, Game.LoaderProcess.CancellationToken);
                    Viewer = new Viewer(simulator, Game);
                    Viewer.Initialize();
                    if (MultiPlayerManager.IsMultiPlayer())
                    {
                        if (ActivityType == ActivityType.Activity)
                            simulator.SetPathAndConsist();
                        MultiPlayerManager.BroadCast(new MSGPlayer(MultiPlayerManager.Instance().UserName, MultiPlayerManager.Instance().Code, simulator.ConsistFileName, simulator.PathFileName, simulator.Trains[0], 0, simulator.Settings.AvatarURL).ToString());
                    }
                    Viewer.Restore(inf);

                    if (MultiPlayerManager.IsMultiPlayer() && MultiPlayerManager.IsServer())
                        MultiPlayerManager.OnlineTrains.Restore(inf);

                    long restorePosition = inf.BaseStream.Position;
                    long savePosition = inf.ReadInt64();
                    if (restorePosition != savePosition)
                        throw new InvalidDataException("Saved game stream position is incorrect.");

                    //Restore Debrief eval data                    
                    string evaluationFile = Path.ChangeExtension(saveFile, ".eval");
                    if (File.Exists(evaluationFile))
                    {
                        using (BinaryReader evalInput = new BinaryReader(new FileStream(evaluationFile, FileMode.Open, FileAccess.Read)))
                        {
                            try
                            {
                                ActivityEvaluation.Restore(evalInput);
                            }
                            catch (IOException)
                            {
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    inf.Close();
                    if (versionOrBuild == VersionInfo.Version)
                    {
                        // If the save version is the same as the program version, we can't be an incompatible save - it's just a bug.
                        throw;
                    }
                    else
                    {
                        // Rethrow the existing error if it is already an IncompatibleSaveException.
                        if (error is IncompatibleSaveException)
                            throw;
                        throw new IncompatibleSaveException(saveFile, versionOrBuild, error);
                    }
                }

                // Reload the command log
                simulator.Log.LoadLog(Path.ChangeExtension(saveFile, "replay"));

#pragma warning disable CA2000 // Dispose objects before losing scope
                Game.ReplaceState(new GameStateViewer3D(Viewer));
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
        }

        /// <summary>
        /// Replay a saved game.
        /// </summary>
        private void Replay(UserSettings settings)
        {
            // If "-replay" also specifies a save file then use it
            // E.g. ActivityRunner.exe -replay "yard_two 2012-03-20 22.07.36"
            // else use most recently changed *.save
            // E.g. ActivityRunner.exe -replay

            // First use the .save file to extract the route and activity.
            string saveFile = GetSaveFile(data);
            string versionOrBuild = string.Empty;
            using (BinaryReader inf = settings.LogSaveData ? new LoggedBinaryReader(new FileStream(saveFile, FileMode.Open, FileAccess.Read)) : new BinaryReader(new FileStream(saveFile, FileMode.Open, FileAccess.Read)))
            {
                versionOrBuild = GetValidSaveVersionOrBuild(saveFile, inf);
                (string PathName, float InitialTileX, float InitialTileZ, string[] Args, ActivityType ActivityType) = GetSavedValues(inf);
                activityType = ActivityType;
                data = Args;
                InitSimulator(settings);
                simulator.Start(Game.LoaderProcess.CancellationToken);
                Viewer = new Viewer(simulator, Game);
                Viewer.Initialize();
            }

            // Load command log to replay
            simulator.ReplayCommandList = new List<ICommand>();
            string replayFile = Path.ChangeExtension(saveFile, "replay");
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
        private void ReplayFromSave(UserSettings settings)
        {
            // E.g. RunActivity.exe -replay_from_save "yard_two 2012-03-20 22.07.36"
            string saveFile = GetSaveFile(data);

            // Find previous save file and then move commands to be replayed into replay list.
            CommandLog log = new CommandLog(null);
            string logFile = saveFile.Replace(".save", ".replay", StringComparison.OrdinalIgnoreCase);
            log.LoadLog(logFile);
            List<ICommand> replayCommandList = new List<ICommand>();

            // Scan backwards to find previous saveFile (ignore any that user has deleted).
            int count = log.CommandList.Count;
            string previousSaveFile = string.Empty;
            for (int i = count - 2; // -2 so we skip over the final save command
                    i >= 0; i--)
            {
                SaveCommand saveCommand = log.CommandList[i] as SaveCommand;
                if (saveCommand != null)
                {
                    string file = Path.Combine(RuntimeInfo.UserDataFolder, saveCommand.FileStem);
                    if (!file.EndsWith(".save", StringComparison.OrdinalIgnoreCase))
                        file += ".save";
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
                // But we have no args, so have to get these from the Save
                using (BinaryReader inf = new BinaryReader(new FileStream(saveFile, FileMode.Open, FileAccess.Read)))
                {
                    inf.ReadString();    // Revision
                    (string _, float _, float _, string[] Args, ActivityType _) = GetSavedValues(inf);
                    actionType = ActionType.Replay;
                    data = Args;
                    InitSimulator(settings);
                }
                simulator.Start(Game.LoaderProcess.CancellationToken);
                Viewer = new Viewer(simulator, Game);
                Viewer.Initialize();
            }
            else
            {
                // Resume from previous SaveFile and then replay
                using (BinaryReader inf = new BinaryReader(new FileStream(previousSaveFile, FileMode.Open, FileAccess.Read)))
                {
                    GetValidSaveVersionOrBuild(saveFile, inf);

                    (string PathName, float InitialTileX, float InitialTileZ, string[] Args, ActivityType ActivityType) = GetSavedValues(inf);
                    data = Args;
                    actionType = ActionType.Resume;
                    InitSimulator(settings);
                    simulator.Restore(inf, PathName, InitialTileX, InitialTileZ, Game.LoaderProcess.CancellationToken);
                    Viewer = new Viewer(simulator, Game);
                    Viewer.Initialize();
                    Viewer.Restore(inf);
                }
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

        private static string GetValidSaveVersionOrBuild(string saveFile, BinaryReader inf)
        {
            string version = inf.ReadString(); // e.g. 1.3.2-alpha.4
            bool? valid = VersionInfo.GetValidity(version);
            if (valid == false) // This is usually detected in ResumeForm.cs but a Resume can also be launched from the command line.
                throw new IncompatibleSaveException(saveFile, version);
            if (!valid.HasValue)
            {
                //Cannot make this multi-language using Viewer.Catalog as Viewer is still null.
                Trace.TraceWarning($"Restoring from a save made by version {version} "
                    + $"of {RuntimeInfo.ProductName} may be incompatible with current version {VersionInfo.Version}.");
            }
            return version;
        }

        /// <summary>
        /// Tests that ActivityRunner.exe can launch a specific activity or explore.
        /// </summary>
        private void Test(UserSettings settings)
        {
            DateTime startTime = DateTime.Now;
#pragma warning disable CA2000 // Dispose objects before losing scope
            GameStateViewer3DTest exitGameState = new GameStateViewer3DTest();
#pragma warning restore CA2000 // Dispose objects before losing scope
            try
            {
                actionType = ActionType.Test;
                InitSimulator(settings);
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
            if (Game.Settings.Logging)
            {
                logFileName = RuntimeInfo.LogFile(Game.Settings.LoggingPath, Game.Settings.LoggingFilename);
                LoggingUtil.InitLogging(logFileName, Game.Settings.LogErrorsOnly, appendLog);
                Game.Settings.Log();
                Trace.WriteLine(LoggingUtil.SeparatorLine);
            }
        }

        #region Loading progress indication calculations

        private const int loadingSampleCount = 100;
        private string loadingDataKey;
        private string loadingDataFilePath;
        private long loadingBytesInitial;
        private DateTime loadingStart;
        private long[] loadingBytesExpected;
        private List<long> loadingBytesActual;
        private TimeSpan loadingBytesSampleRate;
        private DateTime loadingNextSample = DateTime.MinValue;
        private float loadedPercent = -1f;

        private void InitLoading()
        {
            // Get the initial bytes; this is subtracted from all further uses of GetProcessBytesLoaded().
            loadingBytesInitial = GetProcessBytesLoaded();

            // We hash together all the appropriate arguments to the program as the key for the loading cache file.
            // Arguments without a '.' in them and those starting '/' are ignored, since they are explore activity
            // configuration (time, season, etc.) or flags like /test which we don't want to change on.
            loadingDataKey = string.Join(" ", data.Where(a => a.Contains('.', StringComparison.OrdinalIgnoreCase)).ToArray()).ToUpperInvariant();
            loadingDataFilePath = RuntimeInfo.GetCacheFilePath("Load", loadingDataKey);

            int loadingTime = 0;
            long[] bytesExpected = new long[loadingSampleCount];
            List<long> bytesActual = new List<long>(loadingSampleCount);
            // The loading of the cached data doesn't matter if anything goes wrong; we'll simply have no progress bar.
            try
            {
                if (File.Exists(loadingDataFilePath))
                {
                    using (FileStream data = File.OpenRead(loadingDataFilePath))
                    {
                        using (BinaryReader reader = new BinaryReader(data))
                        {
                            reader.ReadString();
                            loadingTime = reader.ReadInt32();
                            for (int i = 0; i < loadingSampleCount; i++)
                                bytesExpected[i] = reader.ReadInt64();
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            { }

            loadingStart = DateTime.UtcNow;
            loadingBytesExpected = bytesExpected;
            loadingBytesActual = bytesActual;
            // Using the cached loading time, pick a sample rate that will get us ~100 samples. Clamp to 100ms < x < 10,000ms.
            loadingBytesSampleRate = new TimeSpan(0, 0, 0, 0, MathHelper.Clamp(loadingTime / loadingSampleCount, 100, 10000));
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

        private void UninitLoading()
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
                using (FileStream data = File.OpenWrite(loadingDataFilePath))
                {
                    data.SetLength(0);
                    using (BinaryWriter writer = new BinaryWriter(data))
                    {
                        writer.Write(loadingDataKey);
                        writer.Write((int)loadingTime.TotalMilliseconds);
                        for (int i = 0; i < loadingSampleCount; i++)
                            writer.Write(loadingBytesExpected[i]);
                    }
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException || exception is DirectoryNotFoundException || exception is NotSupportedException || exception is ObjectDisposedException)
            { }

            //releasing resources only needed during load
            //separatorLine = null;
            loadingBytesExpected = null;
            loadingBytesActual = null;
        }
        #endregion

        private void InitSimulator(UserSettings settings)
        {
            if (activityType == ActivityType.None)
            {
                // implicit processing without explicit action definition
                if (data.Length == 1)
                    activityType = ActivityType.Activity;
                else if (data.Length == 5)
                    activityType = ActivityType.Explorer;
            }

            Trace.WriteLine($"{"Mode",-12}= {actionType} {activityType}");
            TimeSpan startTime = TimeSpan.Zero;
            SeasonType season = SeasonType.Summer;
            WeatherType weather = WeatherType.Clear;

            switch (activityType)
            {
                case ActivityType.Activity:
                    if (data.Length == 0)
                        throw new InvalidCommandLineException("Mode 'activity' needs 1 argument: activity file.");
                    Trace.WriteLine($"{"Route",-12}= {GetRouteName(data[0])}");
                    Trace.WriteLine($"{"Activity",-12}= {GetActivityName(data[0])} ({data[0]})");
                    break;

                case ActivityType.Explorer:
                case ActivityType.ExploreActivity:
                    if (data.Length < 5)
                        throw new InvalidCommandLineException("Mode 'explorer' needs 5 arguments: path file, consist file, time (hh[:mm[:ss]]), season (Spring, Summer, Autumn, Winter), weather (Clear, Rain, Snow).");
                    Trace.WriteLine($"{"Route",-12}= {GetRouteName(data[0])}");
                    Trace.WriteLine($"{"Path",-12}= {GetPathName(data[0])} ({data[0]})");
                    Trace.WriteLine($"{"Consist",-12}= {GetConsistName(data[1])} ({data[1]})");
                    Trace.WriteLine($"{"Time",-12}= {(TimeSpan.TryParse(data[2], out startTime) ? startTime.ToString() : "Unknown")} ({data[2]})");
                    Trace.WriteLine($"{"Season",-12}= {(EnumExtension.GetValue(data[3], out season) ? season.ToString() : "Unknown")} ({data[3]})");
                    Trace.WriteLine($"{"Weather",-12}= {(EnumExtension.GetValue(data[4], out weather) ? weather.ToString() : "Unknown")} ({data[4]})");
                    break;

                case ActivityType.TimeTable:
                    if (data.Length < 5)
                        throw new InvalidCommandLineException("Mode 'timetable' needs 5 arguments: timetable file, train name, day (Monday - Sunday), season (Spring, Summer, Autumn, Winter), weather (Clear, Rain, Snow), [optional] WeatherFile.");
                    Trace.WriteLine($"{"File",-12}= {data[0]}");
                    Trace.WriteLine($"{"Train",-12}= {data[1]}");
                    Trace.WriteLine($"{"Day",-12}= {data[2]}");
                    Trace.WriteLine($"{"Season",-12}= {(EnumExtension.GetValue(data[3], out season) ? season.ToString() : "Unknown")} ({data[3]})");
                    Trace.WriteLine($"{"Weather",-12}= {(EnumExtension.GetValue(data[4], out weather) ? weather.ToString() : "Unknown")} ({data[4]})");
                    break;

                default:
                    throw new InvalidCommandLineException($"Unexpected mode with {arguments.Length} argument(s)");
            }

            Trace.WriteLine(separatorLine);
            if (settings.MultiplayerClient)
            {
                Trace.WriteLine("Multiplayer Client");

                Trace.WriteLine($"{"User",-12}= {settings.Multiplayer_User}");
                Trace.WriteLine($"{"Host",-12}= {settings.Multiplayer_Host}");
                Trace.WriteLine($"{"Port",-12}= {settings.Multiplayer_Port}");
                Trace.WriteLine(separatorLine);
            }

            switch (activityType)
            {
                case ActivityType.Activity:
                    simulator = new Simulator(settings, data[0]);
                    if (loadingScreen == null)
                        loadingScreen = new LoadingScreenPrimitive(Game);
                    simulator.SetActivity(data[0]);
                    break;

                case ActivityType.Explorer:
                    simulator = new Simulator(settings, data[0]);
                    if (loadingScreen == null)
                        loadingScreen = new LoadingScreenPrimitive(Game);
                    simulator.SetExplore(data[0], data[1], startTime, season, weather);
                    break;

                case ActivityType.ExploreActivity:
                    simulator = new Simulator(settings, data[0]);
                    if (loadingScreen == null)
                        loadingScreen = new LoadingScreenPrimitive(Game);
                    simulator.SetExploreThroughActivity(data[0], data[1], startTime, season, weather);
                    break;

                case ActivityType.TimeTable:
                    simulator = new Simulator(settings, data[0]);
                    if (loadingScreen == null)
                        loadingScreen = new LoadingScreenPrimitive(Game);
                    if (actionType != ActionType.Start) // no specific action for start, handled in start_timetable
                    {
                        // for resume and replay : set timetable file and selected train info
                        simulator.TimetableFileName = Path.GetFileNameWithoutExtension(data[0]);
                        simulator.PathName = data[1];
                    }
                    simulator.SetTimetableOptions(data[0], data[1], season, weather, data.Length > 5 ? data[5] : string.Empty);

                    break;
            }

            if (settings.MultiplayerClient)
            {
                MultiPlayerManager.Start(settings.Multiplayer_UpdateInterval, settings.Multiplayer_Host, settings.Multiplayer_Port, settings.Multiplayer_User, "1234");
            }
        }

        private static string GetRouteName(string path)
        {
            try
            {
                return new RouteFile(FolderStructure.RouteFromActivity(path).TrackFileName).Route.Name;
            }
            catch (Formats.Msts.Parsers.STFException) { }
            return null;
        }

        private static string GetActivityName(string path)
        {
            try
            {
                if (Path.GetExtension(path).Equals(".act", StringComparison.OrdinalIgnoreCase))
                {
                    return new ActivityFile(path).Activity.Header.Name;
                }
            }
            catch (Formats.Msts.Parsers.STFException) { }
            return null;
        }

        private static string GetPathName(string path)
        {
            try
            {
                if (Path.GetExtension(path).Equals(".pat", StringComparison.OrdinalIgnoreCase))
                {
                    return new PathFile(path).Name;
                }
            }
            catch (Formats.Msts.Parsers.STFException) { }
            return null;
        }

        private static string GetConsistName(string path)
        {
            try
            {
                if (Path.GetExtension(path).Equals(".con", StringComparison.OrdinalIgnoreCase))
                {
                    return new ConsistFile(path).Name;
                }
            }
            catch (Formats.Msts.Parsers.STFException) { }
            return null;
        }

        private static string GetSaveFile(string[] args)
        {
            if (args.Length == 0)
            {
                DirectoryInfo directory = new DirectoryInfo(RuntimeInfo.UserDataFolder);
                FileInfo file = directory.EnumerateFiles("*.save")
                    .OrderByDescending(f => f.LastWriteTime)
                    .FirstOrDefault();
                return file == null
                    ? throw new FileNotFoundException($"Activity Save file '*.save' not found in folder {directory}")
                    : file.FullName;
            }
            string saveFile = args[0];
            if (!saveFile.EndsWith(".save", StringComparison.OrdinalIgnoreCase))
            {
                saveFile += ".save";
            }
            return Path.Combine(RuntimeInfo.UserDataFolder, saveFile);
        }

        private static (string PathName, float InitialTileX, float InitialTileZ, string[] Args, ActivityType ActivityType) GetSavedValues(BinaryReader inf)
        {
            (string PathName, float InitialTileX, float InitialTileZ, string[] Args, ActivityType ActivityType) result;

            // Skip the heading data used in Menu.exe
            // Done so even if not elegant to be compatible with existing save files
            if (inf.ReadString() == "$Multipl$")
                inf.ReadString();    // Route name
            result.PathName = inf.ReadString();    // Path name
                                                   //skip
            inf.ReadInt32();     // Time elapsed in game (secs)
            inf.ReadInt64();     // Date and time in real world
            inf.ReadSingle();    // Current location of player train TileX
            inf.ReadSingle();    // Current location of player train TileZ

            // Read initial position and pass to Simulator so it can be written out if another save is made.
            result.InitialTileX = inf.ReadSingle();  // Initial location of player train TileX
            result.InitialTileZ = inf.ReadSingle();  // Initial location of player train TileZ

            // Read in the real data...
            string[] savedArgs = new string[inf.ReadInt32()];
            for (int i = 0; i < savedArgs.Length; i++)
                savedArgs[i] = inf.ReadString();
            result.Args = savedArgs;
            result.ActivityType = (ActivityType)inf.ReadInt32();

            return result;
        }

        private static long GetProcessBytesLoaded()
        {
            return NativeMethods.GetProcessIoCounters(Process.GetCurrentProcess().Handle, out NativeStructs.IO_COUNTERS counters)
                ? (long)counters.ReadTransferCount
                : 0;
        }
    }
}
