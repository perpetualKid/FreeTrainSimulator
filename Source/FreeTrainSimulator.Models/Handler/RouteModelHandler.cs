using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    /// <summary>
    /// Handler for route models. Loads individual <see cref="RouteModelHeader"/> or extended
    /// <see cref="RouteModel"/> instances from disk and enumerates all routes available within
    /// a content folder.
    /// </summary>
    internal sealed class RouteModelHandler : ContentHandlerBase<RouteModelHeader>
    {
        public static Task<RouteModelHeader> GetCore(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return GetCore(routeModel.Id, routeModel.Parent, cancellationToken);
        }

        public static Task<RouteModelHeader> GetCore(string routeId, FolderModel folderModel, CancellationToken cancellationToken)
            => GetOrAddCore(routeId, folderModel, cancellationToken);

        public static ValueTask<RouteModel> GetExtended(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return routeModel is RouteModel routeModelExtended ? ValueTask.FromResult(routeModelExtended) : GetExtended(routeModel.Id, routeModel.Parent, cancellationToken);
        }

        public static async ValueTask<RouteModel> GetExtended(string routeId, FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            string key = folderModel.Hierarchy(routeId);

            if (!modelTaskCache.TryGetValue(key, out Task<RouteModelHeader> modelTask) || modelTask.IsFaulted ||
                await modelTask.ConfigureAwait(false) is not RouteModel)
            {
                modelTaskCache[key] = modelTask = Cast(FromFile<RouteModel, FolderModel>(routeId, folderModel, cancellationToken));
                collectionUpdateRequired[folderModel.Hierarchy()] = true;
            }

            return await modelTask.ConfigureAwait(false) as RouteModel;
        }

        public static Task<ImmutableArray<RouteModelHeader>> GetRoutes(FolderModel folderModel, CancellationToken cancellationToken)
            => GetOrAddCollection(folderModel, cancellationToken);
    }
}