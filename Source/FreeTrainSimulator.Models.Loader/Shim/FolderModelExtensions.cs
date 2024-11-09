using System;
using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Handler;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class FolderModelExtensions
    {
        public static FolderStructure.ContentFolder MstsContentFolder(this FolderModel folderModel) => FileResolver.ContentFolderResolver(folderModel).MstsContentFolder;
        public static ValueTask<FrozenSet<RouteModelCore>> GetRoutes(this FolderModel folderModel, CancellationToken cancellationToken) => RouteModelHandler.GetRoutes(folderModel, cancellationToken);
        public static ValueTask<FolderModel> Get(this FolderModel folderModel, CancellationToken cancellationToken) => FolderModelHandler.GetCore(folderModel, cancellationToken);
        public static ValueTask<FrozenSet<WagonSetModel>> GetWagonSets(this FolderModel folderModel, CancellationToken cancellationToken) => WagonSetModelHandler.GetWagonSets(folderModel, cancellationToken);
        public static ValueTask<FrozenSet<WagonReferenceModel>> GetLocomotives(this FolderModel folderModel, CancellationToken cancellationToken) => WagonSetModelHandler.GetLocomotives(folderModel, cancellationToken);

        public static async ValueTask<RouteModel> RouteModel(this FolderModel folderModel, string routeName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            ArgumentException.ThrowIfNullOrEmpty(routeName, nameof(routeName));

            return await RouteModelHandler.GetExtended(routeName, folderModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<FolderModel> Convert(this FolderModel folderModel, CancellationToken cancellationToken)
        {
            return folderModel != null ? await FolderModelHandler.GetCore(folderModel, cancellationToken).ConfigureAwait(false) : folderModel;
        }
    }
}
