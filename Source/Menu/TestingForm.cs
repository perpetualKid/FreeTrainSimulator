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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using GetText;
using GetText.WindowsForms;

using Orts.Common.Info;
using Orts.Models.Simplified;
using Orts.Settings;

using Path = System.IO.Path;

namespace Orts.Menu
{
    public partial class TestingForm : Form
    {
        private CancellationTokenSource ctsTestActivityLoader;
        private CancellationTokenSource ctsTestActivityRunner;
        private bool clearedLogs;
        private readonly string runActivity;
        private readonly UserSettings settings;
        private readonly string summaryFilePath = Path.Combine(RuntimeInfo.UserDataFolder, "TestingSummary.csv");
        private readonly string logFilePath = Path.Combine(RuntimeInfo.UserDataFolder, "TestingLog.txt");

        public TestingForm(UserSettings settings, string runActivity)
        {
            InitializeComponent();  // Needed so that setting StartPosition = CenterParent is respected.

            gridTestActivities.GetType().InvokeMember("DoubleBuffered", BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic, null, gridTestActivities, new object[] { true }, CultureInfo.InvariantCulture);

            Localizer.Localize(this, CatalogManager.Catalog);

            this.runActivity = runActivity;
            this.settings = settings;

            UpdateButtons();
        }

        private async void TestingForm_Shown(object sender, EventArgs e)
        {
            await LoadActivitiesAsync().ConfigureAwait(true);
        }

        private void TestingForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (ctsTestActivityLoader != null && !ctsTestActivityLoader.IsCancellationRequested)
            {
                ctsTestActivityLoader.Cancel();
            }
            if (ctsTestActivityRunner != null && !ctsTestActivityRunner.IsCancellationRequested)
            {
                ctsTestActivityRunner.Cancel();
            }
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                ctsTestActivityLoader?.Dispose();
                ctsTestActivityRunner?.Dispose();
            }
            base.Dispose(disposing);
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
            lock (testBindingSource.DataSource)
            {
                if (ctsTestActivityLoader != null && !ctsTestActivityLoader.IsCancellationRequested)
                {
                    ctsTestActivityLoader.Cancel();
                    ctsTestActivityLoader.Dispose();
                }
                ctsTestActivityLoader = new CancellationTokenSource();
            }
            UseWaitCursor = true;
            gridTestActivities.SuspendLayout();
            testBindingSource.DataSource = new SortableBindingList<TestActivity>((await TestActivity.GetTestActivities(settings.FolderSettings.Folders, CancellationToken.None).ConfigureAwait(true)).ToList());
            testBindingSource.Sort = "DefaultSort";
            gridTestActivities.ResumeLayout();
            UseWaitCursor = false;
            UpdateButtons();
        }

        private async void ButtonTestAll_Click(object sender, EventArgs e)
        {
            await TestMarkedActivitiesAsync(from DataGridViewRow r in gridTestActivities.Rows
                                            select r).ConfigureAwait(false);
        }

        private async void ButtonTest_Click(object sender, EventArgs e)
        {
            await TestMarkedActivitiesAsync(from DataGridViewRow r in gridTestActivities.Rows
                                            where r.Selected
                                            select r).ConfigureAwait(false);
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
            Process.Start(new ProcessStartInfo { FileName = summaryFilePath, UseShellExecute = true });
        }

        private void ButtonDetails_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = logFilePath, UseShellExecute = true });
        }

        private async Task TestMarkedActivitiesAsync(IEnumerable<DataGridViewRow> rows)
        {
            lock (testBindingSource.DataSource)
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
                foreach (Tuple<int, TestActivity> item in items)
                {
                    await Task.Run(() => RunTestTask(item.Item2, overrideSettings, ctsTestActivityRunner.Token), ctsTestActivityRunner.Token).ConfigureAwait(true);
                    ShowGridRow(gridTestActivities, item.Item1);
                    if (ctsTestActivityRunner.IsCancellationRequested)
                        break;
                }
            }
            catch (TaskCanceledException)
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

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = runActivity,
                WindowStyle = ProcessWindowStyle.Normal,
                WorkingDirectory = Application.StartupPath
            };

            if (!clearedLogs)
            {
                using (StreamWriter writer = File.CreateText(summaryFilePath))
                    await writer.WriteLineAsync("Route, Activity, Passed, Errors, Warnings, Infos, Load Time, FPS").ConfigureAwait(false);
                using (StreamWriter writer = File.CreateText(logFilePath))
                    await writer.FlushAsync(token).ConfigureAwait(false);
                clearedLogs = true;
            }

            long summaryFilePosition = 0L;
            using (StreamReader reader = File.OpenText(summaryFilePath))
                summaryFilePosition = reader.BaseStream.Length;

            processStartInfo.Arguments = $"{parameters} \"{activity.ActivityFilePath}\"";
            activity.Passed = await RunProcess(processStartInfo, token).ConfigureAwait(false) == 0;
            activity.Tested = true;

            using (StreamReader reader = File.OpenText(summaryFilePath))
            {
                reader.BaseStream.Seek(summaryFilePosition, SeekOrigin.Begin);
                string line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(line) && reader.EndOfStream)
                {
                    string[] csv = line.Split(',');
                    activity.Errors = $"{int.Parse(csv[3], CultureInfo.InvariantCulture)}/{int.Parse(csv[4], CultureInfo.InvariantCulture)}/{int.Parse(csv[5], CultureInfo.InvariantCulture)}";
                    activity.Load = $"{float.Parse(csv[6], CultureInfo.InvariantCulture),6:F1}s";
                    activity.FPS = $"{float.Parse(csv[7], CultureInfo.InvariantCulture),6:F1}";
                }
                else
                {
                    await reader.ReadToEndAsync(token).ConfigureAwait(false);
                    activity.Passed = false;
                }
                summaryFilePosition = reader.BaseStream.Position;
            }
        }

        public static Task<int> RunProcess(ProcessStartInfo processStartInfo, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(processStartInfo);

            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            processStartInfo.RedirectStandardError = true;
            processStartInfo.UseShellExecute = false;

#pragma warning disable CA2000 // Dispose objects before losing scope
            Process process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = processStartInfo
            };
#pragma warning restore CA2000 // Dispose objects before losing scope

            process.Exited += (sender, args) =>
            {
                if (process.ExitCode != 0)
                {
                    string errorMessage = process.StandardError.ReadToEnd();
                    tcs.TrySetException(new InvalidOperationException("The process did not exit correctly. " +
                        "The corresponding error message was: " + errorMessage));
                }
                tcs.TrySetResult(process.ExitCode);
                process.Dispose();
            };
            process.Start();
            token.Register(() =>
            {
                if (!process.HasExited)
                    process.CloseMainWindow();
                tcs.TrySetCanceled();
            });
            return tcs.Task;
        }

        private static void ShowGridRow(DataGridView grid, int rowIndex)
        {
            int displayedRowCount = grid.DisplayedRowCount(false);
            if (grid.FirstDisplayedScrollingRowIndex > rowIndex)
                grid.FirstDisplayedScrollingRowIndex = rowIndex;
            else if (grid.FirstDisplayedScrollingRowIndex < rowIndex - displayedRowCount + 1)
                grid.FirstDisplayedScrollingRowIndex = rowIndex - displayedRowCount + 1;
            grid.InvalidateRow(rowIndex);
        }
    }
}
