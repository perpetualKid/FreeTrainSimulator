using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

using SharpDX;

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
                collectionUpdateRequired = true;
            }

            FolderModel folderModel = await modelTask.Value.ConfigureAwait(false);

            if (folderModel.SetupRequired())
            {
                taskLazyCache[key] = new Lazy<Task<FolderModel>>(() => Cast(Convert(folderModel, cancellationToken)));
                collectionUpdateRequired = true;
            }

            return folderModel;
        }

        public static async ValueTask<FrozenSet<FolderModel>> GetFolders(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            string key = profileModel.Hierarchy();

            if (collectionUpdateRequired || !taskSetCache.TryGetValue(key, out Lazy<Task<FrozenSet<FolderModel>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                taskSetCache[key] = modelSetTask = new Lazy<Task<FrozenSet<FolderModel>>>(() => LoadFolders(profileModel, cancellationToken));
                collectionUpdateRequired = false;
            }

            return await modelSetTask.Value.ConfigureAwait(false);
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
            //_ = RouteModelHandler.ExpandRouteModels(folderModel, cancellationToken);
            //return Task.FromResult(folderModel);
            _ = await RouteModelHandler.ExpandRouteModels(folderModel, cancellationToken).ConfigureAwait(false);
            return folderModel;
        }

        public static async Task<FolderModel> Create(string folderName, string repositoryPath, ProfileModel profile, CancellationToken cancellationToken)
        {
            FolderModel contentFolder = new FolderModel(folderName, repositoryPath, profile);
            await Create(contentFolder, profile, false, true, cancellationToken).ConfigureAwait(false);
            return contentFolder;
        }
    }
}
