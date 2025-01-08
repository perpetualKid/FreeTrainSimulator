
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Win32;

namespace Orts.Formats.Msts
{
    public static class FolderStructure
    {
        public const string OpenRailsSpecificFolder = "OpenRails";
        private const string Global = "Global";

#pragma warning disable CA1034 // Nested types should not be visible
        public class ContentFolder
        {
            #region RouteFolder
            public class RouteFolder
            {
#pragma warning restore CA1034 // Nested types should not be visible

                private const string tsection = "tsection.dat";

                internal RouteFolder(string route, ContentFolder parent)
                {
                    RouteName = route;
                    this.ContentFolder = parent;
                    CurrentFolder = Path.Combine(parent.RoutesFolder, RouteName);
                }

                public bool Valid => !string.IsNullOrEmpty(TrackFileName);

                public string RouteName { get; }

                #region Folders
                public string CurrentFolder { get; }

                public ContentFolder ContentFolder { get; }

                public string TrackFileName => Directory.EnumerateFiles(CurrentFolder, "*.trk").FirstOrDefault();

                public string ActivitiesFolder => Path.Combine(CurrentFolder, "Activities");

                public string OpenRailsActivitiesFolder => Path.Combine(ActivitiesFolder, OpenRailsSpecificFolder);

                public string OpenRailsRouteFolder => Path.Combine(CurrentFolder, OpenRailsSpecificFolder);

                public string EnvironmentTexturesFolder => Path.Combine(CurrentFolder, "EnvFiles", "Textures");

                public string EnvironmentFolder => Path.Combine(CurrentFolder, "EnvFiles");

                public string PathsFolder => Path.Combine(CurrentFolder, "Paths");

                public string ServicesFolder => Path.Combine(CurrentFolder, "Services");

                public string ShapesFolder => Path.Combine(CurrentFolder, "Shapes");

                public string SoundFolder => Path.Combine(CurrentFolder, "Sound");

                public string TexturesFolder => Path.Combine(CurrentFolder, "Textures");

                public string TerrainTexturesFolder => Path.Combine(CurrentFolder, "Terrtex");

                public string TilesFolder => Path.Combine(CurrentFolder, "Tiles");

                public string TilesFolderLow => Path.Combine(CurrentFolder, "Lo_Tiles");

                public string TrafficFolder => Path.Combine(CurrentFolder, "Traffic");

                public string WeatherFolder => Path.Combine(CurrentFolder, "WeatherFiles");

                public string WorldFolder => Path.Combine(CurrentFolder, "World");
                #endregion

                #region Files
                public string TrackDatabaseFile(string routeFileName)
                {
                    if (string.IsNullOrEmpty(routeFileName))
                        throw new ArgumentNullException(nameof(routeFileName));

                    return Path.Combine(CurrentFolder, routeFileName + ".tdb");
                }

                public string RoadTrackDatabaseFile(string routeFileName)
                {
                    if (string.IsNullOrEmpty(routeFileName))
                        throw new ArgumentNullException(nameof(routeFileName));

                    return Path.Combine(CurrentFolder, routeFileName + ".rdb");
                }

                public string ServiceFile(string serviceName)
                {
                    return Path.Combine(ServicesFolder, serviceName + ".srv");
                }

                public string PathFile(string pathName)
                {
                    return Path.Combine(PathsFolder, pathName + ".pat");
                }

                public string ActivityFile(string activityName)
                {
                    return Path.Combine(ActivitiesFolder, activityName + ".act");
                }

                public string TrafficFile(string trafficName)
                {
                    return Path.Combine(TrafficFolder, trafficName + ".trf");
                }

                public string SoundFile(string soundFileName)
                {
                    return Path.Combine(SoundFolder, soundFileName);
                }

                public string HazardFile(string hazardName)
                {
                    return Path.Combine(CurrentFolder, hazardName);
                }

                public string ShapeFile(string shapeFileName)
                {
                    return Path.Combine(ShapesFolder, shapeFileName);
                }

                public string EnvironmentTextureFile(string textureFileName)
                {
                    return Path.Combine(EnvironmentTexturesFolder, textureFileName);
                }

