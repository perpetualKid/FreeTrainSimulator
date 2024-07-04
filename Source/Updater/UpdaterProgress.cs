// COPYRIGHT 2014, 2015 by the Open Rails project.
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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common.Info;

using GetText;
using GetText.WindowsForms;

using Orts.Settings;
using Orts.Updater;

namespace FreeTrainSimulator.Updater
{
    public partial class UpdaterProgress : Form
    {
        private readonly UserSettings settings;
        private readonly Catalog catalog;

        public UpdaterProgress()
        {
            InitializeComponent();

            CatalogManager.SetCatalogDomainPattern(CatalogDomainPattern.AssemblyName, null, RuntimeInfo.LocalesFolder);
            catalog = CatalogManager.Catalog;

            settings = new UserSettings();
            LoadLanguage();
            BringToFront();
        }

        private void LoadLanguage()
        {
            if (settings.Language.Length > 0)
            {
                try
                {
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(settings.Language);
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
                        waitList.Add(WaitForProcessExitAsync(process));
                    }
                }
                catch (ArgumentException)
                {
                    // ArgumentException occurs if we try and GetProcessById with an ID that has already exited.
                }
            }
            await Task.WhenAll(waitList).ConfigureAwait(false);

            // Update manager is needed early to apply any updates before we show UI.
            UpdateManager updateManager = new UpdateManager(settings);
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
                targetVersion = targetVersion?[UpdateManager.VersionCommandLine.Length..];

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

        private async void UpdaterProgress_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                await RelaunchApplicationAsync().ConfigureAwait(false);
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

        private static async Task RelaunchApplicationAsync()
        {
            // If /RELAUNCH=1 is set, we're expected to re-launch the main application when we're done.
            bool relaunchApplication = Environment.GetCommandLineArgs().Any(a => string.Equals(a, $"{UpdateManager.RelaunchCommandLine}1", StringComparison.OrdinalIgnoreCase));
            if (relaunchApplication)
            {
                await UpdateManager.RunProcess(new ProcessStartInfo(RuntimeInfo.LauncherPath)).ConfigureAwait(true);
            }
        }

        /// <summary>
        /// Waits asynchronously for the process to exit.
        /// </summary>
        /// <param name="process">The process to wait for cancellation.</param>
        /// <param name="cancellationToken">A cancellation token. If invoked, the task will return 
        /// immediately as canceled.</param>
        /// <returns>A Task representing waiting for the process to end.</returns>
        private static async Task WaitForProcessExitAsync(Process process, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            void Process_Exited(object sender, EventArgs e)
            {
                _ = tcs.TrySetResult(true);
                process.Dispose();
            }

            try
            {
                if (process.HasExited)
                {
                    process.Close();
                    return;
                }
                process.EnableRaisingEvents = true;
                process.Exited += Process_Exited;
                using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                {
                    _ = await tcs.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                process.Exited -= Process_Exited;
            }
        }
    }
}
