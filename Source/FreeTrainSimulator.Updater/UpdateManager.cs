using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;

using Newtonsoft.Json;

using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

using Orts.Settings;

using VersionInfo = FreeTrainSimulator.Common.Info.VersionInfo;

namespace FreeTrainSimulator.Updater
{
    public partial class UpdateManager : IDisposable
    {
        [GeneratedRegex(".(g[a-z0-9]{8}$)", RegexOptions.Compiled)]
        private static partial Regex removeCommitDataRegex();

        private const string versionFile = "updates.json";
        private static readonly Regex removeCommitData = removeCommitDataRegex();

        private const string developerBuildsUrl = "https://orts.blob.core.windows.net/builds/index.json"; //TODO 20210418 this may be somewhere in configuration instead hard coded

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
        private static string FileSettings => Path.Combine(RuntimeInfo.ApplicationFolder, "OpenRails.ini");
        private static string FileUpdater => Path.Combine(RuntimeInfo.ApplicationFolder, "Updater.exe");

        public string ChannelName { get; private set; }
        public Exception LastCheckError { get; private set; }
        public bool UpdaterNeedsElevation { get; private set; }

        private readonly UserSettings settings;
        private readonly SemaphoreSlim updateVersions = new SemaphoreSlim(1);
        private bool disposedValue;

