using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.Shim;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

using GetText;
using GetText.WindowsForms;

namespace FreeTrainSimulator.Menu
{
    public partial class TestingForm : Form
    {
        private const string logFileName = "TestingLog.txt";
        private CancellationTokenSource ctsTestActivityLoader;
        private CancellationTokenSource ctsTestActivityRunner;
        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1);
        private readonly ContentModel contentModel;
        private readonly ProfileUserSettingsModel userSettings;
        private bool clearedLogs;
        private readonly string summaryFilePath = Path.Combine(RuntimeInfo.UserDataFolder, "TestingSummary.csv");
        private string logFilePath;

        public TestingForm(ContentModel contentModel, ProfileUserSettingsModel userSettings)
        {
            InitializeComponent();  // Needed so that setting StartPosition = CenterParent is respected.

            gridTestActivities.GetType().InvokeMember("DoubleBuffered", BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic, null, gridTestActivities, new object[] { true }, CultureInfo.InvariantCulture);

            Localizer.Localize(this, CatalogManager.Catalog);

            this.contentModel = contentModel;
            this.userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
            logFilePath = Path.Combine(userSettings.LogFilePath, logFileName);
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
                semaphoreSlim?.Dispose();
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
            ctsTestActivityLoader = await ctsTestActivityLoader.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            UseWaitCursor = true;
            gridTestActivities.SuspendLayout();
            testBindingSource.DataSource = new SortableBindingList<TestActivityModel>((await contentModel.LoadTestActivities(ctsTestActivityLoader.Token).ConfigureAwait(true)).Cast<TestActivityModel>().ToList());
            testBindingSource.Sort = "DefaultSort";
            gridTestActivities.ResumeLayout();
            UseWaitCursor = false;
            UpdateButtons();
        }

        private async void ButtonTestAll_Click(object sender, EventArgs e)
        {
            await TestMarkedActivitiesAsync(gridTestActivities.Rows.Cast<DataGridViewRow>()).ConfigureAwait(false);
        }

        private async void ButtonTest_Click(object sender, EventArgs e)
        {
            await TestMarkedActivitiesAsync(gridTestActivities.Rows.Cast<DataGridViewRow>().Where(r => r.Selected)).ConfigureAwait(false);
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
            SystemInfo.OpenFile(summaryFilePath);
        }

        private void ButtonDetails_Click(object sender, EventArgs e)
        {
            SystemInfo.OpenFile(logFilePath);
        }