                public string TrackSectionFile
                {
                    get 
                    {
                        string tsectionFile;
                        if (File.Exists(tsectionFile = Path.Combine(CurrentFolder, OpenRailsSpecificFolder, tsection)))
                            return tsectionFile;
                        else if (File.Exists(tsectionFile = Path.Combine(CurrentFolder, Global, tsection)))   // doesn't seem to be a valid option, but might have been used so keep for now
                            return tsectionFile;
                        else
                            return Path.Combine(ContentFolder.Folder, Global, tsection);
                    }
                }

                public string RouteTrackSectionFile => Path.Combine(CurrentFolder, tsection);

                public string SignalConfigurationFile
                {
                    get
                    {
                        string signalConfig;
                        if (File.Exists(signalConfig = Path.Combine(CurrentFolder, OpenRailsSpecificFolder, "sigcfg.dat")))
                        {
                            ORSignalConfigFile = true;
                            return signalConfig;
                        }
                        return Path.Combine(CurrentFolder, "sigcfg.dat");
                    }
                }

                public bool ORSignalConfigFile { get; private set; }

                public string CarSpawnerFile => Path.Combine(CurrentFolder, "carspawn.dat");
                public string OpenRailsCarSpawnerFile => Path.Combine(CurrentFolder, OpenRailsSpecificFolder, "carspawn.dat");

                #endregion
            }
            #endregion

            private readonly ConcurrentDictionary<string, RouteFolder> routeFolders = new ConcurrentDictionary<string, RouteFolder>(StringComparer.OrdinalIgnoreCase);

            internal ContentFolder(string root)
            {
                Folder = Path.GetFullPath(root);
            }

            public string Folder { get; }

            public string RoutesFolder => Path.Combine(Folder, "Routes");

            public string SoundFolder => Path.Combine(Folder, "Sound");

            public string TrainsFolder => Path.Combine(Folder, "Trains");

            public string ConsistsFolder => Path.Combine(TrainsFolder, "Consists");

            public string TrainSetsFolder => Path.Combine(TrainsFolder, "TrainSet");

            public string EndOfTrainDevicesFolder => Path.Combine(TrainsFolder, "Orts_Eot");

            public string TexturesFolder => Path.Combine(Folder, Global, "Textures");

            public string ShapesFolder => Path.Combine(Folder, Global, "Shapes");

            public string ConsistFile(string consistName)
            {
                return Path.Combine(ConsistsFolder, consistName + ".con");
            }

            public string EngineFile(string trainSetName, string engineName)
            {
                return Path.Combine(TrainSetsFolder, trainSetName, engineName + ".eng");
            }

            public string SoundFile(string soundName)
            {
                return Path.Combine(SoundFolder, soundName);
            }

            public string ShapeFile(string shapeFile)
            {
                return Path.Combine(ShapesFolder, shapeFile);
            }

            public string TextureFile(string textureFile)
            {
                return Path.Combine(TexturesFolder, textureFile);
            }

            public string WagonFile(string trainSetName, string wagonName)
            {
                return Path.Combine(TrainSetsFolder, trainSetName, wagonName + ".wag");
            }

            public RouteFolder Route(string route)
            {
                if (!routeFolders.TryGetValue(route, out RouteFolder result))
                {
                    routeFolders.TryAdd(route, new RouteFolder(route, this));
                    result = routeFolders[route];
                }
                return result;
            }
        }

        private static readonly string mstsLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Games", "Train Simulator");   // MSTS default path.
        private static readonly ConcurrentDictionary<string, ContentFolder> contentFolders = new ConcurrentDictionary<string, ContentFolder>(StringComparer.OrdinalIgnoreCase);

        public static string MstsFolder
        {
            get
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft Games\Train Simulator\1.0");
                if (key == null)
                    key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Microsoft Games\Train Simulator\1.0");
                DirectoryInfo mstsFolder = new DirectoryInfo((string)key?.GetValue("Path", mstsLocation) ?? mstsLocation);

                // Verify installation at this location
                if (!mstsFolder.Exists)
                    Trace.TraceInformation($"MSTS directory '{mstsLocation}' does not exist.");
                return mstsFolder.FullName;
            }
        }

