using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class PathModelHandler : ContentHandlerBase<PathModelHeader>
    {
        public static Task<PathModelHeader> GetCore(PathModelHeader pathModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));
            return GetCore(pathModel.Id, pathModel.Parent, cancellationToken);
        }

        public static Task<PathModelHeader> GetCore(string pathId, RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(pathId);

            if (!modelTaskCache.TryGetValue(key, out Task<PathModelHeader> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(pathId, routeModel, cancellationToken);
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return modelTask;
        }

        public static ValueTask<PathModel> GetExtended(PathModelHeader pathModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));
            return pathModel is PathModel pathModelExtended ? ValueTask.FromResult(pathModelExtended) : GetExtended(pathModel.Id, pathModel.Parent, cancellationToken);
        }

        public static async ValueTask<PathModel> GetExtended(string pathId, RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(pathId);

            if (!modelTaskCache.TryGetValue(key, out Task<PathModelHeader> modelTask) || modelTask.IsFaulted ||
                await modelTask.ConfigureAwait(false) is not PathModel)
            {
                modelTaskCache[key] = modelTask = Cast(FromFile<PathModel, RouteModelHeader>(pathId, routeModel, cancellationToken));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return await modelTask.ConfigureAwait(false) as PathModel;
        }

        public static Task<PathModel> UpdatePath(PathModel pathModel, RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            pathModel.Initialize(routeModel);
            collectionUpdateRequired[routeModel.Hierarchy()] = true;
            modelTaskCache.TryRemove(routeModel.Hierarchy(pathModel.Id), out _);
            return ToFile(pathModel, CancellationToken.None);
        }

        public static Task<ImmutableArray<PathModelHeader>> GetPaths(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<ImmutableArray<PathModelHeader>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadPaths(routeModel, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<ImmutableArray<PathModelHeader>> LoadPaths(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            string pathsFolder = ModelFileResolver<PathModelHeader>.FolderPath(routeModel);
            string pattern = ModelFileResolver<PathModelHeader>.WildcardSavePattern;

            ConcurrentBag<PathModelHeader> results = new ConcurrentBag<PathModelHeader>();

            //load existing path models, and compare if the corresponding folder still exists.
            if (Directory.Exists(pathsFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(pathsFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string pathId = Path.GetFileNameWithoutExtension(file);

                    if (pathId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        pathId = pathId[..^fileExtension.Length];

                    PathModelHeader path = await GetCore(pathId, routeModel, token).ConfigureAwait(false);
                    if (null != path)
                        results.Add(path);
                }).ConfigureAwait(false);
            }
            return results.ToImmutableArray();
        }
    }
}
