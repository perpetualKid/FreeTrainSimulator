using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator
{
    internal sealed class ContentModelImportHandler : ContentHandlerBase<ContentModel>
    {
        private const string root = "root";
        private const string keyName = "content";

        public static Task<ContentModel> Expand(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            Task<ContentModel> modelTask = Convert(contentModel, cancellationToken);
            modelTaskCache[keyName] = modelTask;
            collectionUpdateRequired[root] = true;

            return modelTask;
        }

        private static async Task<ContentModel> Convert(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            contentModel = contentModel with
            {
                ContentFolders = await FolderModelImportHandler.ExpandFolderModels(contentModel, cancellationToken).ConfigureAwait(false)
            };
            await Create(contentModel, (ModelBase)null, cancellationToken).ConfigureAwait(false);
            return contentModel;
        }
    }
}
