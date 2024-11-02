using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class FolderModelHandler : ContentHandlerBase<FolderModel>
    {
        public static ValueTask<FolderModel> GetCore(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            return GetCore(folderModel.Id, folderModel.Parent, cancellationToken);
        }

        public static async ValueTask<FolderModel> GetCore(string folderId, ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            string key = profileModel.Hierarchy(folderId);

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<FolderModel>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<FolderModel>>(Task.FromResult(profileModel.ContentFolders.Where((folder) => string.Equals(folder.Id, folderId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault()));
                collectionUpdateRequired[profileModel.Hierarchy()] = true;
            }

            FolderModel folderModel = await modelTask.Value.ConfigureAwait(false);

            if (folderModel?.RefreshRequired ?? false)
            {
                taskLazyCache[key] = new Lazy<Task<FolderModel>>(() => Cast(Convert(folderModel, cancellationToken)));
                collectionUpdateRequired[profileModel.Hierarchy()] = true;
            }

            return folderModel;
        }

        public static async ValueTask<FrozenSet<FolderModel>> GetFolders(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            string key = profileModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !taskLazyCollectionCache.TryGetValue(key, out Lazy<Task<FrozenSet<FolderModel>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<FolderModel>>>(() => LoadFolders(profileModel, cancellationToken));
            }

            return await modelSetTask.Value.ConfigureAwait(false);
        }

        public static async Task<FrozenSet<FolderModel>> ExpandFolderModels(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            ConcurrentBag<FolderModel> results = new ConcurrentBag<FolderModel>();

            FrozenSet<FolderModel> existingFolders = await GetFolders(profileModel, cancellationToken).ConfigureAwait(false);
            Dictionary<string, FolderModel> configuredFolders = new Dictionary<string, FolderModel>(profileModel.ContentFolders.ToDictionary(f => f.Id), StringComparer.OrdinalIgnoreCase);

            foreach (FolderModel folderModel in existingFolders)
            {
                if (configuredFolders.Remove(folderModel.Id))
                {
                    results.Add(folderModel);
                }
            };

            //for any new MSTS folder (remaining in the preloaded dictionary), Create a new model
            await Parallel.ForEachAsync(configuredFolders, cancellationToken, async (folderModelHolder, token) =>
            {
                Lazy<Task<FolderModel>> modelTask = new Lazy<Task<FolderModel>>(Convert(folderModelHolder.Value, cancellationToken));
                FolderModel folderModel = await modelTask.Value.ConfigureAwait(false);
                string key = folderModel.Hierarchy();
                results.Add(folderModel);
                taskLazyCache[key] = modelTask;
            }).ConfigureAwait(false);

            FrozenSet<FolderModel> result = results.ToFrozenSet();
            string key = profileModel.Hierarchy();
            Lazy<Task<FrozenSet<FolderModel>>> modelSetTask;
            taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<FolderModel>>>(Task.FromResult(result));
            return result;
        }

        public static async Task<FrozenSet<FolderModel>> SetupFolderModels(ProfileModel profileModel, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            ConcurrentBag<FolderModel> results = new ConcurrentBag<FolderModel>();

            await Parallel.ForEachAsync(folders, cancellationToken, async (folderModelHolder, token) =>
            {
                Lazy<Task<FolderModel>> modelTask = new Lazy<Task<FolderModel>>(Convert(new FolderModel(folderModelHolder.Item1, folderModelHolder.Item2, profileModel), cancellationToken));
                FolderModel folderModel = await modelTask.Value.ConfigureAwait(false);
                string key = folderModel.Hierarchy();
                results.Add(folderModel);
                taskLazyCache[key] = modelTask;
            }).ConfigureAwait(false);

            FrozenSet<FolderModel> result = results.ToFrozenSet();
            string key = profileModel.Hierarchy();
            Lazy<Task<FrozenSet<FolderModel>>> modelSetTask;
            taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<FolderModel>>>(Task.FromResult(result));
            return result;
        }

        private static async Task<FrozenSet<FolderModel>> LoadFolders(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ConcurrentBag<FolderModel> results = new ConcurrentBag<FolderModel>();

            await Parallel.ForEachAsync(profileModel.ContentFolders, cancellationToken, async (folder, token) =>
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
