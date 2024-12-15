using System;
using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    public static class FolderModelExtensions
    {
        public static Task<FrozenSet<RouteModelCore>> GetRoutes(this FolderModel folderModel, CancellationToken cancellationToken) => RouteModelHandler.GetRoutes(folderModel, cancellationToken);
        public static Task<FrozenSet<WagonSetModel>> GetWagonSets(this FolderModel folderModel, CancellationToken cancellationToken) => WagonSetModelHandler.GetWagonSets(folderModel, cancellationToken);
        public static ValueTask<FrozenSet<WagonReferenceModel>> GetLocomotives(this FolderModel folderModel, CancellationToken cancellationToken) => WagonSetModelHandler.GetLocomotives(folderModel, cancellationToken);

        public static async ValueTask<RouteModel> RouteModel(this FolderModel folderModel, string routeName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            ArgumentException.ThrowIfNullOrEmpty(routeName, nameof(routeName));

            return await RouteModelHandler.GetExtended(routeName, folderModel, cancellationToken).ConfigureAwait(false);
        }

        public static FrozenSet<WagonSetModel> GetWagonSets(this FolderModel folderModel) => Task.Run(async () => await folderModel.GetWagonSets(CancellationToken.None).ConfigureAwait(false)).Result;

    }
}
