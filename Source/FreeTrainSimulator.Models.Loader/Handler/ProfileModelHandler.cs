using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class ProfileModelHandler : ContentHandlerBase<ProfileModel>
    {
        private const string root = "root";
        public const string DefaultProfileName = "Default";

        private static bool CheckDefaultProfile(string profileName) => string.IsNullOrEmpty(profileName) || string.Equals(profileName, DefaultProfileName, StringComparison.OrdinalIgnoreCase);

        public static ValueTask<ProfileModel> GetCore(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            return GetCore(profileModel.Name, cancellationToken);
        }

        public static async ValueTask<ProfileModel> GetCore(string profileName, CancellationToken cancellationToken)
        {
            if (CheckDefaultProfile(profileName))
                profileName = DefaultProfileName;

            string key = profileName;

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<ProfileModel>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<ProfileModel>>(FromFile<ProfileModel>(profileName, null, cancellationToken));
                collectionUpdateRequired[root] = true;
            }

            ProfileModel profileModel = await modelTask.Value.ConfigureAwait(false) ?? new ProfileModel(profileName);

            if (profileModel.SetupRequired())
            {
                taskLazyCache[key] = new Lazy<Task<ProfileModel>>(() => Cast(Convert(profileModel, cancellationToken)));
                collectionUpdateRequired[root] = true;
            }

            return profileModel;
        }

        public static async ValueTask<FrozenSet<ProfileModel>> GetProfiles(CancellationToken cancellationToken)
        {
            string key = root;

            if (collectionUpdateRequired.TryRemove(key, out _) || !taskLazyCollectionCache.TryGetValue(key, out Lazy<Task<FrozenSet<ProfileModel>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<ProfileModel>>>(() => LoadProfiles(cancellationToken));
            }

            return await modelSetTask.Value.ConfigureAwait(false);
        }

        private static Task<ProfileModel> Convert(string profileName, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(profileName, nameof(profileName));

            ProfileModel profileModel = new ProfileModel(profileName);
            return Convert(profileModel, cancellationToken);
        }

        private static async Task<ProfileModel> Convert(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
             
            profileModel = profileModel with { ContentFolders = await FolderModelHandler.ExpandFolderModels(profileModel, cancellationToken).ConfigureAwait(false) };
            await Create<ProfileModel>(profileModel, null, cancellationToken).ConfigureAwait(false);
            return profileModel;
        }

        private static async Task<FrozenSet<ProfileModel>> LoadProfiles(CancellationToken cancellationToken)
        {
            string profilesFolder = ModelFileResolver<ProfileModel>.FolderPath(null);
            string pattern = ModelFileResolver<ProfileModel>.WildcardSavePattern;

            ConcurrentBag<ProfileModel> results = new ConcurrentBag<ProfileModel>();

            //load existing profile models, and compare if the corresponding folder still exists.
            if (Directory.Exists(profilesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(profilesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string profileId = Path.GetFileNameWithoutExtension(file);

                    if (profileId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        profileId = profileId[..^fileExtension.Length];

                    ProfileModel profile = await GetCore(profileId, token).ConfigureAwait(false);
                    if (null != profile)
                        results.Add(profile);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }

        public static async Task<ProfileModel> Setup(string profileName, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(profileName, nameof(profileName));
            ProfileModel profileModel = await GetCore(profileName, cancellationToken).ConfigureAwait(false);

            profileModel = (profileModel ?? new ProfileModel(profileName)) with 
            { 
                ContentFolders = folders != null ? await FolderModelHandler.SetupFolderModels(profileModel, folders, cancellationToken).ConfigureAwait(false) : FrozenSet<FolderModel>.Empty 
            };
            profileModel = await Convert(profileModel, cancellationToken).ConfigureAwait(false);

            string key = profileName;
            taskLazyCache[key] = new Lazy<Task<ProfileModel>>(Task.FromResult(profileModel));
            collectionUpdateRequired[root] = true;

            return profileModel;
        }
    }
}
