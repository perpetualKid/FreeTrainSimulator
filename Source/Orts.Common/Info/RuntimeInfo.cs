using System;
using System.IO;

namespace Orts.Common.Info
{
    public static class RunTimeInfo
    {
        public const string ProductName = "Open Rails Ultimate";

        public static string ApplicationFolder { get; } = AppContext.BaseDirectory;

        public static string ConfigFolder { get; } = Path.Combine(ApplicationFolder, ".config");

        public static string UserDataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductName);

        static RunTimeInfo()
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
