// COPYRIGHT 2012, 2013 by the Open Rails project.
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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GNU.Gettext;
using GNU.Gettext.WinForms;
using ORTS.Menu;
using ORTS.Settings;
using Path = System.IO.Path;

namespace ORTS
{
    public partial class TestingForm : Form
    {
        public class TestActivity
        {
            public string DefaultSort { get; set; }
            public string Route { get; set; }
            public string Activity { get; set; }
            public string ActivityFilePath { get; set; }
            public bool ToTest { get; set; }
            public bool Tested { get; set; }
            public bool Passed { get; set; }
            public string Errors { get; set; }
            public string Load { get; set; }
            public string FPS { get; set; }

            public TestActivity(Folder folder, Route route, Activity activity)
            {
                DefaultSort = folder.Name + "/" + route.Name + "/" + activity.Name;
                Route = route.Name;
                Activity = activity.Name;
                ActivityFilePath = activity.FilePath;
            }
        }

        private CancellationTokenSource ctsTestActivityLoader;
        private CancellationTokenSource ctsTestActivityRunner;
        private bool clearedLogs;
        private readonly string runActivity;
        private readonly UserSettings settings;
        private readonly string summaryFilePath = Path.Combine(UserSettings.UserDataFolder, "TestingSummary.csv");
        private readonly string logFilePath = Path.Combine(UserSettings.UserDataFolder, "TestingLog.txt");

        public TestingForm(UserSettings settings, string runActivity)
        {
            InitializeComponent();  // Needed so that setting StartPosition = CenterParent is respected.

            GettextResourceManager catalog = new GettextResourceManager("Menu");
            Localizer.Localize(this, catalog);

            // Windows 2000 and XP should use 8.25pt Tahoma, while Windows
            // Vista and later should use 9pt "Segoe UI". We'll use the
            // Message Box font to allow for user-customizations, though.
            Font = SystemFonts.MessageBoxFont;

            this.runActivity = runActivity;
            this.settings = settings;

            UpdateButtons();
        }

        private async void TestingForm_Shown(object sender, EventArgs e)
        {
            await LoadActivitiesAsync();
        }

        private void TestingForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (ctsTestActivityLoader != null && !ctsTestActivityLoader.IsCancellationRequested)
            {
                ctsTestActivityLoader.Cancel();
                ctsTestActivityLoader.Dispose();
            }
            if (ctsTestActivityRunner != null && !ctsTestActivityRunner.IsCancellationRequested)
            {
                ctsTestActivityRunner.Cancel();
                ctsTestActivityRunner.Dispose();
            }
        }

        private void UpdateButtons()
        {
            buttonTestAll.Enabled = ctsTestActivityRunner == null && gridTestActivities.RowCount > 0;
            buttonTest.Enabled = ctsTestActivityRunner == null && gridTestActivities.SelectedRows.Count > 0;
            buttonCancel.Enabled = ctsTestActivityRunner != null && !ctsTestActivityRunner.IsCancellationRequested;
            buttonSummary.Enabled = ctsTestActivityRunner == null && File.Exists(summaryFilePath);
            buttonDetails.Enabled = ctsTestActivityRunner == null && File.Exists(logFilePath);
        }

        private async Task LoadActivitiesAsync()
        {
            lock (testBindingSource)
            {
                if (ctsTestActivityLoader != null && !ctsTestActivityLoader.IsCancellationRequested)
                {
                    ctsTestActivityLoader.Cancel();
                    ctsTestActivityLoader.Dispose();
                }
                ctsTestActivityLoader = new CancellationTokenSource();
            }

            testBindingSource.DataSource = await Task.Run(() =>
            {
                return new SortableBindingList<TestActivity>((from f in Folder.GetFolders(settings).Result
                                                              from r in Route.GetRoutes(f, System.Threading.CancellationToken.None).Result
                                                              from a in Activity.GetActivities(f, r, System.Threading.CancellationToken.None).Result
                                                              where !(a is ORTS.Menu.ExploreActivity)
                                                              orderby a.Name
                                                              select new TestActivity(f, r, a)).ToList());
            });

            testBindingSource.Sort = "DefaultSort";
            UpdateButtons();
        }

        private async void ButtonTestAll_Click(object sender, EventArgs e)
        {
            await TestMarkedActivitiesAsync(from DataGridViewRow r in gridTestActivities.Rows
                                 select r);
        }

        private async void ButtonTest_Click(object sender, EventArgs e)
        {
            await TestMarkedActivitiesAsync(from DataGridViewRow r in gridTestActivities.Rows
                                 where r.Selected
                                 select r);
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            ctsTestActivityRunner?.Cancel();
            UpdateButtons();
        }

