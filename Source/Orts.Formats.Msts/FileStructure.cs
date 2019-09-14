using System.IO;
using Microsoft.Win32;

namespace Orts.Formats.Msts
{
    public static class FileStructure
    {
        private static readonly string defaultLocation;   // MSTS default path.

        static FileStructure()
        {
            defaultLocation = "c:\\program files\\microsoft games\\train simulator";

            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft Games\Train Simulator\1.0");
            if (key == null)
                key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Microsoft Games\Train Simulator\1.0");
            if (key != null)
                defaultLocation = (string)key.GetValue("Path", defaultLocation);

            // Verify installation at this location
            if (!Directory.Exists(defaultLocation))
                throw new FileNotFoundException("MSTS directory '" + defaultLocation + "' does not exist.", defaultLocation);
        }

        /// <summary>
        /// Returns the base path of the MSTS installation
        /// </summary>
        /// <returns>no trailing \</returns>
        public static string Base()
        { 
            return defaultLocation;
        }

        /// <summary>
        /// Returns the route folder with out trailing \
        /// </summary>
        /// <param name="route"></param>
        /// <returns></returns>
        public static string RouteFolder(string route)
        {
            return Path.Combine(Base(), "ROUTES", route);
        }

        public static string ConsistFolder()
        {
            return Path.Combine(Base(), "TRAINS", "CONSISTS");
        }

        public static string TrainsetFolder()
        {
            return Path.Combine(Base(), "TRAINS", "TRAINSET");
        }

        public static string GlobalSoundFolder()
        {
            return Path.Combine(Base(), "SOUND");
        }

        public static string ActivityFolder(string routeFolderName)
        {
            return Path.Combine(RouteFolder(routeFolderName), "ACTIVITIES");
        }

        public static string TrackFileName(string routeFolderPath)
        {
            var trkFileNames = Directory.GetFiles(routeFolderPath, "*.trk");
            if (trkFileNames.Length == 0)
                throw new FileNotFoundException("TRK file not found in '" + routeFolderPath + "'.", Path.Combine(routeFolderPath, Path.GetFileName(routeFolderPath)));
            return trkFileNames[0];
        }

        /// <summary>
        /// Given a soundfile reference in a wag or eng file, return the path the sound file
        /// </summary>
        /// <param name="wagfilename"></param>
        /// <param name="soundfile"></param>
        /// <returns></returns>
        public static string TrainSoundPath(string wagfilename, string soundfile)
        {
            string trainsetSoundPath = Path.Combine(Path.GetDirectoryName(wagfilename), "SOUND", soundfile);
            string globalSoundPath = Path.Combine(GlobalSoundFolder(), soundfile);

            return File.Exists(trainsetSoundPath) ? trainsetSoundPath : globalSoundPath;
        }

        /// <summary>
        /// Given a soundfile reference in a cvf file, return the path to the sound file
        /// </summary>
        public static string SmsSoundPath(string smsfilename, string soundfile)
        {
            string smsSoundPath = Path.Combine(Path.GetDirectoryName(smsfilename), soundfile);
            string globalSoundPath = Path.Combine(GlobalSoundFolder(), soundfile);

            return File.Exists(smsSoundPath) ? smsSoundPath : globalSoundPath;
        }

        public static string TitFilePath(string route)
        {
            return Path.Combine(RouteFolder(route), route + ".TIT");
        }

        public static string ConsistPath(string consistName)
        {
            return Path.Combine(Base(), "TRAINS", "CONSISTS", consistName + ".con");
        }

        public static string ServicesPath(string serviceName, string routeFolderPath)
        {
            return Path.Combine(routeFolderPath, "SERVICES", serviceName + ".srv");
        }

        public static string TrafficPath(string trafficName, string routeFolderPath)
        {
            return Path.Combine(routeFolderPath, "TRAFFIC", trafficName, ".trf");
        }

        public static string PathPath(string pathName, string routeFolderPath)
        {
            return Path.Combine(routeFolderPath, "PATHS", pathName + ".pat");
        }

        public static string WaggonPath(string waggonName, string folder)
        {
            return Path.Combine(TrainsetFolder(), folder, waggonName + ".wag");
        }

        public static string EnginePath(string engineName, string folder)
        {
            return Path.Combine(TrainsetFolder(), folder, engineName + ".eng");
        }

    }
}
