using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class ContentModelHandler : ContentHandlerBase<ContentModel>
    {
        private const string keyName = "content";

        public static Task<ContentModel> GetCore(CancellationToken cancellationToken)
        {
            if (!modelTaskCache.TryGetValue(keyName, out Task<ContentModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[keyName] = modelTask = FromFile<ContentModel>(string.Empty, null, cancellationToken);
            }

            return modelTask;
        }

        public static async Task<ContentModel> Setup(IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            ContentModel contentModel = await GetCore(cancellationToken).ConfigureAwait(false);

            contentModel = (contentModel ??= new ContentModel()) with
            {
                ContentFolders = folders != null ? folders.Select(folderModelHolder => new FolderModel(folderModelHolder.Item1, folderModelHolder.Item2, contentModel)).ToImmutableArray() : ImmutableArray<FolderModel>.Empty
            };
            contentModel = await Convert(contentModel, cancellationToken).ConfigureAwait(false);

            modelTaskCache[keyName] = Task.FromResult(contentModel);

            return contentModel;
        }

        private static async Task<ContentModel> Convert(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            contentModel = contentModel with
            {
                ContentFolders = await FolderModelHandler.ExpandFolderModels(contentModel, cancellationToken).ConfigureAwait(false)
            };
            await Create(contentModel, (ModelBase)null, cancellationToken).ConfigureAwait(false);
            return contentModel;
        }
    }
}
