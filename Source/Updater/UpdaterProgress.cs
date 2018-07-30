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

using GNU.Gettext;
using GNU.Gettext.WinForms;
//using ORTS.Common;
using ORTS.Settings;
using ORTS.Updater;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Updater
{
    public partial class UpdaterProgress : Form
    {
        private readonly UserSettings Settings;
        private readonly GettextResourceManager catalog = new GettextResourceManager("Updater");

        private string basePath;
        private string launcherPath;
        bool ShouldRelaunchApplication;

        public UpdaterProgress()
        {
            InitializeComponent();

            // Windows 2000 and XP should use 8.25pt Tahoma, while Windows
            // Vista and later should use 9pt "Segoe UI". We'll use the
            // Message Box font to allow for user-customizations, though.
            Font = SystemFonts.MessageBoxFont;

            Settings = new UserSettings(new string[0]);
            LoadLanguage();

            basePath = Path.GetDirectoryName(Application.ExecutablePath);
        }

        void LoadLanguage()
        {
            if (Settings.Language.Length > 0)
            {
                try
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(Settings.Language);
                }
                catch { }
            }

            Localizer.Localize(this, catalog);
        }

        private async void UpdaterProgress_Load(object sender, EventArgs e)
        {
            // If /RELAUNCH=1 is set, we're expected to re-launch the main application when we're done.
            ShouldRelaunchApplication = Environment.GetCommandLineArgs().Any(a => a == UpdateManager.RelaunchCommandLine + "1");

            // If /ELEVATE=1 is set, we're an elevation wrapper used to preserve the integrity level of the caller.
            var needsElevation = Environment.GetCommandLineArgs().Any(a => a == UpdateManager.ElevationCommandLine + "1");

            // Run everything in a new thread so the UI is responsive and visible.
            if (needsElevation)
                await AsyncElevation();
            else
                await AsyncUpdater();
        }

        private async Task AsyncElevation()
        {
            // Remove both the /RELAUNCH= and /ELEVATE= command-line flags from the child process - it should not do either.
            var processInfo = new ProcessStartInfo(Application.ExecutablePath, 
                String.Join(" ", Environment.GetCommandLineArgs().Skip(1).Where(a => !a.StartsWith(UpdateManager.RelaunchCommandLine) 
                && !a.StartsWith(UpdateManager.ElevationCommandLine)).ToArray()))
            {
                Verb = "runas"
            };

            Task processTask = RunProcessAsync(processInfo); // exit this current instance
            await Task.CompletedTask;
            Environment.Exit(0);
        }

        private async Task AsyncUpdater()
        {
            // We wait for any processes identified by /WAITPID=<pid> to exit before starting up so that the updater
            // will not try and apply an update whilst the previous instance is still lingering.
            List<Task> waitList = new List<Task>();
            
            var waitPids = Environment.GetCommandLineArgs().Where(a => a.StartsWith(UpdateManager.WaitProcessIdCommandLine));
            foreach (string waitPid in waitPids)
            {
                try
                {
                    Process process = Process.GetProcessById(int.Parse(waitPid.Substring(9)));
                    waitList.Add(WaitForProcessExitAsync(process));
                }
                catch (ArgumentException)
                {
                    // ArgumentException occurs if we try and GetProcessById with an ID that has already exited.
                }
            }
            await Task.WhenAll(waitList).ConfigureAwait(false);

            // Update manager is needed early to apply any updates before we show UI.
            UpdateManager updateManager = new UpdateManager(basePath, Application.ProductName, ORTS.Common.VersionInfo.VersionOrBuild);
            updateManager.ProgressChanged += (object sender, ProgressChangedEventArgs e) =>
            {
                Invoke((Action)(() =>
                {
                    progressBarUpdater.Value = e.ProgressPercentage;
                }));
            };

            string channelName = Enumerable.FirstOrDefault(Environment.GetCommandLineArgs(), a => a.StartsWith(UpdateManager.ChannelCommandLine));
            if (channelName != null && channelName.Length > UpdateManager.ChannelCommandLine.Length)
                updateManager.SetChannel(channelName.Substring(UpdateManager.ChannelCommandLine.Length));

            await updateManager.CheckForUpdateAsync().ConfigureAwait(false);
            if (updateManager.LastCheckError != null)
            {
                if (!IsDisposed)
                {
                    Invoke((Action)(() =>
                    {
                        MessageBox.Show("Error: " + updateManager.LastCheckError, Application.ProductName + " " + ORTS.Common.VersionInfo.VersionOrBuild, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
                Application.Exit();
                return;
            }

            await updateManager.ApplyUpdateAsync().ConfigureAwait(false);
            if (updateManager.LastUpdateError != null)
            {
                if (!IsDisposed)
                {
                    Invoke((Action)(() =>
                    {
                        MessageBox.Show("Error: " + updateManager.LastUpdateError, Application.ProductName + " " + ORTS.Common.VersionInfo.VersionOrBuild, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
                Application.Exit();
                return;
            }

            await RelaunchApplicationAsync().ConfigureAwait(false);

            Environment.Exit(0);

        }

        private async void UpdaterProgress_FormClosed(object sender, FormClosedEventArgs e)
        {
            await RelaunchApplicationAsync();
        }

        private async void RelaunchApplication()
        {
            if (ShouldRelaunchApplication)
            {
                launcherPath = await UpdateManager.GetMainExecutableAsync(basePath, Application.ProductName);

                var process = Process.Start(launcherPath);
            }
        }

        private async Task RelaunchApplicationAsync()
        {
            if (ShouldRelaunchApplication)
            {
                launcherPath = await UpdateManager.GetMainExecutableAsync(basePath, Application.ProductName).ConfigureAwait(false);
                await RunProcessAsync(launcherPath).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Waits asynchronously for the process to exit.
        /// </summary>
        /// <param name="process">The process to wait for cancellation.</param>
        /// <param name="cancellationToken">A cancellation token. If invoked, the task will return 
        /// immediately as canceled.</param>
        /// <returns>A Task representing waiting for the process to end.</returns>
        public static async Task WaitForProcessExitAsync(Process process, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<bool>();
            void Process_Exited(object sender, EventArgs e)
            {
                tcs.TrySetResult(true);
            }

            process.EnableRaisingEvents = true;
            process.Exited += Process_Exited;

            try
            {
                if (process.HasExited)
                {
                    process.Close();
                    return;
                }
                using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                {
                    await tcs.Task;
                }
            }
            finally
            {
                process.Exited -= Process_Exited;
            }
        }

        public static Task RunProcessAsync(ProcessStartInfo processStartInfo)
        {
            var tcs = new TaskCompletionSource<object>();
            processStartInfo.RedirectStandardError = true;
            processStartInfo.UseShellExecute = false;

            Process process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = processStartInfo
            };

            process.Exited += (sender, args) =>
            {
                if (process.ExitCode != 0)
                {
                    var errorMessage = process.StandardError.ReadToEnd();
                    tcs.SetException(new InvalidOperationException("The process did not exit correctly. " +
                        "The corresponding error message was: " + errorMessage));
                }
                else
                {
                    tcs.SetResult(null);
                }
                process.Dispose();
            };
            process.Start();
            return tcs.Task;
        }

        public static Task RunProcessAsync(string processPath)
        {
            var tcs = new TaskCompletionSource<object>();
            Process process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo(processPath)
                {
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            process.Exited += (sender, args) =>
            {
                if (process.ExitCode != 0)
                {
                    var errorMessage = process.StandardError.ReadToEnd();
                    tcs.SetException(new InvalidOperationException("The process did not exit correctly. " +
                        "The corresponding error message was: " + errorMessage));
                }
                else
                {
                    tcs.SetResult(null);
                }
                process.Dispose();
            };
            process.Start();
            return tcs.Task;
        }
    }
}
