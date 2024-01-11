using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Hashing;
using System.Reflection;
using System.Text;

using Orts.Common.Logging;

namespace Orts.Common.Info
{
    public static class RuntimeInfo
    {
        public const string LauncherExecutable = "FreeTrainSimulator.exe";

        public const string ActivityRunnerExecutable = "ActivityRunner.exe";

        public static readonly string ProductName = VersionInfo.ProductName();

        public const string WikiLink = "https://github.com/perpetualKid/ORTS-MG/wiki";

        public static string ApplicationName => FileVersionInfo.GetVersionInfo(Assembly.GetCallingAssembly().Location).FileDescription;

        public static string ApplicationFile => Path.GetFileName(Assembly.GetCallingAssembly().Location);
        /// <summary>
        /// returns the current application base directory, i.e. Program\netcoreapp3.1
        /// </summary>
        public static string ApplicationFolder { get; } = AppContext.BaseDirectory;

        /// <summary>
        /// returns the .config directory in the <see cref="ApplicationFolder"/>
        /// </summary>
        public static string ConfigFolder { get; } = Path.Combine(ApplicationFolder, ".config");

        public static string ContentFolder { get; } = Path.Combine(ApplicationFolder, "content");

        public static string UserDataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductName);

        /// <summary>
        /// returns the common program root. While this may be same as <see cref="ApplicationFolder"/>
        /// this is one level up in dual target environment ("Program" for "Program\netcoreapp3.1")
        /// </summary>
        public static string ProgramRoot { get; } = Path.GetFullPath(Path.Combine(ApplicationFolder, ".."));

        public static string LauncherPath { get; } = Path.Combine(ProgramRoot, LauncherExecutable);

        public static string ActivityRunnerPath { get; } = Path.Combine(ApplicationFolder, ActivityRunnerExecutable);

        public static string LocalesFolder { get; } = Path.Combine(ProgramRoot, "Locales");

        public static string DocumentationFolder { get; } = Path.Combine(ProgramRoot, "Documentation");

        public static string LogFile(string path, string fileNamePattern) => Path.Combine(path, LoggingUtil.CustomizeLogFileName(fileNamePattern));

        static RuntimeInfo()
        {
            try
            {
                Directory.CreateDirectory(ConfigFolder);
            }
            catch (Exception exception) when
                (exception is IOException || exception is UnauthorizedAccessException)
            {
                //we may not be able to write directly to the current appliction folder (self-contained) so we rather use user appdata folder
                ConfigFolder = Path.Combine(UserDataFolder, ".config");
                Directory.CreateDirectory(ConfigFolder);
            }
        }

        public static string GetCacheFilePath(string cacheType, string key)
        {
            string hash = XxHash64.HashToUInt64(Encoding.Default.GetBytes(key)).ToString("X", CultureInfo.InvariantCulture);

            string directory = Path.Combine(UserDataFolder, "Cache", cacheType);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return Path.Combine(directory, hash + ".dat");
        }

    }
}
