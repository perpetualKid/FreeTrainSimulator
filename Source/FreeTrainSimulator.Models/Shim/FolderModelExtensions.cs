using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    public static class FolderModelExtensions
    {
        public static Task<ImmutableArray<RouteModelCore>> GetRoutes(this FolderModel folderModel, CancellationToken cancellationToken) => RouteModelHandler.GetRoutes(folderModel, cancellationToken);
        public static Task<ImmutableArray<WagonSetModel>> GetWagonSets(this FolderModel folderModel, CancellationToken cancellationToken) => WagonSetModelHandler.GetWagonSets(folderModel, cancellationToken);
        public static ValueTask<ImmutableArray<WagonReferenceModel>> GetLocomotives(this FolderModel folderModel, CancellationToken cancellationToken) => WagonSetModelHandler.GetLocomotives(folderModel, cancellationToken);

        public static async ValueTask<RouteModel> RouteModel(this FolderModel folderModel, string routeId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            ArgumentException.ThrowIfNullOrEmpty(routeId, nameof(routeId));

            return await RouteModelHandler.GetExtended(routeId, folderModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<WagonSetModel> WagonSetModel(this FolderModel folderModel, string wagonSetId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            ArgumentException.ThrowIfNullOrEmpty(wagonSetId, nameof(wagonSetId));

            return await WagonSetModelHandler.GetCore(wagonSetId, folderModel, cancellationToken).ConfigureAwait(false);
        }

        public static ImmutableArray<WagonSetModel> GetWagonSets(this FolderModel folderModel) => Task.Run(async () => await folderModel.GetWagonSets(CancellationToken.None).ConfigureAwait(false)).Result;

    }
}
