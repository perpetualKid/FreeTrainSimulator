using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Environment;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public class RouteLoader : LoaderBase
    {
        public static async ValueTask<RouteModel> LoadRoute(string routePath, CancellationToken cancellationToken)
        {
            string routeName = Path.GetFileName(routePath);

            string routeModelFile = Path.Combine(routePath, routeName + ".route");
            RouteModel route = await FromFile<RouteModel>(routeModelFile, cancellationToken).ConfigureAwait(false);

            if (route == null)
            {
                FolderStructure.ContentFolder.RouteFolder routeFolder = FolderStructure.Route(routePath);

                if (routeFolder.Valid)
                {
                    string trkFilePath = routeFolder.TrackFileName;
                    RouteFile routeFile = new RouteFile(trkFilePath);
                    route = routeFile.Route.RouteData;

                    await ToFile("", route, cancellationToken).ConfigureAwait(false);
                }
            }
            if (route != null)
            {
                route.Path = routePath;
            }
            return route;

        }

        public static async ValueTask<FrozenSet<RouteModel>> GetRoutes(FolderStructure.ContentFolder contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            ConcurrentBag<RouteModel> results = new ConcurrentBag<RouteModel>();
            await Parallel.ForEachAsync(Directory.EnumerateDirectories(contentFolder.RoutesFolder), cancellationToken, async (routeDirectory, token) =>
            {
                RouteModel route = await LoadRoute(routeDirectory, token).ConfigureAwait(false);
                if (null != route)
                    results.Add(route);
            }).ConfigureAwait(false);

            return results.ToFrozenSet();
        }

        public static async ValueTask<FrozenSet<RouteModel>> GetRoutes(ContentFolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            ContentFolderResolver resolver = FileResolver.ContentFolderResolver(contentFolder);
            ConcurrentBag<RouteModel> results = new ConcurrentBag<RouteModel>();
            await Parallel.ForEachAsync(Directory.EnumerateDirectories(resolver.MstsContentFolder.RoutesFolder), cancellationToken, async (routeDirectory, token) =>
            {
                RouteModel route = await LoadRoute(routeDirectory, token).ConfigureAwait(false);
                if (null != route)
                    results.Add(route);
            }).ConfigureAwait(false);

            return results.ToFrozenSet();
        }

    }
}
