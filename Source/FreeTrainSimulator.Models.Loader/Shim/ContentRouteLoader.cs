using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public class ContentRouteLoader : LoaderBase<ContentRouteModel>
    {
        public static async ValueTask<ContentRouteModel> LoadRoute(string routePath, CancellationToken cancellationToken)
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

        //public static async ValueTask<ContentRouteModel> LoadRoute(string routeName, ContentFolderModel contentFolder, CancellationToken cancellationToken)
        //{
        //    string routeName = Path.GetFileName(routePath);

        //    string routeModelFile = Path.Combine(routePath, routeName + ".route");
        //    return await FromFile<ContentRouteModel>(routeModelFile, cancellationToken).ConfigureAwait(false);

        //    if (route == null)
        //    {
        //        FolderStructure.ContentFolder.RouteFolder routeFolder = FolderStructure.Route(routePath);

        //        if (routeFolder.Valid)
        //        {
        //            string trkFilePath = routeFolder.TrackFileName;
        //            RouteFile routeFile = new RouteFile(trkFilePath);
        //            route = routeFile.Route.RouteData;

        //            await ToFile(routeModelFile, route, cancellationToken).ConfigureAwait(false);
        //        }
        //    }
        //    if (route != null)
        //    {
        //        route.Path = routePath;
        //    }
        //    return route;

        //}

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

            ContentFolderResolver resolver = FileResolver.ContentFolderResolver(contentFolder);
            ConcurrentBag<ContentRouteModel> results = new ConcurrentBag<ContentRouteModel>();

            string directory = contentFolder.FilePath;
            if (!Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.Message);
                    throw;
                }
            }

            await Parallel.ForEachAsync(Directory.EnumerateDirectories(resolver.MstsContentFolder.RoutesFolder), cancellationToken, async (routeDirectory, token) =>
            {
                ContentRouteModel route = await LoadRoute(routeDirectory, token).ConfigureAwait(false);
                if (null != route)
                    results.Add(route);
            }).ConfigureAwait(false);

            return results.ToFrozenSet();
        }

    }
}
