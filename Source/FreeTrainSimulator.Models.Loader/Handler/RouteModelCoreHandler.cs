using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class RouteModelCoreHandler : ContentHandlerBase<RouteModelCore, RouteModelCore>
    {
        private static ConcurrentDictionary<string, Task<RouteModelCore>> modelCache = new ConcurrentDictionary<string, Task<RouteModelCore>>(StringComparer.OrdinalIgnoreCase);
        private static ConcurrentDictionary<string, Task<FrozenSet<RouteModelCore>>> modelSetCache = new ConcurrentDictionary<string, Task<FrozenSet<RouteModelCore>>>(StringComparer.OrdinalIgnoreCase);

        public static async ValueTask<RouteModelCore> Get(string fileName, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy(Path.GetFileNameWithoutExtension(fileName));

            Task<RouteModelCore> fromCache = GetCachedTask(modelCache, key, () => FromFile(fileName, folderModel, cancellationToken, false));

            return await fromCache.ConfigureAwait(false);
        }

        public static async ValueTask<FrozenSet<RouteModelCore>> GetRoutes(FolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));
            string key = contentFolder.Hierarchy();

            if (!modelSetCache.TryGetValue(key, out Task<FrozenSet<RouteModelCore>> routeModelSetTask))
            {
                _ = modelSetCache.TryAdd(key, routeModelSetTask = GetRoutesInternal(contentFolder, cancellationToken));
            }
            if (routeModelSetTask.IsFaulted)
                modelSetCache[key] = routeModelSetTask = GetRoutesInternal(contentFolder, cancellationToken);

            return await routeModelSetTask.ConfigureAwait(false);
        }

        private static async Task<FrozenSet<RouteModelCore>> GetRoutesInternal(FolderModel contentFolder, CancellationToken cancellationToken)
        {
            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(contentFolder);
            string pattern = ModelFileResolver<RouteModelCore>.WildcardSavePattern;

            ConcurrentBag<RouteModelCore> results = new ConcurrentBag<RouteModelCore>();

            //load existing route models, and compare if the corresponding folder still exists.
            if (Directory.Exists(routesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(routesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    RouteModelCore route = await Get(file, contentFolder, token).ConfigureAwait(false);
                    if (null != route)
                        results.Add(route);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }

        public static async ValueTask<RouteModelCore> Load(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            routeModel = routeModel with
            {
                TrainPaths = await PathModelCoreHandler.GetPaths(routeModel, cancellationToken).ConfigureAwait(false),
                RouteActivities = await ActivityModelCoreHandler.GetActivities(routeModel, cancellationToken).ConfigureAwait(false)
            };
            IFileResolve parent = (routeModel as IFileResolve).Container;
            routeModel.Initialize(ModelFileResolver<RouteModelCore>.FilePath(routeModel, parent), parent);
            routeModel.RefreshModel();
            return routeModel;
        }
    }
}