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
        private static readonly string fileExtension = ModelFileResolver<RouteModelCore>.FileExtension;

        private static readonly ConcurrentDictionary<string, Lazy<Task<RouteModelCore>>> taskLazyCache = new ConcurrentDictionary<string, Lazy<Task<RouteModelCore>>>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Task<FrozenSet<RouteModelCore>>> taskSetCache = new ConcurrentDictionary<string, Task<FrozenSet<RouteModelCore>>>(StringComparer.OrdinalIgnoreCase);

        public static async ValueTask<RouteModelCore> Get(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return await Get(routeModel.Id, routeModel.Parent, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<RouteModelCore> Get (string routeId, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy(routeId);
            bool renewed = false;

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<RouteModelCore>> cachedTask) || (cachedTask.IsValueCreated && cachedTask.Value.IsFaulted))
            {
                taskLazyCache[key] = cachedTask = new Lazy<Task<RouteModelCore>>(FromFile(routeId, folderModel, cancellationToken));
                renewed = true;
            }

            RouteModelCore routeModel = await cachedTask.Value.ConfigureAwait(false);

            if (routeModel.SetupRequired())
            {
                taskLazyCache[key] = new Lazy<Task<RouteModelCore>>(() => RouteModelHandler.Cast(RouteModelHandler.Convert(routeModel, cancellationToken)));
                renewed = true;
            }

            if (renewed)
            {
                key = folderModel.Hierarchy();
                _ = taskSetCache.TryRemove(key, out _);
            }

            return routeModel;
        }

        public static async ValueTask<FrozenSet<RouteModelCore>> GetRoutes(FolderModel contentFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));
            string key = contentFolder.Hierarchy();

            if (!taskSetCache.TryGetValue(key, out Task<FrozenSet<RouteModelCore>> routeModelSetTask) || routeModelSetTask.IsFaulted)
            {
                routeModelSetTask = LoadRefresh(contentFolder, cancellationToken);
            }

            FrozenSet<RouteModelCore> result = await routeModelSetTask.ConfigureAwait(false);
            taskSetCache[key] = routeModelSetTask;
            return result;
        }

        private static async Task<FrozenSet<RouteModelCore>> LoadRefresh(FolderModel contentFolder, CancellationToken cancellationToken)
        {
            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(contentFolder);
            string pattern = ModelFileResolver<RouteModelCore>.WildcardSavePattern;

            ConcurrentBag<RouteModelCore> results = new ConcurrentBag<RouteModelCore>();

            //load existing route models, and compare if the corresponding folder still exists.
            if (Directory.Exists(routesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(routesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string routeId = Path.GetFileNameWithoutExtension(file);

                    if (routeId.EndsWith(fileExtension))
                        routeId = routeId[..^fileExtension.Length];

                    RouteModelCore route = await Get(routeId, contentFolder, token).ConfigureAwait(false);
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