        public static ContentFolder Content(string root)
        {
            if (!contentFolders.TryGetValue(root, out ContentFolder result))
            {
                contentFolders.TryAdd(root, new ContentFolder(root));
                result = contentFolders[root];
            }
            return result;
        }

        public static ContentFolder.RouteFolder Route(string routePath)
        {
            string routeName = Path.GetFileName(routePath);
            string contentFolder = Path.GetFullPath(Path.Combine(routePath, "..\\.."));
            return Content(contentFolder).Route(routeName);
        }

        public static ContentFolder.RouteFolder RouteFromActivity(string activityPath)
        {
            string traversal = "..\\..";
            if (Path.GetFileName(Path.GetDirectoryName(activityPath)).Equals(OpenRailsSpecificFolder, StringComparison.OrdinalIgnoreCase))
                traversal = "..\\..\\..";
            string routePath = Path.GetFullPath(Path.Combine(activityPath, traversal));
            string routeName = Path.GetFileName(routePath);
            string contentFolder = Path.GetFullPath(Path.Combine(routePath, "..\\.."));
            return Content(contentFolder).Route(routeName);
        }

        //public static string TrackItemTable => Path.Combine(RouteFolder, RouteName + ".TIT");

        ///// <summary>
        ///// Given a soundfile reference in a wag or eng file, return the path the sound file
        ///// </summary>
        ///// <param name="wagfilename"></param>
        ///// <param name="soundfile"></param>
        ///// <returns></returns>
        //public static string TrainSound(string waggonFile, string soundFile)
        //{
        //    string trainsetSoundPath = Path.Combine(Path.GetDirectoryName(waggonFile), "SOUND", soundFile);
        //    string globalSoundPath = Path.Combine(SoundsFolder, soundFile);

        //    return File.Exists(trainsetSoundPath) ? trainsetSoundPath : globalSoundPath;
        //}

        ///// <summary>
        ///// Given a soundfile reference in a cvf file, return the path to the sound file
        ///// </summary>
        //public static string SmsSoundPath(string smsFile, string soundFile)
        //{
        //    string smsSoundPath = Path.Combine(Path.GetDirectoryName(smsFile), soundFile);
        //    string globalSoundPath = Path.Combine(SoundsFolder, soundFile);

        //    return File.Exists(smsSoundPath) ? smsSoundPath : globalSoundPath;
        //}

        /// <summary>
        /// Static variables to reduce occurrence of duplicate warning messages.
        /// </summary>
        private static string badBranch = "";
        private static string badPath = "";
        private static readonly Dictionary<string, StringDictionary> filesFound = new Dictionary<string, StringDictionary>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Search an array of paths for a file. Paths must be in search sequence.
        /// No need for trailing "\" on path or leading "\" on branch parameter.
        /// </summary>
        /// <param name="pathArray">2 or more folders, e.g. "D:\MSTS", E:\OR"</param>
        /// <param name="branch">a filename possibly prefixed by a folder, e.g. "folder\file.ext"</param>
        /// <returns>null or the full file path of the first file found</returns>
        public static string FindFileFromFolders(in ImmutableArray<string> paths, string fileRelative)
        {
            ArgumentNullException.ThrowIfNull(paths);
            if (string.IsNullOrEmpty(fileRelative))
                return string.Empty;

            if (filesFound.TryGetValue(fileRelative, out StringDictionary existingFiles))
            {
                foreach (string path in paths)
                {
                    if (existingFiles.ContainsKey(path))
                        return existingFiles[path];
                }
            }
            foreach (string path in paths)
            {
                string fullPath = Path.Combine(path, fileRelative);
                if (File.Exists(fullPath))
                {
                    if (null != existingFiles)
                        existingFiles.Add(path, fullPath);
                    else
                        filesFound.Add(fileRelative, new StringDictionary
                                {
                                    { path, fullPath }
                                });
                    return fullPath;
                }
            }

            string firstPath = paths.First();
            if (fileRelative != badBranch || firstPath != badPath)
            {
                Trace.TraceWarning("File {0} missing from {1}", fileRelative, firstPath);
                badBranch = fileRelative;
                badPath = firstPath;
            }
            return null;
        }

    }
}
