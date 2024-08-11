using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent;
using FreeTrainSimulator.Models.Independent.Content;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public sealed class ContentProfileHandler : ContentHandlerBase<ContentProfileModel>
    {
        public static async ValueTask<ContentProfileModel> Get(string profileName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(profileName) || (string.Equals(profileName == ContentProfileModel.Default.Name, StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrEmpty((ContentProfileModel.Default as IFileResolve).FilePath)) //FilePath is set once loaded from a file
                    return ContentProfileModel.Default; //already initialized default model, just returning that instance

                // else loading, updating the static default instance (can't replace), and return the default instance
                profileName = ContentProfileModel.Default.Name;
                ContentProfileModel result = await FromFile<ContentProfileModel>(profileName, null, cancellationToken).ConfigureAwait(false);
                ContentProfileModel.Default.Clear();
                foreach(ContentFolderModel contentFolder in result)
                    ContentProfileModel.Default.Add(contentFolder);
                ContentProfileModel.Default.Initialize(ModelFileResolver<ContentProfileModel>.FilePath(profileName, (ContentProfileModel)null) + SaveStateExtension, null);
                return ContentProfileModel.Default;
            }
            else
                //return the specific profile instance if exists)
                return await FromFile<ContentProfileModel>(profileName, null, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<ContentProfileModel> Setup(string profileName, CancellationToken cancellationToken)
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
            ContentProfileModel contentProfile = await Get(profileName, cancellationToken).ConfigureAwait(false);

            if (null == contentProfile)
            {
                contentProfile = await Setup(profileName, cancellationToken).ConfigureAwait(false);

                if (contentProfile == ContentProfileModel.Default && contentProfile.Count == 0 && defaultFolders != null)
                {
                    contentProfile = await UpdateFolders(contentProfile, defaultFolders, cancellationToken).ConfigureAwait(false);
                }
            }
            return contentProfile?.ToFrozenSet();
        }
    }
}
