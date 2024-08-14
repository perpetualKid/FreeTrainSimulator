using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    public class ContentRouteHandler : ContentHandlerBase<RouteModel, RouteModelCore>
    {
        public static async ValueTask<RouteModel> Get(string name, FolderModel contentFolder, CancellationToken cancellationToken)
        {
            return await FromFile(name, contentFolder, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<RouteModelCore> GetBase(string name, FolderModel contentFolder, CancellationToken cancellationToken)
        {
            return await FromFile(name, contentFolder, cancellationToken).ConfigureAwait(false);
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

        public static async ValueTask<RouteModel> Convert(string routePath, FolderModel contentFolder, CancellationToken cancellationToken)
        {
            FolderStructure.ContentFolder.RouteFolder routeFolder = FolderStructure.Route(routePath);

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

        public static async ValueTask<FrozenSet<RouteModel>> GetRoutes(FolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(contentFolder);

            string pattern = $"*{ModelFileResolver<RouteModelCore>.FileExtension}.*";
            ConcurrentBag<RouteModel> results = new ConcurrentBag<RouteModel>();

            if (Directory.Exists(routesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(routesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    RouteModel route = await FromFile(file, contentFolder, token, false).ConfigureAwait(false);
                    if (null != route)
                        results.Add(route);
                }).ConfigureAwait(false);
            }

            if (results.IsEmpty)
            {
                ContentFolderResolver resolver = FileResolver.ContentFolderResolver(contentFolder);
                await Parallel.ForEachAsync(Directory.EnumerateDirectories(contentFolder.MstsContentFolder().RoutesFolder), cancellationToken, async (routeDirectory, token) =>
                {
                    RouteModel route = await Convert(routeDirectory, contentFolder, token).ConfigureAwait(false);
                    if (null != route)
                        results.Add(route);
                }).ConfigureAwait(false);
            }

            return results.ToFrozenSet();
        }
    }
}
