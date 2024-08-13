using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public class ContentRouteHeaderHandler : ContentHandlerBase<RouteModelHeader, RouteModelCore>
    {
        public static async ValueTask<RouteModelHeader> Get(string name, FolderModel contentFolder, CancellationToken cancellationToken)
        {
            return await FromFile(name, contentFolder, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<FrozenSet<RouteModelHeader>> GetRoutes(FolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(contentFolder);

            string pattern = ModelFileResolver<RouteModelCore>.WildcardPattern;

            ConcurrentBag<RouteModelHeader> results = new ConcurrentBag<RouteModelHeader>();

            if (Directory.Exists(routesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(routesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    RouteModelHeader route = await FromFile(file, contentFolder, token, false).ConfigureAwait(false);
                    if (null != route)
                        results.Add(route);
                }).ConfigureAwait(false);
            }

            if (results.IsEmpty)
            {
                ContentFolderResolver resolver = FileResolver.ContentFolderResolver(contentFolder);
                await Parallel.ForEachAsync(Directory.EnumerateDirectories(contentFolder.MstsContentFolder().RoutesFolder), cancellationToken, async (routeDirectory, token) =>
                {
                    RouteModel route = await ContentRouteHandler.Convert(routeDirectory, contentFolder, token).ConfigureAwait(false);
                    if (null != route)
                    {
                        RouteModelHeader header = new RouteModelHeader(route);
                        header.Initialize((route as IFileResolve).FilePath, contentFolder);
                        results.Add(header);
                    }
                }).ConfigureAwait(false);
            }

            return results.ToFrozenSet();
        }
    }
}
