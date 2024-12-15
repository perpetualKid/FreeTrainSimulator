using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;

namespace FreeTrainSimulator.Models.Imported.ImportHandler
{
    internal sealed class ContentModelImportHandler : ContentHandlerBase<ContentModel>
    {
        private const string root = "root";

        public static Task<ContentModel> Expand(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));
            string key = contentModel.Name;

            Task<ContentModel> modelTask = Convert(contentModel, cancellationToken);
            modelTaskCache[key] = modelTask;
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
