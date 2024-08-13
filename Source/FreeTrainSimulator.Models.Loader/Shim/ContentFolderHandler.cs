using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public sealed class ContentFolderHandler: ContentHandlerBase<FolderModel, FolderModel>
    {
        public static async ValueTask<FolderModel> Create(string folderName, string repositoryPath, ProfileModel profile, CancellationToken cancellationToken)
        {
            FolderModel contentFolder = new FolderModel(folderName, repositoryPath, profile);
            await Create(contentFolder, profile, false, true, cancellationToken).ConfigureAwait(false);
            return contentFolder;
        }

        public static ValueTask<FolderModel> Get(string folderName, ProfileModel parent, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parent, nameof(parent));

            return ValueTask.FromResult(parent.ContentFolders.Where((folder) => string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault());
        }
    }
}
