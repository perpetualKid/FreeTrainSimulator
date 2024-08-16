using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal class ContentRouteHandler : ContentHandlerBase<RouteModel, RouteModelCore>
    {
        public static async ValueTask<RouteModel> Get(string name, FolderModel contentFolder, CancellationToken cancellationToken)
        {
            return await FromFile(name, contentFolder, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<RouteModel> Get(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return await FromFile(routeModel.Name, (routeModel as IFileResolve).Container as FolderModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<RouteModel> Convert(FolderStructure.ContentFolder.RouteFolder routeFolder, FolderModel contentFolder, CancellationToken cancellationToken)
        {
            if (routeFolder.Valid)
            {
                string trkFilePath = routeFolder.TrackFileName;
                RouteFile routeFile = new RouteFile(trkFilePath);
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
                    RouteId = route.RouteID,    // ie JAPAN1  - used for TRK file and route folder name
                    Tag = routeFolder.RouteName,    //store the route folder name
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
            return null;
        }
    }
}
