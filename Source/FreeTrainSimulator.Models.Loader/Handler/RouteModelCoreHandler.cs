using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class RouteModelCoreHandler : ContentHandlerBase<RouteModelCore, RouteModelCore>
    {
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

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<RouteModelCore>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<RouteModelCore>>(FromFile(routeId, folderModel, cancellationToken));
                renewed = true;
            }

            RouteModelCore routeModel = await modelTask.Value.ConfigureAwait(false);

            if (routeModel.SetupRequired())
            {
                taskLazyCache[key] = new Lazy<Task<RouteModelCore>>(() => RouteModelHandler.Cast(RouteModelHandler.Convert(routeModel, cancellationToken)));
                // now also need to expand (renew) the child entities

                renewed = true;
            }

            if (renewed)
            {
                key = folderModel.Hierarchy();
                _ = taskSetCache.TryRemove(key, out _);
            }

            return routeModel;
        }

        public static async ValueTask<FrozenSet<RouteModelCore>> GetRoutes(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy();

            if (!taskSetCache.TryGetValue(key, out Lazy<Task<FrozenSet<RouteModelCore>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                modelSetTask = new Lazy<Task<FrozenSet<RouteModelCore>>>(() => LoadRefresh(folderModel, cancellationToken));
            }

            FrozenSet<RouteModelCore> result = await modelSetTask.Value.ConfigureAwait(false);
            taskSetCache[key] = modelSetTask;
            return result;
        }

        private static async Task<FrozenSet<RouteModelCore>> LoadRefresh(FolderModel folderModel, CancellationToken cancellationToken)
        {
            string routesFolder = ModelFileResolver<FolderModel>.FolderPath(folderModel);
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

                    RouteModelCore route = await Get(routeId, folderModel, token).ConfigureAwait(false);
                    if (null != route)
                        results.Add(route);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }
    }
}