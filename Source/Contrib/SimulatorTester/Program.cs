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
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using Orts.Common.Info;
using Orts.Common.Position;
using Orts.Simulation;
using Orts.Simulation.Commanding;
using System.Threading;
using Orts.Common.Calc;
using Orts.Common;

namespace Orts.SimulatorTester
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            IEnumerable<string> options = args.Where(a => a.StartsWith("-") || a.StartsWith("/")).Select(a => a[1..]);
            List<string> files = args.Where(a => !a.StartsWith("-") && !a.StartsWith("/")).ToList();
            UserSettings settings = new UserSettings(options);

            Trace.Listeners.Add(new ConsoleTraceListener());

            if (files.Count != 1 || options.Contains("help", StringComparer.InvariantCultureIgnoreCase))
            {
                var version = FileVersionInfo.GetVersionInfo(Application.ExecutablePath);
                Console.WriteLine("{0} {1}", version.FileDescription, VersionInfo.FullVersion);
                Console.WriteLine();
                Console.WriteLine("Usage:");
                Console.WriteLine("  {0} [options] <SAVE_FILE>", Path.GetFileNameWithoutExtension(Application.ExecutablePath));
                Console.WriteLine();
                Console.WriteLine("Arguments:");
                Console.WriteLine("  <SAVE_FILE>  {0} save file to use", Application.ProductName);
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  /quiet       Do not show summary of simulation (only exit code is set)");
                Console.WriteLine("  /verbose     Show version and settings (similar to a {0} log)", Application.ProductName);
                Console.WriteLine("  /fps <FPS>   Set the simulation frame-rate [default: 10]");
                Console.WriteLine("  /help        Show help and usage information");
                Console.WriteLine("  ...and any standard {0} option", Application.ProductName);
                Console.WriteLine();
                Console.WriteLine("The {0} takes a save file and:", version.FileDescription);
                Console.WriteLine("  - Loads the same activity as contained in the save file");
                Console.WriteLine("  - Runs the simulation at the specified FPS for the same duration as the save file");
                Console.WriteLine("  - Compares the final position with that contained in the save file");
                Console.WriteLine();
                Console.WriteLine("The exit code is set to the distance from the target in meters");
                Console.WriteLine();
                return;
            }

            if (settings.Verbose)
            {
                Console.WriteLine("This is a log file for {0}. Please include this file in bug reports.", Application.ProductName);
                LogSeparator();

                SystemInfo.WriteSystemDetails();
                LogSeparator();

                Console.WriteLine("Version      = {0}", VersionInfo.Version.Length > 0 ? VersionInfo.Version : "<none>");
                Console.WriteLine("Build        = {0}", VersionInfo.Build);
                Console.WriteLine("Executable   = {0}", Path.GetFileName(Application.ExecutablePath));
                foreach (var arg in args)
                    Console.WriteLine("Argument     = {0}", arg);
                LogSeparator();

                settings.Log();
                LogSeparator();
            }

            string saveFile = files[0];
            using (BinaryReader inf = new BinaryReader(new FileStream(saveFile, FileMode.Open, FileAccess.Read)))
            {
                SaveData data = GetSaveData(inf);
                string activityFile = data.Args[0];

                if (!settings.Quiet)
                {
                    foreach (string arg in data.Args)
                        Console.WriteLine("Argument     = {0}", arg);
                    Console.WriteLine("Initial Pos  = {0}, {1}", data.InitialTileX, data.InitialTileZ);
                    Console.WriteLine("Expected Pos = {0}, {1}", data.ExpectedTileX, data.ExpectedTileZ);
                    Console.Write("Loading...   ");
                }

                StaticRandom.MakeDeterministic();
                DateTimeOffset startTime = DateTimeOffset.Now;
                Simulator simulator = new Simulator(settings, activityFile);
                simulator.SetActivity(activityFile);
                simulator.Start(CancellationToken.None);
                simulator.SetCommandReceivers();
                simulator.Log.LoadLog(Path.ChangeExtension(saveFile, "replay"));
                simulator.ReplayCommandList = new List<ICommand>();
                simulator.ReplayCommandList.AddRange(simulator.Log.CommandList);
                simulator.Log.CommandList.Clear();

                DateTimeOffset loadTime = DateTimeOffset.Now;
                if (!settings.Quiet)
                {
                    Console.WriteLine("{0:N1} seconds", (loadTime - startTime).TotalSeconds);
                    Console.Write("Replaying... ");
                }

                float step = 1f / settings.FPS;
                for (float tick = 0f; tick < data.TimeElapsed; tick += step)
                {
                    simulator.Update(step);
                    simulator.Log.Update(simulator.ReplayCommandList);
                }

                DateTimeOffset endTime = DateTimeOffset.Now;
                double actualTileX = simulator.Trains[0].FrontTDBTraveller.TileX + (simulator.Trains[0].FrontTDBTraveller.X / WorldPosition.TileSize);
                double actualTileZ = simulator.Trains[0].FrontTDBTraveller.TileZ + (simulator.Trains[0].FrontTDBTraveller.Z / WorldPosition.TileSize);
                double initialToExpectedM = Math.Sqrt(Math.Pow(data.ExpectedTileX - data.InitialTileX, 2) + Math.Pow(data.ExpectedTileZ - data.InitialTileZ, 2)) * WorldPosition.TileSize;
                double expectedToActualM = Math.Sqrt(Math.Pow(actualTileX - data.ExpectedTileX, 2) + Math.Pow(actualTileZ - data.ExpectedTileZ, 2)) * WorldPosition.TileSize;

                if (!settings.Quiet)
                {
                    Console.WriteLine("{0:N1} seconds ({1:F0}x speed-up)", (endTime - loadTime).TotalSeconds, data.TimeElapsed / (endTime - loadTime).TotalSeconds);
                    Console.WriteLine("Actual Pos   = {0}, {1}", actualTileX, actualTileZ);
                    Console.WriteLine("Distance     = {0:N3} m ({1:P1})", expectedToActualM, 1 - expectedToActualM / initialToExpectedM);
                }

                Environment.ExitCode = (int)expectedToActualM;
            }
        }

        private static void LogSeparator()
        {
            Console.WriteLine(new string('-', 80));
        }

        private static SaveData GetSaveData(BinaryReader inf)
        {
            SaveData values = new SaveData();

            _ = inf.ReadString(); // e.g. 1.3.2-alpha.4

            string routeName = inf.ReadString();
            if (routeName == "$Multipl$")
            {
                _ = inf.ReadString();  // Route name
            }

            _ = inf.ReadString();  // Path name
            values.TimeElapsed = inf.ReadInt32();  // Time elapsed in game (secs)
            _ = inf.ReadInt64();  // Date and time in real world

            values.ExpectedTileX = inf.ReadSingle();  // Current location of player train TileX
            values.ExpectedTileZ = inf.ReadSingle();  // Current location of player train TileZ

            values.InitialTileX = inf.ReadSingle();  // Initial location of player train TileX
            values.InitialTileZ = inf.ReadSingle();  // Initial location of player train TileZ

            values.Args = new string[inf.ReadInt32()];
            for (int i = 0; i < values.Args.Length; i++)
                values.Args[i] = inf.ReadString();

            inf.ReadString();  // Activity type

            return values;
        }

        private struct SaveData
        {
            public int TimeElapsed;
            public float InitialTileX;
            public float InitialTileZ;
            public float ExpectedTileX;
            public float ExpectedTileZ;
            public string[] Args;
        }
    }
}
