using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent;
using FreeTrainSimulator.Models.Independent.Environment;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

using static Orts.Formats.Msts.FolderStructure;
using static Orts.Formats.Msts.FolderStructure.ContentFolder;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public class RouteLoader : LoaderBase
    {
        public static async ValueTask<RouteModel> LoadRoute(string routePath, CancellationToken cancellationToken)
        {
            string routeName = Path.GetFileName(routePath);

            string routeModelFile = Path.Combine(routePath, routeName + ".route");
            RouteModel route = await ModelBase.FromFile<RouteModel>(routeModelFile, cancellationToken).ConfigureAwait(false);

            if (route == null)
            {
                RouteFolder routeFolder = FolderStructure.Route(routePath);

                string trkFilePath = routeFolder.TrackFileName;
                RouteFile routeFile = new RouteFile(trkFilePath);
                route = routeFile.Route.RouteData;

                await ModelBase.ToFile(routeModelFile, route, cancellationToken).ConfigureAwait(false);
            }

            return route;
        }

        public static async ValueTask<FrozenSet<RouteModel>> GetRoutes(ContentFolder contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            ConcurrentBag<RouteModel> results = new ConcurrentBag<RouteModel>();

            await Parallel.ForEachAsync(Directory.EnumerateDirectories(contentFolder.RoutesFolder), async (routeDirectory, cancellationToken) =>
            {
                RouteModel route = await LoadRoute(routeDirectory, cancellationToken).ConfigureAwait(false);
                if (null != route)
                    results.Add(route);
            }).ConfigureAwait(false);

            return results.ToFrozenSet();
        }

    }
}
