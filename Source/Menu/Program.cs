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
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Imported.Shim;

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
                    string joinedParameters = ResolveParameters(MainForm.ProfileSelections, MainForm);

                    Debug.WriteLine(joinedParameters);

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
                            Arguments = joinedParameters,
                            WindowStyle = ProcessWindowStyle.Normal,
                            WorkingDirectory = Application.StartupPath
                        };
                        Process process = Process.Start(processStartInfo);
                        process.WaitForExit();
                    }
                }
            }
        }

        private static string ResolveParameters(ProfileSelectionsModel profileSelections, MainForm MainForm)
        {
            List<string> parameters = new List<string>();

            switch (profileSelections.GamePlayAction)
            {
                case GamePlayAction.SingleplayerNewGame:
                    parameters.Add("-start");
                    break;
                case GamePlayAction.SingleplayerResumeSave:
                    parameters.Add("-resume");
                    break;
                case GamePlayAction.SingleplayerReplaySave:
                    parameters.Add("-replay");
                    break;
                case GamePlayAction.SingleplayerReplaySaveFromSave:
                    parameters.Add("-replayfromsave");
                    break;
                case GamePlayAction.MultiplayerClient:
                    parameters.Add("-multiplayerclient");
                    break;
                case GamePlayAction.SinglePlayerTimetableGame:
                    parameters.Add("-start");
                    break;
                case GamePlayAction.SinglePlayerResumeTimetableGame:
                    parameters.Add("-resume");
                    break;
                case GamePlayAction.MultiplayerClientResumeSave:
                    parameters.Add("-multiplayerclient");
                    break;
            }

            switch (profileSelections.GamePlayAction)
            {
                case GamePlayAction.SingleplayerNewGame:
                case GamePlayAction.MultiplayerClient:
                    if (profileSelections.ActivityType == ActivityType.Explorer)
                    {
                        parameters.Add("-explorer");
                        parameters.Add($"\"{profileSelections.SelectedPath().SourceFile()}\"");
                        parameters.Add($"\"{profileSelections.SelectedWagonSet().SourceFile()}\"");
                        parameters.Add($"{profileSelections.StartTime}");
                        parameters.Add($"{profileSelections.Season}");
                        parameters.Add($"{profileSelections.Weather}");
                    }
                    else if (profileSelections.ActivityType == ActivityType.ExploreActivity)
                    {
                        parameters.Add("-exploreactivity");
                        parameters.Add($"\"{profileSelections.SelectedPath().SourceFile()}\"");
                        parameters.Add($"\"{profileSelections.SelectedWagonSet().SourceFile()}\"");
                        parameters.Add($"{profileSelections.StartTime}");
                        parameters.Add($"{profileSelections.Season}");
                        parameters.Add($"{profileSelections.Weather}");
                    }
                    else
                    {
                        parameters.Add("-activity");
                        parameters.Add($"\"{profileSelections.SelectedActivity().SourceFile()}\"");
                    }
                    break;
                case GamePlayAction.SingleplayerResumeSave:
                case GamePlayAction.SingleplayerReplaySave:
                case GamePlayAction.SingleplayerReplaySaveFromSave:
                case GamePlayAction.MultiplayerClientResumeSave:
                    parameters.Add($"\"{MainForm.SelectedSaveFile}\"");
                    break;
                case GamePlayAction.SinglePlayerTimetableGame:

                    parameters.Add("-timetable");
                    parameters.Add($"\"{profileSelections.SelectedTimetable().SourceFile()}\"");
                    parameters.Add($"\"{profileSelections.TimetableName}:{profileSelections.TimetableTrain}\"");
                    parameters.Add($"{profileSelections.TimetableDay}");
                    parameters.Add($"{profileSelections.Season}");
                    parameters.Add($"{profileSelections.Weather}");
                    if (!string.IsNullOrEmpty(profileSelections.WeatherChanges))
                    {
                        parameters.Add($"\"{profileSelections.SelectedWeatherChangesModel().SourceFile()}\"");
                    }
                    break;
                case GamePlayAction.SinglePlayerResumeTimetableGame:
                    parameters.Add($"\"{MainForm.SelectedSaveFile}\"");
                    break;
            }

            return string.Join(" ", parameters);
        }
    }
}
