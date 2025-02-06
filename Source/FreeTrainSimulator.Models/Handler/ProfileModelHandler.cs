using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class ProfileModelHandler : ContentHandlerBase<ProfileModel>
    {
        private const string root = "root";
        public const string DefaultProfile = "Default";

        private static string CheckDefaultProfile(string profileName) => string.IsNullOrEmpty(profileName) || string.Equals(profileName, DefaultProfile, StringComparison.OrdinalIgnoreCase) ? DefaultProfile : profileName;

        public static async Task<ProfileModel> Current(CancellationToken cancellationToken)
        {
            AllProfileSettingsModel currentProfileSettingsModel = await ProfileSettingModelHandler<AllProfileSettingsModel>.FromFile(null, cancellationToken).ConfigureAwait(false);
            string profileName = (await AllProfileSettingsHandler.GetCore(cancellationToken).ConfigureAwait(false))?.Profile;
            return (await GetProfiles(cancellationToken).ConfigureAwait(false)).GetByNameOrFirstByName(profileName) ?? new ProfileModel(DefaultProfile);
        }

        public static async Task UpdateCurrent(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            AllProfileSettingsModel currentProfileSettingsModel = new AllProfileSettingsModel()
            {
                Profile = profileModel?.Name,
            };
            await ContentHandlerBase<AllProfileSettingsModel>.ToFile(currentProfileSettingsModel, cancellationToken).ConfigureAwait(false);
        }

        public static Task<ProfileModel> GetCore(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            return GetCore(profileModel.Id, cancellationToken);
        }

        public static Task<ProfileModel> GetCore(string profileId, CancellationToken cancellationToken)
        {
            profileId = CheckDefaultProfile(profileId);

            string key = profileId;

            if (!modelTaskCache.TryGetValue(key, out Task<ProfileModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = Task.FromResult(new ProfileModel(profileId));
                collectionUpdateRequired[root] = true;
            }
            return modelTask;
        }

        public static Task<FrozenSet<ProfileModel>> GetProfiles(CancellationToken cancellationToken)
        {
            if (collectionUpdateRequired.TryRemove(root, out _) || !modelSetTaskCache.TryGetValue(root, out Task<FrozenSet<ProfileModel>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[root] = modelSetTask = LoadProfiles(cancellationToken);
            }

            return modelSetTask;
        }

        public static Task<FrozenSet<ProfileModel>> Create(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            string key = profileModel.Id;

            string profilesFolder = ModelFileResolver<ProfileModel>.FolderPath(null);
            Directory.CreateDirectory(Path.Combine(profilesFolder, profileModel.Name));
            modelTaskCache[key] = Task.FromResult(profileModel);
            collectionUpdateRequired[root] = true;
            return GetProfiles(cancellationToken);
        }

        public static Task<FrozenSet<ProfileModel>> Delete(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            string key = profileModel.Id;

            string profilesFolder = ModelFileResolver<ProfileModel>.FolderPath(null);
            try
            {
                Directory.Delete(Path.Combine(profilesFolder, profileModel.Name), true);
            }
            catch(Exception ex) when( ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
            { }

            modelTaskCache.TryRemove(key, out _);
            collectionUpdateRequired[root] = true;
            return GetProfiles(cancellationToken);
        }

        private static Task<FrozenSet<ProfileModel>> LoadProfiles(CancellationToken cancellationToken)
        {
            string profilesFolder = ModelFileResolver<ProfileModel>.FolderPath(null);

            ConcurrentBag<ProfileModel> results = new ConcurrentBag<ProfileModel>();

            if (Directory.Exists(profilesFolder))
            {
                Parallel.ForEach(Directory.EnumerateDirectories(profilesFolder), (directory) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;
                    string profileId = Path.GetFileNameWithoutExtension(directory);
                    if (String.Equals(profileId, ProfileModel.TestingProfile, StringComparison.OrdinalIgnoreCase))
                        return; // skip the $Testing profile from any regular listings

                    if (profileId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        profileId = profileId[..^fileExtension.Length];

                    results.Add(GetCore(profileId, cancellationToken).Result);
                });
            }
            return Task.FromResult(results.ToFrozenSet());
        }
    }
}
