using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class FolderModelHandler : ContentHandlerBase<FolderModel, FolderModel>
    {
        private static readonly ConcurrentDictionary<string, Task<FolderModel>> modelCache = new ConcurrentDictionary<string, Task<FolderModel>>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Lazy<Task<FolderModel>>> modelConvertCache = new ConcurrentDictionary<string, Lazy<Task<FolderModel>>>(StringComparer.OrdinalIgnoreCase);

        public static async ValueTask<FolderModel> Create(string folderName, string repositoryPath, ProfileModel profile, CancellationToken cancellationToken)
        {
            FolderModel contentFolder = new FolderModel(folderName, repositoryPath, profile);
            await Create(contentFolder, profile, false, true, cancellationToken).ConfigureAwait(false);
            return contentFolder;
        }

        public static ValueTask<FolderModel> Get(string folderName, ProfileModel profileModel, CancellationToken _)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            return ValueTask.FromResult(profileModel.ContentFolders.Where((folder) => string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault());
        }

        public static async ValueTask<FolderModel> Get(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            string key = folderModel.Hierarchy();
            Task<FolderModel> fromCache = GetCachedTask(modelCache, key, () => LoadInternal(folderModel, cancellationToken));

            return await fromCache.ConfigureAwait(false);
        }

        public static async ValueTask<FolderModel> Converted(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            string key = folderModel.Hierarchy();

            if (!modelConvertCache.TryGetValue(key, out Lazy<Task<FolderModel>> folderModelTask))
            {
                _ = modelConvertCache.TryAdd(key, folderModelTask = new Lazy<Task<FolderModel>>(() => LoadInternal(folderModel, cancellationToken)));
            }
            if (folderModelTask.Value.IsFaulted)
                modelConvertCache[key] = folderModelTask = new Lazy<Task<FolderModel>>(() => LoadInternal(folderModel, cancellationToken));

            return await folderModelTask.Value.ConfigureAwait(false);
        }

        public static async ValueTask<FolderModel> Load(FolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            contentFolder.SetRoutes(await RouteModelHandler.GetRoutes(contentFolder, cancellationToken).ConfigureAwait(false));
            IFileResolve parent = (contentFolder as IFileResolve).Container;
            contentFolder.Initialize(ModelFileResolver<FolderModel>.FilePath(contentFolder, parent), parent);
            contentFolder.RefreshModel();
            return contentFolder;
        }

        private static Task<FolderModel> LoadInternal(FolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            IFileResolve parent = (contentFolder as IFileResolve).Container;
            contentFolder.Initialize(ModelFileResolver<FolderModel>.FilePath(contentFolder, parent), parent);
            contentFolder.RefreshModel();
            modelConvertCache.TryAdd(ModelFileResolver<FolderModel>.FilePath(contentFolder), new Lazy<Task<FolderModel>>(() => ConvertInternal(contentFolder, cancellationToken)));
            return Task.FromResult(contentFolder);
        }

        private static async Task<FolderModel> ConvertInternal(FolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(contentFolder);
            string pattern = ModelFileResolver<RouteModelCore>.WildcardPattern;

            ConcurrentBag<RouteModelCore> routes = new ConcurrentBag<RouteModelCore>();
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
                    RouteModelCore route = await RouteModelHandler.FromFile(file, contentFolder, token, false).ConfigureAwait(false);
                    if (route != null && routeFolders.TryRemove(route.Tag, out FolderStructure.ContentFolder.RouteFolder routeFolder)) //
                    {
                        //if (route.SetupRequired())
                        //    route = await RouteModelHandler.Convert(routeFolder, contentFolder, token).ConfigureAwait(false);
                        routes.Add(route);
                    }
                }).ConfigureAwait(false);
            }

            //for any new MSTS folder (remaining in the preloaded dictionary), Create a route model
            await Parallel.ForEachAsync(routeFolders, cancellationToken, async (routeFolder, token) =>
            {
                //RouteModelCore route = await RouteModelHandler.Convert(routeFolder.Value, contentFolder, token).ConfigureAwait(false);
                //if (null != route)
                //{
                //    routes.Add(route);
                //}
            }).ConfigureAwait(false);

            return contentFolder;
        }

        public static async Task<FolderModel> Convert(FolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));

            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(contentFolder);
            string pattern = ModelFileResolver<RouteModelCore>.WildcardPattern;

            ConcurrentBag<RouteModelCore> routes = new ConcurrentBag<RouteModelCore>();
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
                    RouteModelCore route = await RouteModelHandler.FromFile(file, contentFolder, token, false).ConfigureAwait(false);
                    if (route != null && routeFolders.TryRemove(route.Tag, out FolderStructure.ContentFolder.RouteFolder routeFolder)) //
                    {
                        //if (route.SetupRequired())
                        //    route = await RouteModelHandler.Convert(routeFolder, contentFolder, token).ConfigureAwait(false);
                        routes.Add(route);
                    }
                }).ConfigureAwait(false);
            }

            //for any new MSTS folder (remaining in the preloaded dictionary), Create a route model
            await Parallel.ForEachAsync(routeFolders, cancellationToken, async (routeFolder, token) =>
            {
                //RouteModelCore route = await RouteModelHandler.Convert(routeFolder.Value, contentFolder, token).ConfigureAwait(false);
                //if (null != route)
                //{
                //    routes.Add(route);
                //}
            }).ConfigureAwait(false);

            contentFolder.SetRoutes(routes);
            IFileResolve parent = (contentFolder as IFileResolve).Container;
            contentFolder.Initialize(ModelFileResolver<FolderModel>.FilePath(contentFolder, parent), parent);
            contentFolder.RefreshModel();
            return contentFolder;
        }
    }
}
