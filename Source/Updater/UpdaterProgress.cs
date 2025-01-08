using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;

using GetText;
using GetText.WindowsForms;

namespace FreeTrainSimulator.Updater
{
    public partial class UpdaterProgress : Form
    {
        private readonly Catalog catalog;

        public UpdaterProgress()
        {
            InitializeComponent();

            CatalogManager.SetCatalogDomainPattern(CatalogDomainPattern.AssemblyName, null, RuntimeInfo.LocalesFolder);
            catalog = CatalogManager.Catalog;

            LoadLanguage();
            BringToFront();
        }

        private void LoadLanguage()
        {
            string language = Enumerable.FirstOrDefault(Environment.GetCommandLineArgs(), a => a.StartsWith(UpdateManager.LanguageCommandLine, StringComparison.OrdinalIgnoreCase));
            language = language?[UpdateManager.LanguageCommandLine.Length..];

            if (!string.IsNullOrEmpty(language))
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(language);
                }
                catch (ArgumentException) { }
            }

            Localizer.Localize(this, catalog);
        }

        private async void UpdaterProgress_Load(object sender, EventArgs e)
        {
            // If /ELEVATE=1 is set, we're an elevation wrapper used to preserve the integrity level of the caller.
            bool needsElevation = Environment.GetCommandLineArgs().Any(a => string.Equals(a, $"{UpdateManager.ElevationCommandLine}1", StringComparison.OrdinalIgnoreCase));

            // Run everything in a new thread so the UI is responsive and visible.
            if (needsElevation)
                RunWithElevation();
            else
                await AsyncUpdater().ConfigureAwait(true);
        }

        private static void RunWithElevation()
        {
            // Remove /ELEVATE= command-line flags from the child process
            ProcessStartInfo processInfo = new ProcessStartInfo(Application.ExecutablePath,
                string.Join(" ", Environment.GetCommandLineArgs().Skip(1).Where(a => !a.StartsWith(UpdateManager.ElevationCommandLine, StringComparison.OrdinalIgnoreCase)).ToArray()))
            {
                Verb = "runas"
            };

            Task processTask = UpdateManager.RunProcess(processInfo); // exit this current instance
            Environment.Exit(0);
        }

        private async Task AsyncUpdater()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            // We wait for any processes identified by /WAITPID=<pid> to exit before starting up so that the updater
            // will not try and apply an update whilst the previous instance is still lingering.
            List<Task> waitList = new List<Task>();

            IEnumerable<string> waitPids = Environment.GetCommandLineArgs().Where(a => a.StartsWith(UpdateManager.WaitProcessIdCommandLine, StringComparison.OrdinalIgnoreCase));
            foreach (string waitPid in waitPids)
            {
                try
                {
                    if (int.TryParse(waitPid.AsSpan(9), out int processId))
                    {
                        Process process = Process.GetProcessById(processId);
                        waitList.Add(process.WaitForExitAsync(cts.Token));
                    }
                }
                catch (ArgumentException)
                {
                    // ArgumentException occurs if we try and GetProcessById with an ID that has already exited.
                }
            }
            await Task.WhenAll(waitList).ConfigureAwait(false);

            // Update manager is needed early to apply any updates before we show UI.
            string updateModeText = Enumerable.FirstOrDefault(Environment.GetCommandLineArgs(), a => a.StartsWith(UpdateManager.UpdateModeCommandLine, StringComparison.OrdinalIgnoreCase));
            updateModeText = updateModeText?[UpdateManager.UpdateModeCommandLine.Length..];
            UpdateMode updateMode;
            if (!Enum.TryParse(updateModeText, out updateMode))
                updateMode = UpdateMode.Release;

            UpdateManager updateManager = new UpdateManager(updateMode);
            updateManager.ProgressChanged += (object sender, ProgressChangedEventArgs e) =>
            {
                Invoke(() =>
                {
                    progressBarUpdater.Value = e.ProgressPercentage;
                });
            };

            try
            {
                string targetVersion = Enumerable.FirstOrDefault(Environment.GetCommandLineArgs(), a => a.StartsWith(UpdateManager.VersionCommandLine, StringComparison.OrdinalIgnoreCase));
                targetVersion = targetVersion?[UpdateManager.VersionCommandLine.Length..]?.Trim('\"');

                Invoke(() =>
                {
                    progressBarUpdater.Value = 5;
                });
                Application.DoEvents();

                await updateManager.ApplyUpdateAsync(targetVersion, cts.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                if (!IsDisposed)
                {
                    Invoke(() =>
                    {
                        _ = MessageBox.Show(catalog.GetString($"Error: {exception.Message} {exception.InnerException?.Message}"),
                            $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
                return;
                throw;
            }
            finally
            {
                updateManager.Dispose();
                cts.Dispose();
                Application.Exit();
            }
        }

        private void UpdaterProgress_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                RelaunchApplication();
            }
            catch (Exception exception)
            {
                if (!IsDisposed)
                {
                    Invoke(() =>
                    {
                        _ = MessageBox.Show(catalog.GetString($"Error: {exception.Message} {exception.InnerException?.Message}"),
                            $"{RuntimeInfo.ProductName} {VersionInfo.Version}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
                Application.Exit();
                return;
                throw;
            }
        }

        private static void RelaunchApplication()
        {
            // If /RELAUNCH=1 is set, we're expected to re-launch the main application when we're done.
            bool relaunchApplication = Environment.GetCommandLineArgs().Any(a => string.Equals(a, $"{UpdateManager.RelaunchCommandLine}1", StringComparison.OrdinalIgnoreCase));
            if (relaunchApplication)
            {
                SystemInfo.OpenApplication(RuntimeInfo.LauncherPath);
            }
        }
    }
}
