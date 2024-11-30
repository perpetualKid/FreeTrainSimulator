using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class ProfileModelHandler : ContentHandlerBase<ProfileModel>
    {
        private const string root = "root";
        public const string DefaultProfileName = "Default";

        public static string CheckDefaultProfile(string profileName) => (string.IsNullOrEmpty(profileName) || string.Equals(profileName, DefaultProfileName, StringComparison.OrdinalIgnoreCase)) ? DefaultProfileName : profileName;

        public static Task<ProfileModel> GetCore(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            return GetCore(profileModel.Name, cancellationToken);
        }

        public static Task<ProfileModel> GetCore(string profileName, CancellationToken cancellationToken)
        {
            profileName = CheckDefaultProfile(profileName);

            string key = profileName;

            if (!modelTaskCache.TryGetValue(key, out Task<ProfileModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile<ProfileModel>(profileName, null, cancellationToken);
                collectionUpdateRequired[root] = true;
            }

            return modelTask;
        }

        public static Task<FrozenSet<ProfileModel>> GetProfiles(CancellationToken cancellationToken)
        {
            string key = root;

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<FrozenSet<ProfileModel>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadProfiles(cancellationToken);
            }

            return modelSetTask;
        }

        public static Task<ProfileModel> Expand(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            string key = profileModel.Name;

            Task<ProfileModel> modelTask = Convert(profileModel, cancellationToken);
            modelTaskCache[key] = modelTask;
            collectionUpdateRequired[root] = true;

            return modelTask;
        }

        public static async Task<ProfileModel> Setup(string profileName, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            profileName = CheckDefaultProfile(profileName);
            ProfileModel profileModel = await GetCore(profileName, cancellationToken).ConfigureAwait(false);

            profileModel = (profileModel ??= new ProfileModel(profileName)) with
            {
                ContentFolders = folders != null ? folders.Select(folderModelHolder => new FolderModel(folderModelHolder.Item1, folderModelHolder.Item2, profileModel)).ToFrozenSet() : FrozenSet<FolderModel>.Empty
            };
            profileModel = await Convert(profileModel, cancellationToken).ConfigureAwait(false);

            string key = profileName;
            modelTaskCache[key] = Task.FromResult(profileModel);
            collectionUpdateRequired[root] = true;

            return profileModel;
        }

        private static async Task<ProfileModel> Convert(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            profileModel = profileModel with
            {
                ContentFolders = await FolderModelHandler.ExpandFolderModels(profileModel, cancellationToken).ConfigureAwait(false)
            };
            await Create(profileModel, ProfileModel.None, cancellationToken).ConfigureAwait(false);
            return profileModel;
        }

        private static async Task<FrozenSet<ProfileModel>> LoadProfiles(CancellationToken cancellationToken)
        {
            string profilesFolder = ModelFileResolver<ProfileModel>.FolderPath(ProfileModel.None);
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
    }
}
