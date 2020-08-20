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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Orts.Common;
using Orts.Common.Info;
using Orts.Settings;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Test.Orts")]

namespace Orts.Updater
{
    public class UpdateManager
    {
        private const string versionFile = "version.json";

        public const string ChannelCommandLine = "/CHANNEL=";
        public const string VersionCommandLine = "/VERSION=";
        public const string WaitProcessIdCommandLine = "/WAITPID=";
        public const string RelaunchCommandLine = "/RELAUNCH=";
        public const string ElevationCommandLine = "/ELEVATE=";

        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        private static string PathUpdateTest => Path.Combine(RuntimeInfo.ApplicationFolder, "UpdateTest");

        private static string PathUpdateDirty => Path.Combine(RuntimeInfo.ApplicationFolder, "UpdateDirty");
        private static string PathUpdateStage => Path.Combine(RuntimeInfo.ApplicationFolder, "UpdateStage");
        private static string PathDocumentation => Path.Combine(RuntimeInfo.ApplicationFolder, "Documentation");
        private static string PathUpdateDocumentation => Path.Combine(PathUpdateStage, "Documentation");
        private static string FileUpdateStage => Path.Combine(PathUpdateStage, "Update.zip");
        private static string FileSettings => Path.Combine(RuntimeInfo.ApplicationFolder, "OpenRails.ini");
        private static string FileUpdater => Path.Combine(RuntimeInfo.ApplicationFolder, "Updater.exe");

        public string ChannelName { get; private set; }
        public Exception LastCheckError { get; private set; }
        public bool UpdaterNeedsElevation { get; private set; }

        private UpdateChannels channels;
        private readonly UserSettings settings;

        private static string UserAgent => $"{RuntimeInfo.ProductName}/{VersionInfo.Version}";

        public UpdateManager(UserSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            channels = ResolveUpdateChannels();
            // Check for elevation to update; elevation is needed if the update writes failed and the user is NOT an
            // Administrator. Weird cases (like no permissions on the directory for anyone) are not handled.
            if (!CheckUpdateWrites())
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                UpdaterNeedsElevation = !principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public IEnumerable<string> GetChannels()
        {
            return channels.Channels.Select(channel => channel.Name);
        }

        public ChannelInfo GetChannelByName(string channelName)
        {
            return channels.Channels.FirstOrDefault(channel => channel.Name.Equals(channelName, StringComparison.OrdinalIgnoreCase));
        }

        public ChannelInfo GetChannelInfoByVersion(string normalizedVersion)
        {
            return channels.Channels.FirstOrDefault(channel => channel.NormalizedVersion.Equals(normalizedVersion, StringComparison.OrdinalIgnoreCase));
        }

        public static string VersionFile { get; } = Path.Combine(RuntimeInfo.ConfigFolder, versionFile);

        public async Task RefreshUpdateInfo(UpdateCheckFrequency frequency)
        {
            if (!CheckUpdateNeeded(frequency))
                return;

            LastCheckError = null;
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                client.DefaultRequestHeaders.UserAgent.TryParseAdd(UserAgent);
                UriBuilder uriBuilder = new UriBuilder(settings.UpdateSource + versionFile);

                try
                {
                    string versions = await client.GetStringAsync(uriBuilder.Uri).ConfigureAwait(false);
                    File.WriteAllText(VersionFile, versions);
                    channels = ResolveUpdateChannels();
                }
                catch (HttpRequestException httpException)
                {
                    Trace.WriteLine(httpException);
                    LastCheckError = httpException;
                }
            }
        }

        public static bool CheckUpdateNeeded(UpdateCheckFrequency target)
        {
            //we just check the update file's timestamp against the target
            switch (target)
            {
                case UpdateCheckFrequency.Never: return false;
                case UpdateCheckFrequency.Daily: return File.Exists(VersionFile) && File.GetLastWriteTime(VersionFile).AddDays(1) < DateTime.Now;
                case UpdateCheckFrequency.Weekly: return File.Exists(VersionFile) && File.GetLastWriteTime(VersionFile).AddDays(7) < DateTime.Now;
                case UpdateCheckFrequency.Biweekly: return File.Exists(VersionFile) && File.GetLastWriteTime(VersionFile).AddDays(14) < DateTime.Now;
                case UpdateCheckFrequency.Monthly: return File.Exists(VersionFile) && File.GetLastWriteTime(VersionFile).AddMonths(1) < DateTime.Now;
                default: return true; //Always
            }
        }

