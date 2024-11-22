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
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;
using FreeTrainSimulator.Models.Simplified;

[assembly: CLSCompliant(false)]

namespace Orts.Menu
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
                    List<string> parameters = new List<string>();
                    switch (MainForm.SelectedAction)
                    {
                        case MainForm.UserAction.SingleplayerNewGame:
                            parameters.Add("-start");
                            break;
                        case MainForm.UserAction.SingleplayerResumeSave:
                            parameters.Add("-resume");
                            break;
                        case MainForm.UserAction.SingleplayerReplaySave:
                            parameters.Add("-replay");
                            break;
                        case MainForm.UserAction.SingleplayerReplaySaveFromSave:
                            parameters.Add("-replayfromsave");
                            break;
                        case MainForm.UserAction.MultiplayerClient:
                            parameters.Add("-multiplayerclient");
                            break;
                        case MainForm.UserAction.SinglePlayerTimetableGame:
                            parameters.Add("-start");
                            break;
                        case MainForm.UserAction.SinglePlayerResumeTimetableGame:
                            parameters.Add("-resume");
                            break;
                        case MainForm.UserAction.MultiplayerClientResumeSave:
                            parameters.Add("-multiplayerclient");
                            break;
                    }
                    switch (MainForm.SelectedAction)
                    {
                        case MainForm.UserAction.SingleplayerNewGame:
                        case MainForm.UserAction.MultiplayerClient:
                            if (MainForm.SelectedActivity.ActivityType == ActivityType.Explorer)
                            {
                                parameters.Add("-explorer");
                                parameters.Add($"\"{MainForm.SelectedActivity.MstsSourceFile()}\"");
                                parameters.Add($"\"{MainForm.SelectedConsist.MstsSourceFile()}\"");
                                parameters.Add($"{MainForm.SelectedActivity.StartTime}");
                                parameters.Add($"{MainForm.SelectedActivity.Season}");
                                parameters.Add($"{MainForm.SelectedActivity.Weather}");
                            }
                            else if (MainForm.SelectedActivity.ActivityType == ActivityType.ExploreActivity)
                            {
                                parameters.Add("-exploreactivity");
                                parameters.Add($"\"{MainForm.SelectedActivity.MstsSourceFile()}\"");
                                parameters.Add($"\"{MainForm.SelectedConsist.MstsSourceFile()}\"");
                                parameters.Add($"{MainForm.SelectedActivity.StartTime}");
                                parameters.Add($"{MainForm.SelectedActivity.Season}");
                                parameters.Add($"{MainForm.SelectedActivity.Weather}");
                            }
                            else
                            {
                                parameters.Add("-activity");
                                parameters.Add($"\"{MainForm.SelectedActivity.MstsSourceFile()}\"");
                            }
                            break;
                        case MainForm.UserAction.SingleplayerResumeSave:
                        case MainForm.UserAction.SingleplayerReplaySave:
                        case MainForm.UserAction.SingleplayerReplaySaveFromSave:
                        case MainForm.UserAction.MultiplayerClientResumeSave:
                            parameters.Add($"\"{MainForm.SelectedSaveFile}\"");
                            break;
                        case MainForm.UserAction.SinglePlayerTimetableGame:
                            parameters.Add("-timetable");
                            parameters.Add($"\"{MainForm.SelectedTimetable.Name}\"");
                            parameters.Add($"\"{MainForm.SelectedTimetable.Name}:{MainForm.SelectedTimetableTrain.Name}\"");
                            parameters.Add($"{MainForm.CurrentSelections.TimetableDay}");
                            parameters.Add($"{MainForm.CurrentSelections.Season}");
                            parameters.Add($"{ MainForm.CurrentSelections.Weather}");
                            if (!string.IsNullOrEmpty(MainForm.CurrentSelections.WeatherChanges))
                            {
                                parameters.Add($"\"{MainForm.CurrentSelections.WeatherChanges}\"");
                            }
                            break;
                        case MainForm.UserAction.SinglePlayerResumeTimetableGame:
                            parameters.Add($"\"{MainForm.SelectedSaveFile}\"");
                            break;
                    }

                    string joinedParameters = string.Join(" ", parameters);
                    if ((Control.ModifierKeys & Keys.Alt) == Keys.Alt)
                    {
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
                            Arguments = string.Join(" ", parameters),
                            WindowStyle = ProcessWindowStyle.Normal,
                            WorkingDirectory = Application.StartupPath
                        };
                        Process process = Process.Start(processStartInfo);
                        process.WaitForExit();
                    }
                }
            }
        }
    }
}
