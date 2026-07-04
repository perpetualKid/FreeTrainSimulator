using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    /// <summary>
    /// Handler for path models. Loads individual <see cref="PathModelHeader"/> or extended
    /// <see cref="PathModel"/> instances from disk, enumerates all paths for a route, and
    /// supports saving edited path data back to the file system.
    /// </summary>
    internal sealed class PathModelHandler : ContentHandlerBase<PathModelHeader>
    {
        public static Task<PathModelHeader> GetCore(PathModelHeader pathModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));
            return GetCore(pathModel.Id, pathModel.Parent, cancellationToken);
        }

        public static Task<PathModelHeader> GetCore(string pathId, RouteModelHeader routeModel, CancellationToken cancellationToken)
            => GetOrAddCore(pathId, routeModel, cancellationToken);

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
            => GetOrAddCollection(routeModel, cancellationToken);
    }
}