        public UpdateManager(UserSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Check for elevation to update; elevation is needed if the update writes failed and the user is NOT an
            // Administrator. Weird cases (like no permissions on the directory for anyone) are not handled.
            if (!CheckUpdateWrites())
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                UpdaterNeedsElevation = !principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static string VersionFile { get; } = Path.Combine(RuntimeInfo.ConfigFolder, versionFile);

        public async Task<IEnumerable<NuGetVersion>> RefreshUpdateInfo(UpdateCheckFrequency frequency)
        {
            if (!CheckUpdateNeeded(frequency))
                return CachedUpdateVersions();

            await updateVersions.WaitAsync().ConfigureAwait(false);
            LastCheckError = null;
            ILogger logger = NullLogger.Instance;
            CancellationToken cancellationToken = CancellationToken.None;

            using (SourceCacheContext cache = new SourceCacheContext
            {
                DirectDownload = true,
                NoCache = true
            })
            {
                try
                {
                    SourceRepository repository = Repository.Factory.GetCoreV3(settings.UpdateSource);
                    FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>().ConfigureAwait(false);
                    IEnumerable<NuGetVersion> result = await resource.GetAllVersionsAsync(VersionInfo.PackageId, cache, logger, cancellationToken).ConfigureAwait(false);
                    string versions = result.ToJson(Formatting.Indented);
                    await File.WriteAllTextAsync(VersionFile, versions).ConfigureAwait(false);
                    return result;
                }
                catch (NuGetProtocolException exception)
                {
                    Trace.WriteLine(exception);
                    LastCheckError = exception;
                }
                catch (IOException)
                { }
                finally
                {
                    updateVersions.Release();
                }
            }
            return Enumerable.Empty<NuGetVersion>();
        }

        public static IEnumerable<NuGetVersion> CachedUpdateVersions()
        {
            try
            {
                if (File.Exists(VersionFile))
                {
                    using (StreamReader reader = File.OpenText(VersionFile))
                    {
                        return reader.ReadToEnd().FromJson<IEnumerable<NuGetVersion>>();
                    }
                }
            }
            catch (JsonSerializationException)
            {
                try
                {
                    File.Delete(VersionFile);
                }
                catch (IOException) { }
            }
            catch (IOException) { }
            return Enumerable.Empty<NuGetVersion>();
        }

        public void SetUpdateChannel(bool prereleases, bool developerBuilds)
        {
            if (developerBuilds)
            {
                settings.UpdateSource = developerBuildsUrl;
                settings.Save(nameof(settings.UpdateSource));
                settings.UpdatePreReleases = true;
                settings.Save(nameof(settings.UpdatePreReleases));
            }
            else
            {
                settings.UpdateSource = (string)settings.GetDefaultValue(nameof(settings.UpdateSource));
                settings.Save(nameof(settings.UpdateSource));
                settings.UpdatePreReleases = prereleases;
                settings.Save(nameof(settings.UpdatePreReleases));
            }
        }

        public static bool CheckUpdateNeeded(UpdateCheckFrequency target)
        {
            //we just check the update file's timestamp against the target
            switch (target)
            {
                case UpdateCheckFrequency.Never:
                    return false;
                case UpdateCheckFrequency.Daily:
                    return File.Exists(VersionFile) && File.GetLastWriteTime(VersionFile).AddDays(1) < DateTime.Now;
                case UpdateCheckFrequency.Weekly:
                    return File.Exists(VersionFile) && File.GetLastWriteTime(VersionFile).AddDays(7) < DateTime.Now;
                case UpdateCheckFrequency.Biweekly:
                    return File.Exists(VersionFile) && File.GetLastWriteTime(VersionFile).AddDays(14) < DateTime.Now;
                case UpdateCheckFrequency.Monthly:
                    return File.Exists(VersionFile) && File.GetLastWriteTime(VersionFile).AddMonths(1) < DateTime.Now;
                default:
                    return true; //Always
            }
        }

        public async Task<string> GetBestAvailableVersionString(bool refresh)
        {
            return (await GetBestAvailableVersion(refresh).ConfigureAwait(false))?.ToString();
        }

        public static string NormalizedPackageVersion(string packageVersion)
        {
            return packageVersion == null ? packageVersion : removeCommitData.Replace(packageVersion, string.Empty);
        }

        public async Task<NuGetVersion> GetBestAvailableVersion(bool refresh)
        {
            IEnumerable<NuGetVersion> availableVersions = await RefreshUpdateInfo(refresh ? UpdateCheckFrequency.Always : (UpdateCheckFrequency)settings.UpdateCheckFrequency).ConfigureAwait(false);

            return VersionInfo.GetBestAvailableVersion(availableVersions, settings.UpdatePreReleases);
        }

        public Task RunUpdateProcess(string targetVersion)
        {
            Task updateTask = RunProcess(new ProcessStartInfo(FileUpdater, $"{VersionCommandLine}\"{targetVersion}\" " +
                $"{WaitProcessIdCommandLine}{Environment.ProcessId} {RelaunchCommandLine}1 {ElevationCommandLine}{(UpdaterNeedsElevation ? "1" : "0")}"));
            Environment.Exit(0);
            return updateTask;
        }

        public async Task ApplyUpdateAsync(string targetVersionString, CancellationToken token)
        {
            NuGetVersion targetVersion;
            if (string.IsNullOrEmpty(targetVersionString))
                targetVersion = await GetBestAvailableVersion(true).ConfigureAwait(false);
            else
                _ = NuGetVersion.TryParse(targetVersionString, out targetVersion);

            if (null == targetVersion)
                throw new InvalidOperationException("No suitable update version found nor given in commandline." + Environment.NewLine + Environment.NewLine +
                    "This may be caused by missing connectivity to check with the online update repository.");

            TriggerApplyProgressChanged(6);
            CheckUpdateWrites();
            TriggerApplyProgressChanged(7);

            await CleanDirectoriesAsync(token).ConfigureAwait(false);
            TriggerApplyProgressChanged(9);

            await DownloadAndExpandUpdateAsync(targetVersion, token).ConfigureAwait(false);
            TriggerApplyProgressChanged(90);

            if (await UpdateIsReadyAync($"{VersionInfo.PackageId}.{targetVersion}").ConfigureAwait(false))
            {
                await CopyUpdateFileAsync().ConfigureAwait(false);
                TriggerApplyProgressChanged(98);

                await CleanDirectoriesAsync(token).ConfigureAwait(false);
                TriggerApplyProgressChanged(100);
            }
            else
                throw new InvalidOperationException("The update package is in an unexpected state.");
        }

        private void TriggerApplyProgressChanged(int progressPercentage)
        {
            ProgressChanged?.Invoke(this, new ProgressChangedEventArgs(progressPercentage, null));
        }

        private static bool CheckUpdateWrites()
        {
            try
            {
                Directory.CreateDirectory(PathUpdateTest).Delete(true);
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
                try
                { File.Delete(file); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                { Trace.TraceWarning($"{path} :: {ex.Message}"); };
            }
            if (token.IsCancellationRequested)
                return Task.FromCanceled(token);
            foreach (string directory in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
            {
                try
                { Directory.Delete(directory, true); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                { Trace.TraceWarning($"{path} :: {ex.Message}"); };
            }
            if (token.IsCancellationRequested)
                return Task.FromCanceled(token);
            try
            { Directory.Delete(path, true); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            { Trace.TraceWarning($"{path} :: {ex.Message}"); };
            return Task.CompletedTask;
        }

        private async Task DownloadAndExpandUpdateAsync(NuGetVersion targetVersion, CancellationToken token)
        {
            int progressMin = 10;
            int progressLength = 60;
            DirectoryInfo stagingDirectory = Directory.CreateDirectory(PathUpdateStage);
            stagingDirectory.Attributes |= FileAttributes.Hidden;

            SourceRepository repository = Repository.Factory.GetCoreV3(settings.UpdateSource);
            FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>(token).ConfigureAwait(false);
            using (SourceCacheContext cacheContext = new SourceCacheContext { DirectDownload = true, NoCache = true })
            {
                using (ProgressMemoryStream packageStream = new ProgressMemoryStream())
                {
                    packageStream.ProgressChanged += (sender, e) =>
                    {
                        TriggerApplyProgressChanged(progressMin + progressLength * e.ProgressPercentage / 100);
                    };
                    Task<bool> downloadTask = resource.CopyNupkgToStreamAsync(VersionInfo.PackageId, targetVersion, packageStream, cacheContext, NullLogger.Instance, token);
                    packageStream.ExpectedLength = await repository.PackageSize(new PackageIdentity(VersionInfo.PackageId, targetVersion), token).ConfigureAwait(false);
                    if (await downloadTask.ConfigureAwait(false))
                    {
                        TriggerApplyProgressChanged(progressMin + progressLength);
                        packageStream.Position = 0;
                        progressMin = 70;
                        progressLength = 20;
                        PackagePathResolver packagePathResolver = new PackagePathResolver(Path.GetFullPath(PathUpdateStage));
                        PackageExtractionContext extractionContext = new PackageExtractionContext(PackageSaveMode.Files, XmlDocFileSaveMode.Skip, null, NullLogger.Instance);
                        IEnumerable<string> files = await PackageExtractor.ExtractPackageAsync(settings.UpdateSource, packageStream, packagePathResolver, extractionContext, token).ConfigureAwait(false);
                    }
                }
            }
        }

        private static async Task<bool> UpdateIsReadyAync(string versionFolder)
        {
            if (Directory.Exists(Path.Combine(PathUpdateStage, versionFolder, "Program")) && !File.Exists(Path.Combine(PathUpdateStage, versionFolder, RuntimeInfo.LauncherExecutable)))
            {
                //looks like the archive contains the root folder as well, so we move everything one level up
                await MoveDirectoryFiles(Path.Combine(PathUpdateStage, versionFolder, "Program"), PathUpdateStage, true).ConfigureAwait(false);
                Directory.Delete(Path.Combine(PathUpdateStage, versionFolder), true);
            }

            // The staging directory must exist, contain OpenRails.exe (be ready).
            return await Task.FromResult(Directory.Exists(PathUpdateStage) && File.Exists(Path.Combine(PathUpdateStage, RuntimeInfo.LauncherExecutable))).ConfigureAwait(false);
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
            try
            {
                if (!source.EnumerateFileSystemInfos().Any())
                    source.Delete();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is DirectoryNotFoundException)
            {
                Trace.TraceWarning($"{sourceDirName} :: {ex.Message} {ex.InnerException?.Message}");
            };
            return Task.CompletedTask;
        }

        public static Task RunProcess(ProcessStartInfo processStartInfo)
        {
            ArgumentNullException.ThrowIfNull(processStartInfo);

            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    updateVersions?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