        private static UpdateChannels ResolveUpdateChannels()
        {
            if (File.Exists(VersionFile))
            {
                using (StreamReader reader = File.OpenText(VersionFile))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    return (UpdateChannels)serializer.Deserialize(reader, typeof(UpdateChannels));
                }
            }
            return UpdateChannels.Empty;
        }

        public string GetBestAvailableVersion(string targetVersion = "", string targetChannel = "")
        {
            var availableVersions = channels.Channels.Select(channel => channel.NormalizedVersion).ToList();

            return VersionInfo.SelectSuitableVersion(availableVersions, string.IsNullOrEmpty(targetChannel) ? settings.UpdateChannel : targetChannel, targetVersion);
        }

        public Task RunUpdateProcess()
        {
            Task updateTask = RunProcess(new ProcessStartInfo(FileUpdater, $"{ChannelCommandLine}{settings.UpdateChannel} " +
                $"{WaitProcessIdCommandLine}{Process.GetCurrentProcess().Id} {RelaunchCommandLine}1 {ElevationCommandLine}{(UpdaterNeedsElevation ? "1" : "0")}"));
            Environment.Exit(0);
            return updateTask;
        }

        public async Task ApplyUpdateAsync(ChannelInfo target, CancellationToken token)
        {
            if (null == target || null == target.DownloadUrl)
                throw new ApplicationException("No suitable update available");

            TriggerApplyProgressChanged(6);
            CheckUpdateWrites();
            TriggerApplyProgressChanged(7);

            await CleanDirectoriesAsync(token).ConfigureAwait(false);
            TriggerApplyProgressChanged(9);

            await DownloadUpdateAsync(9, 48, target.DownloadUrl, token).ConfigureAwait(false);
            TriggerApplyProgressChanged(57);

            await VerifyUpdateAsync(target.Hash).ConfigureAwait(false);
            TriggerApplyProgressChanged(62);

            await ExtractUpdate(62, 30).ConfigureAwait(false);
            TriggerApplyProgressChanged(92);

            if (await UpdateIsReadyAync().ConfigureAwait(false))
            {
                await CopyUpdateFileAsync().ConfigureAwait(false);
                TriggerApplyProgressChanged(98);

                await CleanDirectoriesAsync(token).ConfigureAwait(false);
                TriggerApplyProgressChanged(100);
            }
            else
                throw new ApplicationException("The update package is in an unexpected state.");
        }