        private void ButtonNoSort_Click(object sender, EventArgs e)
        {
            gridTestActivities.Sort(defaultSortDataGridViewTextBoxColumn, ListSortDirection.Ascending);
        }

        private void ButtonSummary_Click(object sender, EventArgs e)
        {
            Process.Start(summaryFilePath);
        }

        private void ButtonDetails_Click(object sender, EventArgs e)
        {
            Process.Start(logFilePath);
        }

        private async Task TestMarkedActivitiesAsync(IEnumerable<DataGridViewRow> rows)
        {
            lock (testBindingSource)
            {
                if (ctsTestActivityRunner != null && !ctsTestActivityRunner.IsCancellationRequested)
                {
                    ctsTestActivityRunner.Cancel();
                    ctsTestActivityRunner.Dispose();
                }
                ctsTestActivityRunner = new CancellationTokenSource();
            }
            UpdateButtons();

            bool overrideSettings = checkBoxOverride.Checked;

            IEnumerable<Tuple<int, TestActivity>> items = from r in rows
                                                          select new Tuple<int, TestActivity>(r.Index, (TestActivity)r.DataBoundItem);

            try
            {
                foreach (var item in items)
                {
                    await Task.Run(() => RunTestTask(item.Item2, overrideSettings, ctsTestActivityRunner.Token), ctsTestActivityRunner.Token);
                    ShowGridRow(gridTestActivities, item.Item1);
                    if (ctsTestActivityRunner.IsCancellationRequested)
                        break;
                }
            }
            catch(TaskCanceledException)
            { }

            ctsTestActivityRunner.Dispose();
            ctsTestActivityRunner = null;
            UpdateButtons();
        }

        private async Task RunTestTask(TestActivity activity, bool overrideSettings, CancellationToken token)
        {
            string parameters = $"/Test /Logging /LoggingFilename=\"{Path.GetFileName(logFilePath)}\" /LoggingPath=\"{Path.GetDirectoryName(logFilePath)}\" " +
                $"/Profiling /ProfilingTime=10 /ShowErrorDialogs=False";
            if (overrideSettings)
                parameters += " /Skip-User-Settings";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = runActivity,
                WindowStyle = ProcessWindowStyle.Normal,
                WorkingDirectory = Application.StartupPath
            };

            if (!clearedLogs)
            {
                using (var writer = File.CreateText(summaryFilePath))
                    writer.WriteLine("Route, Activity, Passed, Errors, Warnings, Infos, Loading, FPS");
                using (var writer = File.CreateText(logFilePath))
                    writer.Flush();
                clearedLogs = true;
            }

            long summaryFilePosition = 0L;
            using (var reader = File.OpenText(summaryFilePath))
                summaryFilePosition = reader.BaseStream.Length;

            processStartInfo.Arguments = $"{parameters} \"{activity.ActivityFilePath}\"";
            activity.Passed = await RunProcess(processStartInfo, token) == 0;
            activity.Tested = true;

            using (var reader = File.OpenText(summaryFilePath))
            {
                reader.BaseStream.Seek(summaryFilePosition, SeekOrigin.Begin);
                var line = reader.ReadLine();
                if (!String.IsNullOrEmpty(line) && reader.EndOfStream)
                {
                    var csv = line.Split(',');
                    activity.Errors = String.Format("{0}/{1}/{2}", int.Parse(csv[3]), int.Parse(csv[4]), int.Parse(csv[5]));
                    activity.Load = String.Format("{0,6:F1}s", float.Parse(csv[6]));
                    activity.FPS = String.Format("{0,6:F1}", float.Parse(csv[7]));
                }
                else
                {
                    reader.ReadToEnd();
                    activity.Passed = false;
                }
                summaryFilePosition = reader.BaseStream.Position;
            }
        }

        public static Task<int> RunProcess(ProcessStartInfo processStartInfo, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<int>();
            processStartInfo.RedirectStandardError = true;
            processStartInfo.UseShellExecute = false;

            Process process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = processStartInfo
            };

            process.Exited += (sender, args) =>
            {
                tcs.TrySetResult(process.ExitCode);
                process.Dispose();
            };

            process.Start();
            using (token.Register(() =>
            {
                tcs.TrySetCanceled();
                process.CloseMainWindow();
            }))
            {

                return tcs.Task;
            }
        }

        private void ShowGridRow(DataGridView grid, int rowIndex)
        {
            var displayedRowCount = grid.DisplayedRowCount(false);
            if (grid.FirstDisplayedScrollingRowIndex > rowIndex)
                grid.FirstDisplayedScrollingRowIndex = rowIndex;
            else if (grid.FirstDisplayedScrollingRowIndex < rowIndex - displayedRowCount + 1)
                grid.FirstDisplayedScrollingRowIndex = rowIndex - displayedRowCount + 1;
            grid.InvalidateRow(rowIndex);
        }
    }
}
