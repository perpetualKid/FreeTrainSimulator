using System;
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

        private static bool CheckDefaultProfile(string profileName) => string.IsNullOrEmpty(profileName) || string.Equals(profileName, DefaultProfileName, StringComparison.OrdinalIgnoreCase);

        private static bool CheckDefaultProfile(ProfileModel profileModel) => profileModel == null || CheckDefaultProfile(profileModel.Name);

        public static ProfileModel DefaultProfile { get; private set; } = new ProfileModel(DefaultProfileName);

        public static async ValueTask<ProfileModel> Get(string profileName, CancellationToken cancellationToken)
        {
            if (CheckDefaultProfile(profileName))
            {
                if (DefaultProfile?.Initialized ?? false)
                    return DefaultProfile; //already initialized default model, just returning that instance

                return DefaultProfile = await FromFile<ProfileModel>(DefaultProfileName, null, cancellationToken).ConfigureAwait(false);
            }
            else
                //return the specific profile instance if exists)
                return await FromFile<ProfileModel>(profileName, null, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<ProfileModel> Convert(string profileName, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
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
            if (CheckDefaultProfile(contentProfile))
                DefaultProfile = contentProfile;
            return contentProfile;
        }

        private static async ValueTask<ProfileModel> Setup(string profileName, CancellationToken cancellationToken)
        {
            ProfileModel contentProfile = new ProfileModel(string.IsNullOrEmpty(profileName) ? DefaultProfileName : profileName);
            await Create(contentProfile, (ProfileModel)null, true, true, cancellationToken).ConfigureAwait(false);
            if (contentProfile.Name == DefaultProfileName)
                DefaultProfile = contentProfile;
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
            if (CheckDefaultProfile(contentProfile))
                DefaultProfile = contentProfile;
            return contentProfile;
        }
    }
}
