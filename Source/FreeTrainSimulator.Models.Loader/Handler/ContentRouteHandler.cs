using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

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

                RouteModel routeModel = new RouteModel(routeFile.Route.RouteStart.Location)
                {
                    Name = routeFile.Route.Name,
                    Description = routeFile.Route.Description,
                    MetricUnits = routeFile.Route.MilepostUnitsMetric,
                    RouteId = routeFile.Route.RouteID,
                    Tag = routeFolder.RouteName,    //store the route folder name
                    EnvironmentConditions = new EnumArray2D<string, SeasonType, WeatherType>(routeFile.Route.Environment.GetEnvironmentFileName),
                    RouteKey = routeFile.Route.FileName,
                };
                await Create(routeModel, contentFolder, true, true, cancellationToken).ConfigureAwait(false);
                return routeModel;
            }
            return null;
        }
    }
}
