using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Hashing;
using System.Reflection;
using System.Text;

using FreeTrainSimulator.Common.Logging;

namespace FreeTrainSimulator.Common.Info
{
    public static class RuntimeInfo
    {
        public const string LauncherExecutable = "FreeTrainSimulator.exe";

        public const string ActivityRunnerExecutable = "ActivityRunner.exe";

        public static readonly string ProductName = VersionInfo.ProductName();

        public const string WikiLink = "https://github.com/perpetualKid/FreeTrainSimulator/wiki";
        public const string WhatsNewLinkTemplate = "https://github.com/perpetualKid/FreeTrainSimulator/blob/gitcodeversion/WHATSNEW.md";

        /// <summary>
        /// returns the Application as part of the product family, like (Family), like "Free Train Simulator"
        /// </summary>
        public static string ApplicationName { get; } = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).FileDescription;

        /// <summary>
        /// returns the Product Name (Family), like "Free Train Simulator Toolbox" return "Toolbox" for "Free Train Simulator" product name
        /// </summary>
        public static string ProductApplication { get; } = ApplicationName.Replace(ProductName, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

        /// <summary>
        /// returns the application entry file, i.e. Contrib.ContentManager
        /// </summary>
        public static string ApplicationFile { get; } = Path.GetFileName(Assembly.GetEntryAssembly().Location);
        /// <summary>
        /// returns the current application base directory, i.e. Program\netcoreapp3.1
        /// </summary>
        public static string ApplicationFolder { get; } = AppContext.BaseDirectory;

        public static string UserDataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductName);

        public static string CacheFolder { get; } = Path.Combine(UserDataFolder, "Cache");

        public static string ContentFolder { get; } = Path.Combine(ApplicationFolder, "content");

        public static string LogFilesFolder { get; } = Path.Combine(UserDataFolder, "Logs");
        /// <summary>
        /// returns the common program root. While this may be same as <see cref="ApplicationFolder"/>
        /// this is one level up in dual target environment ("Program" for "Program\netcoreapp3.1")
        /// </summary>
        public static string ProgramRoot { get; } = Path.GetFullPath(Path.Combine(ApplicationFolder, ".."));

        public static string LauncherPath { get; } = Path.Combine(ProgramRoot, LauncherExecutable);

        public static string ActivityRunnerPath { get; } = Path.Combine(ApplicationFolder, ActivityRunnerExecutable);

        public static string LocalesFolder { get; } = Path.Combine(ProgramRoot, "Locales");

        public static string DocumentationFolder { get; } = Path.Combine(ProgramRoot, "Documentation");

        public static string LogFile(string path, string fileNameTemplate) => Path.Combine(path, LoggingUtil.CustomizeLogFileName(fileNameTemplate));

        static RuntimeInfo()
        {
            Directory.CreateDirectory(LogFilesFolder);
        }

        public static string GetCacheFilePath(string cacheType, string key)
        {
            string hash = XxHash64.HashToUInt64(Encoding.Default.GetBytes(key)).ToString("X", CultureInfo.InvariantCulture);

            string directory = Path.Combine(CacheFolder, cacheType);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return Path.Combine(directory, hash + FileNameExtensions.DataFile);
        }

    }
}
