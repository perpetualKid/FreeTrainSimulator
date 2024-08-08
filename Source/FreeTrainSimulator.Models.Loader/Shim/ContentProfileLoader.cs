using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public class ContentProfileLoader : LoaderBase<ContentProfileModel>
    {
        public static async ValueTask<ContentProfileModel> Load(CancellationToken cancellationToken)
        {
            return await Load(ContentProfileModel.Default.Name, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<ContentProfileModel> Load(string profileName, CancellationToken cancellationToken)
        {
            return await FromFile<ContentProfileModel>(profileName, null, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<FrozenSet<ContentFolderModel>> GetContentFolders(string profileName, IEnumerable<(string, string)> defaultFolders, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(profileName))
                profileName = ContentProfileModel.Default.Name;

            ContentProfileModel contentProfile = await Load(profileName, cancellationToken).ConfigureAwait(false);

            if (null == contentProfile)
            {
                contentProfile = profileName == ContentProfileModel.Default.Name ? ContentProfileModel.Default : new ContentProfileModel(profileName);
                contentProfile.Initialize(FileResolver.ContentProfileFile(profileName));

                string directory = FileResolver.ContentProfileDirectory(profileName);
                if (!Directory.Exists(directory))
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.Message);
                        throw;
                    }
                }
                if (contentProfile == ContentProfileModel.Default && contentProfile.Count == 0 && defaultFolders != null)
                {
                    foreach ((string name, string path) in defaultFolders)
                    {
                        contentProfile.Add(new ContentFolderModel(name, path, contentProfile));

                        if (cancellationToken.IsCancellationRequested)
                            return FrozenSet<ContentFolderModel>.Empty;
                    }
                    await ToFile(FileResolver.ContentProfileFile(contentProfile.Name), contentProfile, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (ContentFolderModel contentFolder in contentProfile ?? Enumerable.Empty<ContentFolderModel>())
            {
                contentFolder.Initialize(FileResolver.ContentFolderFile(profileName, contentFolder.Name));
            }
            return contentProfile?.ToFrozenSet();
        }


    }
}
