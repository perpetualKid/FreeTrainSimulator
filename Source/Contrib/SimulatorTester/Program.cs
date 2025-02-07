// COPYRIGHT 2022 by the Open Rails project.
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
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Logging;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.State;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

using Orts.Simulation;
using Orts.Simulation.Commanding;

[assembly: CLSCompliant(false)]
namespace Orts.SimulatorTester
{
    internal sealed class Program
    {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
        private static async Task Main(string[] args)
        {
            ImmutableArray<string> options = args.Where(a => a.StartsWith('-') || a.StartsWith('/')).Select(a => a[1..]).ToImmutableArray();
            List<string> files = args.Where(a => !a.StartsWith('-') && !a.StartsWith('/')).ToList();

            Trace.Listeners.Add(new ConsoleTraceListener());

            if (files.Count != 1 || options.Contains("help", StringComparer.InvariantCultureIgnoreCase))
            {
                Console.WriteLine("{0} {1}", RuntimeInfo.ApplicationName, VersionInfo.FullVersion);
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("  {0} [options] <SAVE_FILE>", Path.GetFileNameWithoutExtension(RuntimeInfo.ApplicationFile));
                Console.WriteLine();
                Console.WriteLine("Arguments:");
                Console.WriteLine("  <SAVE_FILE>  {0} save file to use", RuntimeInfo.ProductName);
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  /quiet       Do not show summary of simulation (only exit code is set)");
                Console.WriteLine("  /verbose     Show version and settings (similar to a {0} log)", RuntimeInfo.ProductName);
                Console.WriteLine("  /fps <FPS>   Set the simulation frame-rate [default: 10]");
                Console.WriteLine("  /help        Show help and usage information");
                Console.WriteLine("  ...and any standard {0} option", RuntimeInfo.ProductName);
                Console.WriteLine();
                Console.WriteLine("The {0} takes a save file and:", RuntimeInfo.ApplicationName);
                Console.WriteLine("  - Loads the same activity as contained in the save file");
                Console.WriteLine("  - Runs the simulation at the specified FPS for the same duration as the save file");
                Console.WriteLine("  - Compares the final position with that contained in the save file");
                Console.WriteLine();
                Console.WriteLine("The exit code is set to the distance from the target in meters");
                Console.WriteLine();
                return;
            }

            ProfileModel profileModel = await ((ProfileModel)null).Current(CancellationToken.None).ConfigureAwait(false);
            ProfileUserSettingsModel userSettings = await profileModel.LoadSettingsModel<ProfileUserSettingsModel>(CancellationToken.None).ConfigureAwait(false);

            if (userSettings.LogLevel > TraceEventType.Warning)
            {
                Console.WriteLine("This is a log file for {0}. Please include this file in bug reports.", RuntimeInfo.ProductName);
                Console.WriteLine(LoggingUtil.SeparatorLine);

                SystemInfo.WriteSystemDetails();
                Console.WriteLine(LoggingUtil.SeparatorLine);

                Console.WriteLine($"{"Date/Time",-12}= {DateTime.Now} ({DateTime.UtcNow:u})");
                Console.WriteLine($"{"Version",-12}= {VersionInfo.Version}");
                Console.WriteLine($"{"Code Version",-12}= {VersionInfo.CodeVersion}");
                Console.WriteLine($"{"OS",-12}= {RuntimeInformation.OSDescription} {RuntimeInformation.RuntimeIdentifier}");
                Console.WriteLine($"{"Runtime",-12}= {RuntimeInformation.FrameworkDescription} ({(Environment.Is64BitProcess ? "64" : "32")}bit)");
                Trace.WriteLine($"{"Logging",-12}= {userSettings.LogLevel}");
                foreach (string arg in Environment.GetCommandLineArgs())
                    Trace.WriteLine($"{"Argument",-12}= {arg.Replace(Environment.UserName, "********", StringComparison.OrdinalIgnoreCase)}");
                Console.WriteLine(LoggingUtil.SeparatorLine);

                userSettings.Log();
                Console.WriteLine(LoggingUtil.SeparatorLine);
            }

            GameSaveState saveState = await GameSaveState.FromFile<GameSaveState>(files[0], CancellationToken.None).ConfigureAwait(false);
            saveState.ProfileSelections.Log();
            
            Console.WriteLine("Initial Pos  = {0}, {1}", saveState.InitialLocation.TileX, saveState.InitialLocation.TileZ);
            Console.WriteLine("Expected Pos = {0}, {1}", saveState.PlayerLocation.TileX, saveState.PlayerLocation.TileZ);
            Console.Write("Loading...   ");

            StaticRandom.MakeDeterministic();

            FolderModel folderModel = await saveState.ProfileSelections.SelectedFolder(CancellationToken.None).ConfigureAwait(false);
            RouteModel routeModel = await folderModel.RouteModel(saveState.ProfileSelections.RouteId, CancellationToken.None).ConfigureAwait(false);
            ActivityModel activityModel = await routeModel.ActivityModel(saveState.ProfileSelections.ActivityId, CancellationToken.None).ConfigureAwait(false);

            DateTimeOffset startTime = DateTimeOffset.Now;
            Simulator simulator = new Simulator(userSettings, routeModel);
            simulator.SetActivity(activityModel);
            simulator.Start(CancellationToken.None);
            simulator.SetCommandReceivers();
            simulator.Log.LoadLog(Path.ChangeExtension(files[0], "replay"));
            simulator.ReplayCommandList = new List<ICommand>();
            simulator.ReplayCommandList.AddRange(simulator.Log.CommandList);
            simulator.Log.CommandList.Clear();

            DateTimeOffset loadTime = DateTimeOffset.Now;
            if (userSettings.LogLevel > TraceEventType.Warning)
            {
                Console.WriteLine("{0:N1} seconds", (loadTime - startTime).TotalSeconds);
                Console.Write("Replaying... ");
            }

            userSettings.ProfilingFps = 10;
            float step = 1f / userSettings.ProfilingFps;
            for (float tick = 0f; tick < saveState.GameTime; tick += step)
            {
                simulator.Update(step);
                simulator.Log.Update(simulator.ReplayCommandList);
            }

            DateTimeOffset endTime = DateTimeOffset.Now;
            WorldLocation actualLocation = simulator.Trains[0].FrontTDBTraveller.WorldLocation;

            double initialToExpectedM = WorldLocation.GetDistance(saveState.PlayerLocation, saveState.InitialLocation).Length();
            double expectedToActualM = WorldLocation.GetDistance(saveState.PlayerLocation, actualLocation).Length();

            if (userSettings.LogLevel > TraceEventType.Warning)
            {
                Console.WriteLine("{0:N1} seconds ({1:F0}x speed-up)", (endTime - loadTime).TotalSeconds, saveState.GameTime / (endTime - loadTime).TotalSeconds);
                Console.WriteLine("Actual Pos   = {0}", actualLocation);
                Console.WriteLine("Distance     = {0:N3} m ({1:P1})", expectedToActualM, 1 - expectedToActualM / initialToExpectedM);
            }

            Environment.ExitCode = (int)expectedToActualM;
        }
#pragma warning restore CA1303 // Do not pass literals as localized parameters
    }
}
