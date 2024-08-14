using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    public sealed class ContentFolderHandler : ContentHandlerBase<FolderModel, FolderModel>
    {
        public static async ValueTask<FolderModel> Create(string folderName, string repositoryPath, ProfileModel profile, CancellationToken cancellationToken)
        {
            FolderModel contentFolder = new FolderModel(folderName, repositoryPath, profile);
            await Create(contentFolder, profile, false, true, cancellationToken).ConfigureAwait(false);
            return contentFolder;
        }

        public static ValueTask<FolderModel> Get(string folderName, ProfileModel parent, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parent, nameof(parent));

            return ValueTask.FromResult(parent.ContentFolders.Where((folder) => string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault());
        }

        public static async ValueTask<FrozenSet<RouteModelCore>> Convert(FolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(contentFolder);
            string pattern = ModelFileResolver<RouteModelCore>.WildcardPattern;

            ConcurrentBag<RouteModelCore> results = new ConcurrentBag<RouteModelCore>();
            ConcurrentDictionary<string, FolderStructure.ContentFolder.RouteFolder> routeFolders = new ConcurrentDictionary<string, FolderStructure.ContentFolder.RouteFolder>(StringComparer.OrdinalIgnoreCase);

            // preload existing MSTS folders
            await Parallel.ForEachAsync(Directory.EnumerateDirectories(contentFolder.MstsContentFolder().RoutesFolder), cancellationToken, (routeFolder, token) =>
            {
                FolderStructure.ContentFolder.RouteFolder folder = FolderStructure.Route(routeFolder);
                if (folder.Valid)
                {
                    _ = routeFolders.TryAdd(folder.RouteName, folder);
                }
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

            //load existing route models, and compare if the corresponding folder still exists.
            if (Directory.Exists(routesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(routesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    RouteModelCore route = await ContentRouteCoreHandler.FromFile(file, contentFolder, token, false).ConfigureAwait(false);
                    if (route != null && routeFolders.TryRemove(route.Tag, out FolderStructure.ContentFolder.RouteFolder routeFolder)) //
                    {
                        if (route.SetupRequired())
                            route = await ContentRouteHandler.Convert(routeFolder, contentFolder, token).ConfigureAwait(false);
                        results.Add(route);
                    }
                }).ConfigureAwait(false);
            }

            //for any new MSTS folder (remaining in the preploaded dictionary), Create a route model
            await Parallel.ForEachAsync(routeFolders, cancellationToken, async (routeFolder, token) =>
            {
                RouteModel route = await ContentRouteHandler.Convert(routeFolder.Value, contentFolder, token).ConfigureAwait(false);
                if (null != route)
                {
                    RouteModelCore routeModel = route with { };
                    results.Add(routeModel);
                }
            }).ConfigureAwait(false);

            return results.ToFrozenSet();
        }
    }
}