        private void TriggerApplyProgressChanged(int progressPercentage)
        {
            ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(progressPercentage, null));
        }

        private static bool CheckUpdateWrites()
        {
            try
            {
                Directory.CreateDirectory(PathUpdateTest)?.Delete(true);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static async Task CleanDirectoriesAsync(CancellationToken token)
        {
            List<Task> cleanupTasks = new List<Task>();
            //temporarily suspended due to dual framework targeting
            //if (Directory.Exists(PathUpdateDirty))
            //    cleanupTasks.Add(CleanDirectory(PathUpdateDirty, token));

            //if (Directory.Exists(PathUpdateStage))
            //    cleanupTasks.Add(CleanDirectory(PathUpdateStage, token));

            //await Task.WhenAll(cleanupTasks).ConfigureAwait(false);

            string programPath = Path.GetDirectoryName(Path.GetDirectoryName(RuntimeInfo.ApplicationFolder));
            string[] targets = Directory.GetDirectories(programPath, "net*");

            foreach (string targetFolder in targets)
            {
                if (Directory.Exists(Path.Combine(targetFolder, "UpdateDirty")))
                    cleanupTasks.Add(CleanDirectory(Path.Combine(targetFolder, "UpdateDirty"), token));

                if (Directory.Exists(Path.Combine(targetFolder, "UpdateStage")))
                    cleanupTasks.Add(CleanDirectory(Path.Combine(targetFolder, "UpdateStage"), token));
            }
            await Task.WhenAll(cleanupTasks).ConfigureAwait(false);
        }

        private static Task CleanDirectory(string path, CancellationToken token)
        {
            //// Clean up as much as we can here, but any in-use files will fail. Don't worry about them. This is
            //// called again before the update begins so we'll always start from a clean state.
            //// Scan the files in any order.
            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                { Trace.TraceWarning($"{path} :: {ex.Message}"); };
            }
            if (token.IsCancellationRequested)
                return Task.FromCanceled(token);
            foreach (string directory in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
            {
                try { Directory.Delete(directory, true); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                { Trace.TraceWarning($"{path} :: {ex.Message}"); };
            }
            if (token.IsCancellationRequested)
                return Task.FromCanceled(token);
            try { Directory.Delete(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { Trace.TraceWarning($"{path} :: {ex.Message}"); };
            return Task.CompletedTask;
        }

        private async Task DownloadUpdateAsync(int progressMin, int progressLength, Uri downloadUrl, CancellationToken token)
        {
            DirectoryInfo stagingDirectory = Directory.CreateDirectory(PathUpdateStage);
            stagingDirectory.Attributes |= FileAttributes.Hidden;

            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd(UserAgent);
                HttpResponseMessage response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                long progressPercent = response.Content.Headers.ContentLength.GetValueOrDefault() / 100;
                using (Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
                    fileStream = new FileStream(FileUpdateStage, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    int bytesRead;

                    int percentage = 0;
                    do
                    {
                        bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                        await fileStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);

                        if (fileStream.Length / progressPercent > percentage)
                        {
                            TriggerApplyProgressChanged(progressMin + progressLength * ++percentage / 100);
                        }
                    }
                    while (bytesRead != 0);
                    await fileStream.FlushAsync(token).ConfigureAwait(false);
                }
            }
            TriggerApplyProgressChanged(progressMin + progressLength);
        }

        private Task ExtractUpdate(int progressMin, int progressLength)
        {
            using (FileStream fileStream = new FileStream(FileUpdateStage, FileMode.Open, FileAccess.Read))
            {
                using (ZipArchive zipFile = new ZipArchive(fileStream))
                {
                    // Note that this will give us a good DirectoryInfo even if destinationDirectoryName exists:
                    DirectoryInfo directoryInfo = Directory.CreateDirectory(PathUpdateStage);
                    string destinationDirectoryFullPath = directoryInfo.FullName;
                    int count = 0;

                    foreach (ZipArchiveEntry entry in zipFile.Entries)
                    {
                        count++;
                        string fileDestinationPath = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, entry.FullName));

                        if (!fileDestinationPath.StartsWith(destinationDirectoryFullPath, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("File is extracting to a destination outside of the folder specified.");

                        TriggerApplyProgressChanged(progressMin + progressLength * count / zipFile.Entries.Count);

                        if (Path.GetFileName(fileDestinationPath).Length == 0)
                        {
                            // Directory
                            if (entry.Length != 0)
                                throw new IOException("Directory entry with data.");
                            Directory.CreateDirectory(fileDestinationPath);
                        }
                        else
                        {
                            // File
                            // Create containing directory
                            Directory.CreateDirectory(Path.GetDirectoryName(fileDestinationPath));
                            entry.ExtractToFile(fileDestinationPath);
                        }
                    }
                }
            }
            File.Delete(FileUpdateStage);
            TriggerApplyProgressChanged(progressMin + progressLength);
            return Task.CompletedTask;
        }

        private static async Task<bool> UpdateIsReadyAync()
        {
            if (Directory.Exists(Path.Combine(PathUpdateStage, "Program")) && !File.Exists(Path.Combine(PathUpdateStage, RuntimeInfo.LauncherExecutable)))
            {
                //looks like the archive contains the root folder as well, so we move everything one level up
                await MoveDirectoryFiles(Path.Combine(PathUpdateStage, "Program"), PathUpdateStage, true).ConfigureAwait(false);
            }

            // The staging directory must exist, contain OpenRails.exe (be ready) and NOT contain the update zip.
            return await Task.FromResult(Directory.Exists(PathUpdateStage)
                && File.Exists(Path.Combine(PathUpdateStage, RuntimeInfo.LauncherExecutable))
                && !File.Exists(FileUpdateStage)).ConfigureAwait(false);
        }

        private static Task VerifyUpdateAsync(string targetHash)
        {
            using (SHA256 hashProvider = SHA256.Create())
            {
                using (FileStream file = new FileStream(FileUpdateStage, FileMode.Open, FileAccess.Read))
                {
                    byte[] hash = hashProvider.ComputeHash(file);
                    StringBuilder builder = new StringBuilder(64);
                    foreach (byte item in hash)
                    {
                        builder.Append($"{item:x2}");
                    }

                    if (!string.IsNullOrEmpty(targetHash) && !string.Equals(targetHash, builder.ToString(), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Could not confirm download integrity for downloaded package");
                }
            }
            return Task.CompletedTask;
        }

        private static async Task CopyUpdateFileAsync()
        {
            //temporarily suspended due to dual framework targeting
            //List<string> excludeDirs = new List<string>()
            //{
            //    RuntimeInfo.ConfigFolder, PathUpdateDirty, PathUpdateStage
            //};
            //if (Directory.Exists(PathUpdateDocumentation))
            //    excludeDirs.Add(PathDocumentation);

            //await MoveDirectoryFiles(RuntimeInfo.ApplicationFolder, PathUpdateDirty, true, excludeDirs, new string[] { FileSettings }).ConfigureAwait(false);

            //await MoveDirectoryFiles(PathUpdateStage, RuntimeInfo.ApplicationFolder, true).ConfigureAwait(false);

            string programPath = Path.GetDirectoryName(Path.GetDirectoryName(RuntimeInfo.ApplicationFolder));
            string[] targets = Directory.GetDirectories(programPath, "net*");
            List<Task> moveFileTasks = new List<Task>();

            foreach (string targetFolder in targets)
            {
                List<string> excludeDirs = new List<string>()
                {
                    Path.Combine(targetFolder, ".config"),
                    Path.Combine(targetFolder, "UpdateDirty"),
                    Path.Combine(targetFolder, "UpdateStage"),
                };
                if (Directory.Exists(Path.Combine(targetFolder, "Documentation")))
                    excludeDirs.Add(Path.Combine(targetFolder, "Documentation"));
                moveFileTasks.Add(MoveDirectoryFiles(targetFolder, Path.Combine(targetFolder, "UpdateDirty"), true, excludeDirs, new string[] { FileSettings }));
            }
            await Task.WhenAll(moveFileTasks).ConfigureAwait(false);

            foreach (string targetFolder in targets)
            {
                new DirectoryInfo(Path.Combine(targetFolder, "UpdateDirty")).Attributes |= FileAttributes.Hidden;
            }
            await MoveDirectoryFiles(PathUpdateStage, programPath, true).ConfigureAwait(false);

        }

        private static Task MoveDirectoryFiles(string sourceDirName, string destDirName, bool recursive,
            IEnumerable<string> excludedFolders = null, IEnumerable<string> excludedFiles = null)
        {
            if (null != excludedFolders && excludedFolders.Contains(sourceDirName))
            {
                return Task.CompletedTask;
            }

            // Get the subdirectories for the specified directory.
            DirectoryInfo source = new DirectoryInfo(sourceDirName);
            if (!source.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
            }
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            Parallel.ForEach(source.GetFiles(),
                 (file) =>
                 {
                     if (null == excludedFiles || !excludedFiles.Contains(file.FullName))
                         if (File.Exists(Path.Combine(destDirName, file.Name)))
                         {
                             Trace.TraceWarning($"Deleting extra file {Path.Combine(destDirName, file.Name)}");
                             File.Delete(Path.Combine(destDirName, file.Name));
                         }
                     file.MoveTo(Path.Combine(destDirName, file.Name));
                 });

            // If copying subdirectories, copy them and their contents to new location.
            if (recursive)
            {
                Parallel.ForEach(source.GetDirectories(),
                     (directory) =>
                     {
                         MoveDirectoryFiles(directory.FullName, Path.Combine(destDirName, directory.Name), recursive, excludedFolders, excludedFiles);
                     });
            }

            try { source.Delete(); }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is DirectoryNotFoundException)
            {
                Trace.TraceWarning($"{sourceDirName} :: {ex.Message} {ex.InnerException?.Message}");
            };
            return Task.CompletedTask;
        }

        public static Task RunProcess(ProcessStartInfo processStartInfo)
        {
            if (null == processStartInfo)
                throw new ArgumentNullException(nameof(processStartInfo));

            var tcs = new TaskCompletionSource<object>();
            processStartInfo.RedirectStandardError = true;
            processStartInfo.UseShellExecute = false;

            using (Process process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = processStartInfo
            })
            {

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
}
