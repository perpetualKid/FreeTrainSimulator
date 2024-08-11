using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public sealed class ContentProfileHandler : ContentHandlerBase<ContentProfileModel>
    {
        public static async ValueTask<ContentProfileModel> Load(string profileName, CancellationToken cancellationToken)
        {
            return await FromFile<ContentProfileModel>(profileName, null, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<ContentProfileModel> Create(string profileName, CancellationToken cancellationToken)
        {
            ContentProfileModel contentProfile = string.Equals(profileName, ContentProfileModel.Default.Name, StringComparison.OrdinalIgnoreCase) ?
                ContentProfileModel.Default : new ContentProfileModel(profileName);
            await Create(contentProfile, (ContentProfileModel)null, true, true, cancellationToken).ConfigureAwait(false);
            return contentProfile;
        }

        public static async ValueTask<ContentProfileModel> UpdateFolders(ContentProfileModel contentProfile, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            if (null == folders)
                return contentProfile;

            ArgumentNullException.ThrowIfNull(contentProfile, nameof(contentProfile));

            contentProfile.Clear();
            foreach ((string name, string path) in folders)
            {
                ContentFolderModel contentFolder = await ContentFolderHandler.Create(name, path, contentProfile, cancellationToken).ConfigureAwait(false);
                contentProfile.Add(contentFolder);
                if (cancellationToken.IsCancellationRequested)
                    return null;
            }
            await ToFile(contentProfile, cancellationToken).ConfigureAwait(false);
            return contentProfile;
        }

        public static async ValueTask<FrozenSet<ContentFolderModel>> GetContentFolders(string profileName, IEnumerable<(string, string)> defaultFolders, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(profileName))
                profileName = ContentProfileModel.Default.Name;

            ContentProfileModel contentProfile = await Load(profileName, cancellationToken).ConfigureAwait(false);

            if (null == contentProfile)
            {
                contentProfile = await Create(profileName, cancellationToken).ConfigureAwait(false);

                if (contentProfile == ContentProfileModel.Default && contentProfile.Count == 0 && defaultFolders != null)
                {
                    contentProfile = await UpdateFolders(contentProfile, defaultFolders, cancellationToken).ConfigureAwait(false);
                }
            }
            return contentProfile?.ToFrozenSet();
        }
    }
}