        private async Task TestMarkedActivitiesAsync(IEnumerable<DataGridViewRow> rows)
        {
            ctsTestActivityRunner = await ctsTestActivityRunner.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            UpdateButtons();

            bool overrideSettings = checkBoxOverride.Checked;

            IEnumerable<(int, TestActivityModel)> items = rows.Select(r => (r.Index, (TestActivityModel)r.DataBoundItem));

            try
            {
                foreach ((int, TestActivityModel) item in items)
                {
                    TestActivityModel result = await Task.Run(() => RunTestTask(item.Item2, overrideSettings, ctsTestActivityRunner.Token), ctsTestActivityRunner.Token).ConfigureAwait(true);
                    ShowGridRow(gridTestActivities, item.Item1, result);
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

        private async Task<TestActivityModel> RunTestTask(TestActivityModel activity, bool overrideSettings, CancellationToken cancellationToken)
        {
            ProfileModel testingProfile = new ProfileModel(ProfileModel.TestingProfile);

            ProfileUserSettingsModel testingSettings = (overrideSettings ? new ProfileUserSettingsModel() : userSettings) with
            {
                Id = ProfileModel.TestingProfile,
                Name = ProfileModel.TestingProfile,
                LogFileName = logFileName,
                LogLevel = TraceEventType.Verbose,
                ErrorDialogEnabled = false,
                PauseAtStart = false,
                Profiling = true,
                ProfilingTime = 10,
            };
            _ = await testingProfile.UpdateSettingsModel(testingSettings, cancellationToken).ConfigureAwait(false);

            ProfileSelectionsModel profileSelections = new ProfileSelectionsModel()
            {
                Id = ProfileModel.TestingProfile,
                Name = ProfileModel.TestingProfile,
                ActivityId = activity.Id,
                ActivityType = ActivityType.Activity,
                FolderName = activity.Folder,
                RouteId = activity.Parent.Id,
                GamePlayAction = GamePlayAction.TestActivity,
            };
            _ = await testingProfile.UpdateSettingsModel(profileSelections, cancellationToken).ConfigureAwait(false);

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                FileName = RuntimeInfo.ActivityRunnerExecutable,
                WindowStyle = ProcessWindowStyle.Normal,
                WorkingDirectory = Application.StartupPath
            };

            if (!clearedLogs)
            {
                using (StreamWriter writer = File.CreateText(summaryFilePath))
                    await writer.WriteLineAsync("Route, Activity, Passed, Errors, Warnings, Infos, Load Time, FPS").ConfigureAwait(false);
                try
                {
                    File.Delete(logFilePath);
                }
                catch (IOException) { };
                clearedLogs = true;
            }

            long summaryFilePosition = 0L;
            using (StreamReader reader = File.OpenText(summaryFilePath))
                summaryFilePosition = reader.BaseStream.Length;

            processStartInfo.Arguments = $"/Profile={ProfileModel.TestingProfile}";
            bool passed = await RunProcessAsync(processStartInfo, cancellationToken).ConfigureAwait(false);
            string errors = string.Empty;
            string load = string.Empty;
            string fps = string.Empty;

            using (StreamReader reader = File.OpenText(summaryFilePath))
            {
                reader.BaseStream.Seek(summaryFilePosition, SeekOrigin.Begin);
                string line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(line) && reader.EndOfStream)
                {
                    string[] csv = line.Split(',');
                    errors = $"{int.Parse(csv[3], CultureInfo.InvariantCulture)}/{int.Parse(csv[4], CultureInfo.InvariantCulture)}/{int.Parse(csv[5], CultureInfo.InvariantCulture)}";
                    load = $"{float.Parse(csv[6], CultureInfo.InvariantCulture),6:F1}s";
                    fps = $"{float.Parse(csv[7], CultureInfo.InvariantCulture),6:F1}";
                }
                else
                {
                    await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    passed = false;
                }
                summaryFilePosition = reader.BaseStream.Position;
            }

            return activity with
            {
                Passed = passed,
                Tested = true,
                Errors = errors,
                Load = load,
                FPS = fps,
            };
        }

        public static async Task<bool> RunProcessAsync(ProcessStartInfo processStartInfo, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(processStartInfo);

            processStartInfo.RedirectStandardError = true;
            processStartInfo.UseShellExecute = false;

            Process process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = processStartInfo
            };

            try
            {
                _ = process.Start();
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(true);

                if (process.ExitCode != 0)
                {
                    string errorMessage = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    Trace.TraceWarning(errorMessage);
                }

                if (!process.HasExited)
                    _ = process.CloseMainWindow();
                return process.ExitCode == 0;
            }
            finally
            {
                process.Dispose();
            }
        }

        private static void ShowGridRow(DataGridView grid, int rowIndex, TestActivityModel activityModel)
        {
            (grid.DataSource as BindingSource)[rowIndex] = activityModel;
            int displayedRowCount = grid.DisplayedRowCount(false);
            if (grid.FirstDisplayedScrollingRowIndex > rowIndex)
                grid.FirstDisplayedScrollingRowIndex = rowIndex;
            else if (grid.FirstDisplayedScrollingRowIndex < rowIndex - displayedRowCount + 1)
                grid.FirstDisplayedScrollingRowIndex = rowIndex - displayedRowCount + 1;
            grid.InvalidateRow(rowIndex);
        }
    }
}
