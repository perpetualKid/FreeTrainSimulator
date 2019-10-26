using System;
using System.Diagnostics;
using System.IO;

using Microsoft.Win32;
using Orts.Common.IO;

namespace Orts.Formats.Msts
{
    public static class FolderStructure
    {
        private static readonly string mstsLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), 
            "Microsoft Games", "Train Simulator");   // MSTS default path.

        private static DirectoryInfo rootFolder;
        private static DirectoryInfo routeFolder;

        static FolderStructure()
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft Games\Train Simulator\1.0");
            if (key == null)
                key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Microsoft Games\Train Simulator\1.0");
            rootFolder = new DirectoryInfo((string)key?.GetValue("Path", mstsLocation) ?? mstsLocation);

            // Verify installation at this location
            if (!rootFolder?.Exists ?? true)
                Trace.TraceInformation($"MSTS directory '{mstsLocation}' does not exist.");
        }

        public static void InitializeFromPathOrActivity(string path)
        {
            if (Path.GetExtension(path).Equals(".act", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".pat", StringComparison.OrdinalIgnoreCase))
            {
                rootFolder = new DirectoryInfo(Path.GetFullPath(Path.Combine(path, "..", "..", "..", "..")));
                routeFolder = new DirectoryInfo(Path.GetFullPath(Path.Combine(path, "..", "..")));
            }
            else
                throw new FileNotFoundException($"Could not parse root directory for {path}");
            FileSystemCache.Initialize(rootFolder);
        }

        public static void InitializeFromRoot(string path)
        {
            rootFolder = new DirectoryInfo(path);
            FileSystemCache.Initialize(rootFolder);
        }

        public static void InitializeFromRoute(string path)
        {
            routeFolder = new DirectoryInfo(path);
            rootFolder = routeFolder.Parent.Parent;
            FileSystemCache.Initialize(rootFolder);
        }

        /// <summary>
        /// returns the root folder of content set (such as Train Simulator default routes, or other Mini-Route folders
        /// </summary>
        public static string RootFolder => rootFolder.FullName;

        public static string RouteFolder => routeFolder.FullName;

        public static string RouteName => routeFolder.Name;

        public static string RoutesFolder => Path.Combine(RootFolder, "ROUTES");

        public static string ServicesFolder => Path.Combine(RouteFolder, "SERVICES");

        public static string ConsistsFolder => Path.Combine(RootFolder, "TRAINS", "Consists");

        public static string TrainSetsFolder => Path.Combine(RootFolder, "TRAINS", "TrainSet");

        public static string SoundsFolder => Path.Combine(RootFolder, "SOUND");

        public static string RouteSoundsFolder => Path.Combine(RouteFolder, "SOUND");

        public static string ActivitiesFolder => Path.Combine(RouteFolder, "ACTIVITIES");

        public static string TrackFile
        {
            get
            {
                var trackFiles = routeFolder.GetFiles("*.trk");
                if (trackFiles.Length == 0)
                    throw new FileNotFoundException($"TRK file not found in '{RouteFolder}'.", RouteName);
                return trackFiles[0].FullName;
            }
        }

        public static string TrackItemTable => Path.Combine(RouteFolder, RouteName + ".TIT");

        public static string ConsistFile(string consistName)
        {
            return Path.Combine(ConsistsFolder, consistName + ".con");
        }

        public static string ServiceFile(string serviceName)
        {
            return Path.Combine(ServicesFolder, serviceName + ".srv");
        }

        public static string TrafficFile(string trafficName)
        {
            return Path.Combine(RouteFolder, "TRAFFIC", trafficName + ".trf");
        }

        public static string PathFile(string pathName)
        {
            return Path.Combine(RouteFolder, "PATHS", pathName + ".pat");
        }

        public static string WaggonFile(string waggonName, string trainsetName)
        {
            return Path.Combine(TrainSetsFolder, trainsetName, waggonName + ".wag");
        }

        public static string EngineFile(string engineName, string trainsetName)
        {
            return Path.Combine(TrainSetsFolder, trainsetName, engineName + ".eng");
        }

        /// <summary>
        /// Given a soundfile reference in a wag or eng file, return the path the sound file
        /// </summary>
        /// <param name="wagfilename"></param>
        /// <param name="soundfile"></param>
        /// <returns></returns>
        public static string TrainSound(string waggonFile, string soundFile)
        {
            string trainsetSoundPath = Path.Combine(Path.GetDirectoryName(waggonFile), "SOUND", soundFile);
            string globalSoundPath = Path.Combine(SoundsFolder, soundFile);

            return FileSystemCache.FileExists(trainsetSoundPath) ? trainsetSoundPath : globalSoundPath;
        }

        /// <summary>
        /// Given a soundfile reference in a cvf file, return the path to the sound file
        /// </summary>
        public static string SmsSoundPath(string smsFile, string soundFile)
        {
            string smsSoundPath = Path.Combine(Path.GetDirectoryName(smsFile), soundFile);
            string globalSoundPath = Path.Combine(SoundsFolder, soundFile);

            return FileSystemCache.FileExists(smsSoundPath) ? smsSoundPath : globalSoundPath;
        }

        public static string TrackFileName(string routeFolderPath)
        {
            var trkFileNames = Directory.GetFiles(routeFolderPath, "*.trk");
            if (trkFileNames.Length == 0)
                throw new FileNotFoundException("TRK file not found in '" + routeFolderPath + "'.", Path.Combine(routeFolderPath, Path.GetFileName(routeFolderPath)));
            return trkFileNames[0];
        }

    }
}
