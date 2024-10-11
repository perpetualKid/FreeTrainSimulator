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
    internal sealed class PathModelCoreHandler : ContentHandlerBase<PathModelCore, PathModelCore>
    {
        public static async ValueTask<PathModelCore> Get(PathModelCore pathModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));
            return await Get(pathModel.Id, pathModel.Parent, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<PathModelCore> Get(string pathId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(pathId);
            bool renewed = false;

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<PathModelCore>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<PathModelCore>>(FromFile(pathId, routeModel, cancellationToken));
                renewed = true;
            }

            PathModelCore pathModel = await modelTask.Value.ConfigureAwait(false);

            if (pathModel.SetupRequired())
            {
                taskLazyCache[key] = new Lazy<Task<PathModelCore>>(() => PathModelHandler.Cast(PathModelHandler.Convert(pathModel, cancellationToken)));
                renewed = true;
            }

            if (renewed)
            {
                key = routeModel.Hierarchy();
                _ = taskSetCache.TryRemove(key, out _);
            }

            return pathModel;
        }

        public static async ValueTask<FrozenSet<PathModelCore>> GetPaths(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();
            if (!taskSetCache.TryGetValue(key, out Lazy<Task<FrozenSet<PathModelCore>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                modelSetTask = new Lazy<Task<FrozenSet<PathModelCore>>>(() => LoadRefresh(routeModel, cancellationToken));
            }

            FrozenSet<PathModelCore> result = await modelSetTask.Value.ConfigureAwait(false);
            taskSetCache[key] = modelSetTask;
            return result;
        }

        private static async Task<FrozenSet<PathModelCore>> LoadRefresh(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            string pathsFolder = ModelFileResolver<RouteModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<PathModelCore>.WildcardSavePattern;

            ConcurrentBag<PathModelCore> results = new ConcurrentBag<PathModelCore>();

            //load existing path models, and compare if the corresponding folder still exists.
            if (Directory.Exists(pathsFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(pathsFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string pathId = Path.GetFileNameWithoutExtension(file);

                    if (pathId.EndsWith(fileExtension))
                        pathId = pathId[..^fileExtension.Length];

                    PathModelCore path = await Get(pathId, routeModel, token).ConfigureAwait(false);
                    if (null != path)
                        results.Add(path);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }
    }
}
