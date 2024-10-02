using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class ProfileModelHandler : ContentHandlerBase<ProfileModel, ProfileModel>
    {
        public const string DefaultProfileName = "Default";

        private static readonly ConcurrentDictionary<string, Task<ProfileModel>> modelCache = new ConcurrentDictionary<string, Task<ProfileModel>>(StringComparer.OrdinalIgnoreCase);

        private static bool CheckDefaultProfile(string profileName) => string.IsNullOrEmpty(profileName) || string.Equals(profileName, DefaultProfileName, StringComparison.OrdinalIgnoreCase);

        private static bool CheckDefaultProfile(ProfileModel profileModel) => profileModel == null || CheckDefaultProfile(profileModel.Name);

        public static async ValueTask<ProfileModel> Get(string profileName, CancellationToken cancellationToken)
        {
            if (CheckDefaultProfile(profileName))
                profileName = DefaultProfileName;


            if (!modelCache.TryGetValue(profileName, out Task<ProfileModel> profileModelTask))
            {
                _ = modelCache.TryAdd(profileName, profileModelTask = FromFile<ProfileModel>(profileName, null, cancellationToken));
            }
            if (profileModelTask.IsFaulted)
                modelCache[profileName] = profileModelTask = FromFile<ProfileModel>(profileName, null, cancellationToken);

            return await profileModelTask.ConfigureAwait(false);
        }

        public static async ValueTask<ProfileModel> Convert(string profileName, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            if (CheckDefaultProfile(profileName))
                profileName = DefaultProfileName;

            ProfileModel contentProfile = await Get(profileName, cancellationToken).ConfigureAwait(false);

            if (contentProfile == null)
            {
                contentProfile = await Setup(profileName, cancellationToken).ConfigureAwait(false);
                contentProfile = await UpdateFolders(contentProfile, folders, cancellationToken).ConfigureAwait(false);
            }
            else if (contentProfile.RefreshRequired)
            {
                contentProfile = await UpdateFolders(contentProfile, folders, cancellationToken).ConfigureAwait(false);
            }
            modelCache[profileName] = Task.FromResult(contentProfile);
            return contentProfile;
        }

        private static async ValueTask<ProfileModel> Setup(string profileName, CancellationToken cancellationToken)
        {
            if (CheckDefaultProfile(profileName))
                profileName = DefaultProfileName;

            ProfileModel contentProfile = new ProfileModel(profileName);
            await Create(contentProfile, (ProfileModel)null, true, true, cancellationToken).ConfigureAwait(false);
            modelCache[profileName] = Task.FromResult(contentProfile);
            return contentProfile;
        }

        private static async ValueTask<ProfileModel> UpdateFolders(ProfileModel contentProfile, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            if (null == folders)
                return contentProfile;

            ArgumentNullException.ThrowIfNull(contentProfile, nameof(contentProfile));

            contentProfile = new ProfileModel(contentProfile.Name, (await Task.WhenAll(folders.Select(
                async (item) => await FolderModelHandler.Create(item.Item1, item.Item2, contentProfile, cancellationToken).ConfigureAwait(false))).ConfigureAwait(false)).ToFrozenSet());
            contentProfile.Initialize(ModelFileResolver<ProfileModel>.FilePath(contentProfile, null), null);
            contentProfile = await ToFile(contentProfile, cancellationToken).ConfigureAwait(false);
            modelCache[contentProfile.Name] = Task.FromResult(contentProfile);
            return contentProfile;
        }
    }
}
