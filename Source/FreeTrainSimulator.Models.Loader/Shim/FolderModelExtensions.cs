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
        public static FolderStructure.ContentFolder MstsContentFolder(this FolderModel folderModel)
        {
            ContentFolderResolver resolver = FileResolver.ContentFolderResolver(folderModel);
            return resolver.MstsContentFolder;
        }

        public static async ValueTask<FrozenSet<RouteModelCore>> Routes(this FolderModel folderModel, CancellationToken cancellationToken)
        {
            return await RouteModelCoreHandler.GetRoutes(folderModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<FolderModel> Get(this FolderModel folderModel, CancellationToken cancellationToken)
        { 
            return await FolderModelHandler.Get(folderModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<RouteModel> RouteModel(this FolderModel folderModel, string routeName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            ArgumentException.ThrowIfNullOrEmpty(routeName, nameof(routeName));

            return await RouteModelHandler.Get(routeName, folderModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<FolderModel> Load(this FolderModel folderModel, CancellationToken cancellationToken)
        {
            return folderModel != null && folderModel.SetupRequired() ? await FolderModelHandler.Load(folderModel, cancellationToken).ConfigureAwait(false) : folderModel;
        }

        public static async ValueTask<FolderModel> Convert(this FolderModel folderModel, CancellationToken cancellationToken)
        {
            return folderModel != null ? await FolderModelHandler.Convert(folderModel, cancellationToken).ConfigureAwait(false) : folderModel;
        }
    }
}
