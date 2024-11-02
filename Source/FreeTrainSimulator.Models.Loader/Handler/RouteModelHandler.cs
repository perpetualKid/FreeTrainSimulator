using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

using Orts.Formats.Msts.Files;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using System.Diagnostics;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class RouteModelHandler : ContentHandlerBase<RouteModelCore>
    {
        internal const string SourceNameKey = "MstsSourceRoute";

        public static ValueTask<RouteModelCore> GetCore(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return GetCore(routeModel.Id, routeModel.Parent, cancellationToken);
        }

        public static async ValueTask<RouteModelCore> GetCore(string routeId, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy(routeId);

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<RouteModelCore>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<RouteModelCore>>(FromFile(routeId, folderModel, cancellationToken));
                collectionUpdateRequired[folderModel.Hierarchy()] = true;
            }

            RouteModelCore routeModel = await modelTask.Value.ConfigureAwait(false);

            if (routeModel.SetupRequired())
            {
                taskLazyCache[key] = new Lazy<Task<RouteModelCore>>(() => Cast(Convert(routeModel, cancellationToken)));
                collectionUpdateRequired[folderModel.Hierarchy()] = true;
            }

            return routeModel;
        }

        public static ValueTask<RouteModel> GetExtended(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return routeModel is RouteModel routeModelExtended ? ValueTask.FromResult(routeModelExtended) : GetExtended(routeModel.Id, routeModel.Parent, cancellationToken);
        }

        public static async ValueTask<RouteModel> GetExtended(string routeId, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy(routeId);

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<RouteModelCore>> modelTask) || !modelTask.IsValueCreated ||
                (modelTask.IsValueCreated && (modelTask.Value.IsFaulted || (await modelTask.Value.ConfigureAwait(false) is not RouteModel))))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<RouteModelCore>>(Cast(FromFile<RouteModel, FolderModel>(routeId, folderModel, cancellationToken)));
                collectionUpdateRequired[folderModel.Hierarchy()] = true;
            }

            RouteModel routeModel = await modelTask.Value.ConfigureAwait(false) as RouteModel;

            if (routeModel.SetupRequired())
            {
                taskLazyCache[key] = new Lazy<Task<RouteModelCore>>(() => Cast(Convert(routeModel, cancellationToken)));
                collectionUpdateRequired[folderModel.Hierarchy()] = true;
            }

            return routeModel;
        }

        public static async ValueTask<FrozenSet<RouteModelCore>> GetRoutes(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !taskLazyCollectionCache.TryGetValue(key, out Lazy<Task<FrozenSet<RouteModelCore>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<RouteModelCore>>>(() => LoadRoutes(folderModel, cancellationToken));
            }

            return await modelSetTask.Value.ConfigureAwait(false);
        }

        public static async Task<FrozenSet<RouteModelCore>> ExpandRouteModels(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(folderModel);
            string pattern = ModelFileResolver<RouteModelCore>.WildcardPattern;

            ConcurrentBag<RouteModelCore> results = new ConcurrentBag<RouteModelCore>();
            ConcurrentDictionary<string, FolderStructure.ContentFolder.RouteFolder> routeFolders = new ConcurrentDictionary<string, FolderStructure.ContentFolder.RouteFolder>(StringComparer.OrdinalIgnoreCase);

            string sourceFolder = folderModel.MstsContentFolder().RoutesFolder;

            if (Directory.Exists(sourceFolder))
            {
                // preload existing MSTS folders
                foreach(string routeFolder in Directory.EnumerateDirectories(sourceFolder))
                {
                    FolderStructure.ContentFolder.RouteFolder folder = FolderStructure.Route(routeFolder);
                    if (folder.Valid)
                        _ = routeFolders.TryAdd(folder.RouteName, folder);
                }

                //load existing route models, and compare if the corresponding folder still exists.
                if (Directory.Exists(routesFolder))
                {
                    FrozenSet<RouteModelCore> existingRoutes = await GetRoutes(folderModel, cancellationToken).ConfigureAwait(false);
                    foreach (RouteModelCore route in existingRoutes)
                    {
                        if (routeFolders.TryRemove(route.Tags[SourceNameKey], out FolderStructure.ContentFolder.RouteFolder routeFolder))
                        {
                            results.Add(route);
                        }
                    }
                }

                //for any new MSTS folder (remaining in the preloaded dictionary), Create a route model
                await Parallel.ForEachAsync(routeFolders, cancellationToken, async (routeFolder, token) =>
                {
                    Lazy<Task<RouteModelCore>> modelTask = new Lazy<Task<RouteModelCore>>(Cast(Convert(routeFolder.Value, folderModel, cancellationToken)));
                    RouteModelCore routeModel = await modelTask.Value.ConfigureAwait(false);
                    string key = routeModel.Hierarchy();
                    results.Add(routeModel);
                    taskLazyCache[key] = modelTask;
                }).ConfigureAwait(false);
            }

            FrozenSet<RouteModelCore> result = results.ToFrozenSet();
            string key = folderModel.Hierarchy();
            Lazy<Task<FrozenSet<RouteModelCore>>> modelSetTask;
            taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<RouteModelCore>>>(Task.FromResult(result));
            return result;
        }

        private static async Task<FrozenSet<RouteModelCore>> LoadRoutes(FolderModel folderModel, CancellationToken cancellationToken)
        {
            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(folderModel);
            string pattern = ModelFileResolver<RouteModelCore>.WildcardSavePattern;

            ConcurrentBag<RouteModelCore> results = new ConcurrentBag<RouteModelCore>();

            //load existing route models, and compare if the corresponding folder still exists.
            if (Directory.Exists(routesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(routesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string routeId = Path.GetFileNameWithoutExtension(file);

                    if (routeId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        routeId = routeId[..^fileExtension.Length];

                    RouteModelCore route = await GetCore(routeId, folderModel, token).ConfigureAwait(false);
                    if (null != route)
                        results.Add(route);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }

        private static Task<RouteModel> Convert(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return Convert(routeModel.MstsRouteFolder(), routeModel.Parent, cancellationToken);
        }

        private static async Task<RouteModel> Convert(FolderStructure.ContentFolder.RouteFolder routeFolder, FolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeFolder, nameof(routeFolder));

            if (routeFolder.Valid)
            {
                string routeFileName = routeFolder.TrackFileName;
                RouteFile routeFile = new RouteFile(routeFileName);
                Route route = routeFile.Route;

                // these setting should be used as route specific overrides to standard values or user settings
                Dictionary<string, string> settings = new Dictionary<string, string>()
                {
                    { "SingleTunnelArea",$"{route.SingleTunnelAreaM2}" }, // values for tunnel operation
                    { "SingleTunnelPerimeter",$"{route.SingleTunnelPerimeterM}" }, // values for tunnel operation
                    { "DoubleTunnelArea",$"{route.DoubleTunnelAreaM2}" }, // values for tunnel operation
                    { "DoubleTunnelPerimeter",$"{route.DoubleTunnelPerimeterM}" }, // values for tunnel operation
                    { "ForestClearDistance",$"{route.ForestClearDistance}" },   // if > 0 indicates distance from track without forest trees
                    { "RemoveForestTreesFromRoads",$"{route.RemoveForestTreesFromRoads}" }, // if true removes forest trees also from roads
                    { "OpenComputerTrainDoors",$"{route.OpenDoorsInAITrains}" },
                    { "CurveSound",$"{route.CurveSMSNumber}" },
                    { "CurveSwitchSound",$"{route.CurveSwitchSMSNumber}" },
                    { "SwitchSound",$"{route.SwitchSMSNumber}" },
                };

                RouteModel routeModel = new RouteModel(route.RouteStart.Location)
                {
                    Name = route.Name,
                    Description = route.Description,
                    MetricUnits = route.MilepostUnitsMetric,
                    Id = route.RouteID,    // ie JAPAN1  - used for TRK file and route folder name
                    Tags = new Dictionary<string, string> { { SourceNameKey, routeFolder.RouteName } },    //store the route folder name
                    EnvironmentConditions = new EnumArray2D<string, SeasonType, WeatherType>(route.Environment.GetEnvironmentFileName),
                    RouteKey = route.FileName,  // ie OdakyuSE - used for MKR,RDB,REF,RIT,TDB,TIT
                    RouteSounds = new EnumArray<string, DefaultSoundType>(new string[]
                    {
                        /// elements need to be in same order as listed in <see cref="DefaultSoundType"/>
                        route.DefaultSignalSMS, route.DefaultCrossingSMS, route.DefaultWaterTowerSMS, route.DefaultCoalTowerSMS, route.DefaultDieselTowerSMS, route.DefaultTurntableSMS,
                    }),
                    Graphics = new EnumArray<string, GraphicType>(new string[]
                    {
                        /// elements need to be in same order as listed in <see cref="GraphicType"/>
                        route.Thumbnail, route.LoadingScreen, route.LoadingScreenWide,
                    }),
                    RouteConditions = new RouteConditionModel()
                    {
                        Electrified = route.Electrified,
                        MaxLineVoltage = route.MaxLineVoltage,
                        OverheadWireHeight = route.OverheadWireHeight,
                        DoubleWireEnabled = string.Equals(route.DoubleWireEnabled, "On", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(route.DoubleWireEnabled, "Enabled", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(route.DoubleWireEnabled, "Yes", StringComparison.OrdinalIgnoreCase) ||
                            (bool.TryParse(route.DoubleWireEnabled, out bool doubleWireEnabled) && doubleWireEnabled),
                        DoubleWireHeight = route.DoubleWireHeight,
                        TriphaseEnabled = string.Equals(route.TriphaseEnabled, "On", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(route.TriphaseEnabled, "Enabled", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(route.TriphaseEnabled, "Yes", StringComparison.OrdinalIgnoreCase) ||
                            (bool.TryParse(route.TriphaseEnabled, out bool triphaseEnabled) && triphaseEnabled),
                        TriphaseWidth = route.TriphaseWidth,
                    },
                    SpeedRestrictions = new EnumArray<float, SpeedRestrictionType>(new float[]
                    {
                        /// elements need to be in same order as listed in <see cref="SpeedRestrictionType"/>
                        route.SpeedLimit, route.TempRestrictedSpeed
                    }),
                    Settings = settings.ToFrozenDictionary(),
                    SuperElevationRadiusSettings = route.SuperElevationHgtpRadiusM,
                };
                await Create(routeModel, contentFolder, true, true, cancellationToken).ConfigureAwait(false);
                return routeModel;
            }
            else
            {
                Trace.TraceWarning($"Route folder {routeFolder.RouteName} refers to non-existing route.");
                return null;
            }
        }
    }
}