using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public sealed class ContentProfileHandler : ContentHandlerBase<ContentProfileModel>
    {
        public const string DefaultProfileName = "Default";

        private static bool IsDefaultProfile(string profileName) => string.IsNullOrEmpty(profileName) || (string.Equals(profileName, DefaultProfileName, StringComparison.OrdinalIgnoreCase));

        private static bool IsDefaultProfile(ContentProfileModel profileModel) => profileModel == null || IsDefaultProfile(profileModel.Name);


        public static ContentProfileModel DefaultProfile { get; private set; } = new ContentProfileModel(DefaultProfileName);

        public static async ValueTask<ContentProfileModel> Get(string profileName, CancellationToken cancellationToken)
        {
            if (IsDefaultProfile(profileName))
            {
                if (DefaultProfile?.Initialized ?? false)
                    return DefaultProfile; //already initialized default model, just returning that instance

                return DefaultProfile = await FromFile<ContentProfileModel>(DefaultProfileName, null, cancellationToken).ConfigureAwait(false);
            }
            else
                //return the specific profile instance if exists)
                return await FromFile<ContentProfileModel>(profileName, null, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<ContentProfileModel> Convert(string profileName, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            ContentProfileModel contentProfile = await Get(profileName, cancellationToken).ConfigureAwait(false);

            if (contentProfile == null)
            {
                contentProfile = await Setup(profileName, cancellationToken).ConfigureAwait(false);
                contentProfile = await UpdateFolders(contentProfile, folders, cancellationToken).ConfigureAwait(false);
            }
            else if (contentProfile.RefreshRequired)
            {
                contentProfile = await UpdateFolders(contentProfile, folders, cancellationToken).ConfigureAwait(false);
            }
            if (IsDefaultProfile(contentProfile))
                DefaultProfile = contentProfile;
            return contentProfile;
        }

        private static async ValueTask<ContentProfileModel> Setup(string profileName, CancellationToken cancellationToken)
        {
            // try to load an existing profile with that name
            ContentProfileModel contentProfile = await Get(profileName, cancellationToken).ConfigureAwait(false);

            if (contentProfile == null)
            {
                contentProfile = new ContentProfileModel(string.IsNullOrEmpty(profileName) ? DefaultProfileName : profileName);
                await Create(contentProfile, (ContentProfileModel)null, true, true, cancellationToken).ConfigureAwait(false);
                if (contentProfile.Name == DefaultProfileName)
                    DefaultProfile = contentProfile;
            }
            return contentProfile;
        }

        private static async ValueTask<ContentProfileModel> UpdateFolders(ContentProfileModel contentProfile, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            if (null == folders)
                return contentProfile;

            ArgumentNullException.ThrowIfNull(contentProfile, nameof(contentProfile));

            contentProfile = new ContentProfileModel((await Task.WhenAll(folders.Select(
                async (item) => await ContentFolderHandler.Create(item.Item1, item.Item2, contentProfile, cancellationToken).ConfigureAwait(false))).ConfigureAwait(false)).ToFrozenSet())
            {
                Name = contentProfile.Name,
            };
            contentProfile.Initialize(ModelFileResolver<ContentProfileModel>.FilePath(contentProfile, null), null);
            await ToFile(contentProfile, cancellationToken).ConfigureAwait(false);
            return contentProfile;
        }
    }
}
