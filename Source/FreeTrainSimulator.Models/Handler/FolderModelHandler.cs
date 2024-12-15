using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class FolderModelHandler : ContentHandlerBase<FolderModel>
    {
        public static Task<FolderModel> GetCore(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            return GetCore(folderModel.Id, folderModel.Parent, cancellationToken);
        }

        public static Task<FolderModel> GetCore(string folderId, ContentModel contentModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));
            string key = contentModel.Hierarchy(folderId);

            if (!modelTaskCache.TryGetValue(key, out Task<FolderModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = Task.FromResult(contentModel.ContentFolders.GetByName(folderId));
                collectionUpdateRequired[contentModel.Hierarchy()] = true;
            }

            return modelTask;
        }

        public static async ValueTask<FrozenSet<FolderModel>> GetFolders(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));
            string key = contentModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<FrozenSet<FolderModel>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadFolders(contentModel, cancellationToken);
            }

            return await modelSetTask.ConfigureAwait(false);
        }

        public static async Task<FrozenSet<FolderModel>> ExpandFolderModels(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            ConcurrentBag<FolderModel> results = new ConcurrentBag<FolderModel>();

            Dictionary<string, FolderModel> configuredFolders = new Dictionary<string, FolderModel>(contentModel.ContentFolders.ToDictionary(f => f.Id), StringComparer.OrdinalIgnoreCase);

            await Parallel.ForEachAsync(configuredFolders, cancellationToken, async (folderModelHolder, token) =>
            {
                Task<FolderModel> modelTask = Convert(folderModelHolder.Value, cancellationToken);
                FolderModel folderModel = await modelTask.ConfigureAwait(false);
                string key = folderModel.Hierarchy();
                results.Add(folderModel);
                modelTaskCache[key] = modelTask;
            }).ConfigureAwait(false);

            FrozenSet<FolderModel> result = results.ToFrozenSet();
            string key = contentModel.Hierarchy();
            modelSetTaskCache[key] = Task.FromResult(result);
            return result;
        }

        private static async Task<FrozenSet<FolderModel>> LoadFolders(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ConcurrentBag<FolderModel> results = new ConcurrentBag<FolderModel>();

            await Parallel.ForEachAsync(contentModel.ContentFolders, cancellationToken, async (folder, token) =>
            {
                folder = await GetCore(folder, token).ConfigureAwait(false);
                if (null != folder)
                    results.Add(folder);
            }).ConfigureAwait(false);

            return results.ToFrozenSet();
        }

        private static async Task<FolderModel> Convert(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            folderModel.RefreshModel();

            await Create(folderModel, folderModel.Parent, false, true, cancellationToken).ConfigureAwait(false);
            return folderModel;
        }
    }
}
