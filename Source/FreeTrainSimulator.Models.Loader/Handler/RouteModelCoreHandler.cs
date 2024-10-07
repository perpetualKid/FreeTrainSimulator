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
        private static ConcurrentDictionary<string, (Task<RouteModelCore>, Lazy<Task<RouteModelCore>>)> modelLazyCache =
            new ConcurrentDictionary<string, (Task<RouteModelCore>, Lazy<Task<RouteModelCore>>)>(StringComparer.OrdinalIgnoreCase);

        private static ConcurrentDictionary<string, Task<FrozenSet<RouteModelCore>>> modelSetCache = new ConcurrentDictionary<string, Task<FrozenSet<RouteModelCore>>>(StringComparer.OrdinalIgnoreCase);

        public static async ValueTask<RouteModelCore> Get(string fileName, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fileName)));

            if (!modelLazyCache.TryGetValue(key, out (Task<RouteModelCore> SimpleTask, Lazy<Task<RouteModelCore>> LazyTask) cachedTask))
            {
                _ = modelLazyCache.TryAdd(key, cachedTask = (FromFile(fileName, folderModel, cancellationToken, false), null));
            }
            if (cachedTask.SimpleTask.IsFaulted)
                modelLazyCache[key] = cachedTask = (FromFile(fileName, folderModel, cancellationToken, false), null);

            RouteModelCore routeModel = await cachedTask.SimpleTask.ConfigureAwait(false);

            if (routeModel.SetupRequired())
            {
                modelLazyCache[key] = cachedTask = (FromFile(fileName, folderModel, cancellationToken, false), new Lazy<Task<RouteModelCore>>(() => RouteModelHandler.Cast(RouteModelHandler.Convert(routeModel, cancellationToken))));
            }
            return routeModel;
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

        internal static async ValueTask<RouteModelCore> Get(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            // if there is a lazy task for the given routeModel
            //      
            //      return that result
            // else return the cached task

            if (!modelLazyCache.TryGetValue(key, out (Task<RouteModelCore> SimpleTask, Lazy<Task<RouteModelCore>> LazyTask) cachedTask))
            {
                return null;
            }
            else
            {
                return await (cachedTask.LazyTask?.Value ?? cachedTask.SimpleTask).ConfigureAwait(false);
            }
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