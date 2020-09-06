using System;
using System.IO;

namespace Orts.Common.Info
{
    public static class RuntimeInfo
    {
        public const string LauncherExecutable = "openrails.exe";

        public const string ActivityRunnerExecutable = "activityrunner.exe";

        public static readonly string ProductName = VersionInfo.ProductName();

        /// <summary>
        /// returns the current application base directory, i.e. Program\netcoreapp3.1
        /// </summary>
        public static string ApplicationFolder { get; } = AppContext.BaseDirectory;

        /// <summary>
        /// returns the .config directory in the <see cref="ApplicationFolder"/>
        /// </summary>
        public static string ConfigFolder { get; } = Path.Combine(ApplicationFolder, ".config");

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

        static RuntimeInfo()
        {
            try
            {
                Directory.CreateDirectory(ConfigFolder);
            }
            catch(Exception exception) when
                (exception is IOException || exception is UnauthorizedAccessException)
            {
                //we may not be able to write directly to the current appliction folder (self-contained) so we rather use user appdata folder
                ConfigFolder = Path.Combine(UserDataFolder, ".config");
                Directory.CreateDirectory(ConfigFolder);
            }
        }
    }
}
