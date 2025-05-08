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
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Settings;

[assembly: CLSCompliant(false)]

namespace FreeTrainSimulator.Menu
{
    internal static class Program
    {
        [STAThread]  // requred for use of the DirectoryBrowserDialog in the main form.
        private static void Main()
        {
//            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();

            if (Debugger.IsAttached)
            {
                MainForm();
            }
            else
            {
                try
                {
                    MainForm();
                }
                catch (Exception error)
                {
                    MessageBox.Show(error.ToString(), $"{RuntimeInfo.ProductName} {VersionInfo.Version}");
                    throw;
                }
            }
        }

        private static void MainForm()
        {
            using (MainForm MainForm = new MainForm())
            {
                while (MainForm.ShowDialog() == DialogResult.OK)
                {
                    if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
                    {
                        string joinedParameters = ResolveParameters(MainForm.ProfileSelections);
                        Clipboard.SetText(joinedParameters);
                        MessageBox.Show(
                            "Activity arguments have been copied to the clipboard:" + Environment.NewLine + Environment.NewLine +
                            $"{joinedParameters}" + Environment.NewLine + Environment.NewLine +
                            "This is a debugging aid. If you wanted to start the simulator instead, select Start without holding down the Alt key.", "Command Line Arguments");
                    }
                    else
                    {
                        ProcessStartInfo processStartInfo = new ProcessStartInfo
                        {
                            FileName = RuntimeInfo.ActivityRunnerExecutable,
                            WindowStyle = ProcessWindowStyle.Normal,
                            WorkingDirectory = Application.StartupPath
                        };
                        Process process = Process.Start(processStartInfo);
                        process.WaitForExit();
                    }
                }
            }
        }

        private static string ResolveParameters(ProfileSelectionsModel profileSelections)
        {
            List<string> parameters = new List<string>();

            parameters.Add($"-{profileSelections.GamePlayAction}");
            parameters.Add($"-{profileSelections.ActivityType}");

            switch (profileSelections.GamePlayAction)
            {
                case GamePlayAction.SingleplayerNewGame:
                case GamePlayAction.MultiplayerClientGame:
                    if (profileSelections.ActivityType is ActivityType.Explorer or ActivityType.ExploreActivity)
                    {
                        parameters.Add($"\"{profileSelections.FolderName}\\{profileSelections.RouteId}\\{profileSelections.PathId}\\{profileSelections.WagonSetId}\"");
                        parameters.Add($"{profileSelections.StartTime}");
                        parameters.Add($"{profileSelections.Season}");
                        parameters.Add($"{profileSelections.Weather}");
                    }
                    else
                    {
                        parameters.Add($"\"{profileSelections.FolderName}\\{profileSelections.RouteId}\\{profileSelections.ActivityId}\"");
                    }
                    break;
                case GamePlayAction.SingleplayerResume:
                case GamePlayAction.SingleplayerReplay:
                case GamePlayAction.SingleplayerReplayFromSave:
                case GamePlayAction.MultiplayerClientResumeSave:
                    parameters.Add($"\"{profileSelections.GameSaveFile}\"");
                    break;
                case GamePlayAction.SinglePlayerTimetableGame:
                    parameters.Add($"\"{profileSelections.FolderName}\\{profileSelections.RouteId}\\{profileSelections.TimetableSet}\\{profileSelections.TimetableName}\\{profileSelections.TimetableTrain}\"");
                    parameters.Add($"{profileSelections.TimetableDay}");
                    parameters.Add($"{profileSelections.Season}");
                    parameters.Add($"{profileSelections.Weather}");
                    if (!string.IsNullOrEmpty(profileSelections.WeatherChanges))
                    {
                        parameters.Add($"{profileSelections.WeatherChanges}");
                    }
                    break;
                case GamePlayAction.SinglePlayerResumeTimetableGame:
                    parameters.Add($"\"{profileSelections.GameSaveFile}\"");
                    break;
            }

            return string.Join(" ", parameters);
        }
    }
}
