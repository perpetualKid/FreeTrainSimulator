using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Content;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public class ContentRouteHandler : ContentHandlerBase<ContentRouteModel>
    {
        private static async ValueTask<ContentRouteModel> LoadRoute(string routePath, CancellationToken cancellationToken)
        {
            string routeName = Path.GetFileName(routePath);

            string routeModelFile = Path.Combine(routePath, routeName + ".route");
            ContentRouteModel route = await FromFile(routeModelFile, cancellationToken).ConfigureAwait(false);

            if (route == null)
            {
                FolderStructure.ContentFolder.RouteFolder routeFolder = FolderStructure.Route(routePath);

                if (routeFolder.Valid)
                {
                    string trkFilePath = routeFolder.TrackFileName;
                    RouteFile routeFile = new RouteFile(trkFilePath);
                    route = routeFile.Route.RouteData;

                    await ToFile(routeModelFile, route, cancellationToken).ConfigureAwait(false);
                }
            }
            if (route != null)
            {
                route.Path = routePath;
            }
            return route;

        }

        public static async ValueTask<ContentRouteModel> Create(string routePath, ContentFolderModel contentFolder, CancellationToken cancellationToken)
        {
            FolderStructure.ContentFolder.RouteFolder routeFolder = FolderStructure.Route(routePath);

            if (routeFolder.Valid)
            {
                string trkFilePath = routeFolder.TrackFileName;
                RouteFile routeFile = new RouteFile(trkFilePath);

                ContentRouteModel routeModel = new ContentRouteModel(routeFile.Route.RouteStart.Location)
                {
                    Name = routeFile.Route.Name,
                    Description = routeFile.Route.Description,
                    MetricUnits = routeFile.Route.MilepostUnitsMetric,
                    RouteId = routeFile.Route.RouteID,
                    EnvironmentConditions = new EnumArray2D<string, SeasonType, WeatherType>(routeFile.Route.Environment.GetEnvironmentFileName)
                };
                routeModel.Initialize(ModelFileResolver<ContentRouteModel>.FilePath(routeModel, contentFolder), contentFolder);
                await ToFile(routeModel, cancellationToken).ConfigureAwait(false);
                return routeModel;
            }
            return null;
        }

        public static async ValueTask<FrozenSet<ContentRouteModel>> GetRoutes(FolderStructure.ContentFolder contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            ConcurrentBag<ContentRouteModel> results = new ConcurrentBag<ContentRouteModel>();
            await Parallel.ForEachAsync(Directory.EnumerateDirectories(contentFolder.RoutesFolder), cancellationToken, async (routeDirectory, token) =>
            {
                ContentRouteModel route = await LoadRoute(routeDirectory, token).ConfigureAwait(false);
                if (null != route)
                    results.Add(route);
            }).ConfigureAwait(false);

            return results.ToFrozenSet();
        }

        public static async ValueTask<FrozenSet<ContentRouteModel>> GetRoutes(ContentFolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            string routesFolder = ModelFileResolver<ContentFolderModel>.FolderPath(contentFolder);

            string pattern = $"*{ModelFileResolver<ContentRouteModel>.FileExtension}.*";
            ConcurrentBag<ContentRouteModel> results = new ConcurrentBag<ContentRouteModel>();

            await Parallel.ForEachAsync(Directory.EnumerateFiles(routesFolder, pattern), cancellationToken, async (file, token) =>
            {
                ContentRouteModel route = await FromFile(file, contentFolder, token, false).ConfigureAwait(false);
                if (null != route)
                    results.Add(route);
            }).ConfigureAwait(false);

            if (results.IsEmpty)
            {
                ContentFolderResolver resolver = FileResolver.ContentFolderResolver(contentFolder);
                await Parallel.ForEachAsync(Directory.EnumerateDirectories(resolver.MstsContentFolder.RoutesFolder), cancellationToken, async (routeDirectory, token) =>
                {
                    ContentRouteModel route = await Create(routeDirectory, contentFolder, token).ConfigureAwait(false);
                    if (null != route)
                        results.Add(route);
                }).ConfigureAwait(false);
            }

            return results.ToFrozenSet();
        }

    }
}
