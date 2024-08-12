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
        private const string DefaultProfileName = "Default";

        public static ContentProfileModel DefaultProfile { get; private set; } = new ContentProfileModel(DefaultProfileName);

        public static async ValueTask<ContentProfileModel> Get(string profileName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(profileName) || (string.Equals(profileName, DefaultProfileName, StringComparison.OrdinalIgnoreCase)))
            {
                if (DefaultProfile?.Initialized ?? false) //FilePath is set once loaded from a file
                    return DefaultProfile; //already initialized default model, just returning that instance

                return DefaultProfile = await FromFile<ContentProfileModel>(DefaultProfileName, null, cancellationToken).ConfigureAwait(false);
            }
            else
                //return the specific profile instance if exists)
                return await FromFile<ContentProfileModel>(profileName, null, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<ContentProfileModel> Setup(string profileName, CancellationToken cancellationToken)
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
            ContentProfileModel contentProfile = await Get(profileName, cancellationToken).ConfigureAwait(false);

            if (null == contentProfile)
            {
                contentProfile = await Setup(profileName, cancellationToken).ConfigureAwait(false);

                if (contentProfile == DefaultProfile && contentProfile.Count == 0 && defaultFolders != null)
                {
                    contentProfile = await UpdateFolders(contentProfile, defaultFolders, cancellationToken).ConfigureAwait(false);
                }
            }
            return contentProfile?.ToFrozenSet();
        }
    }
}
