using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public sealed class ContentFolderHandler: ContentHandlerBase<ContentFolderModel>
    {
        public static async ValueTask<ContentFolderModel> Create(string folderName, string repositoryPath, ContentProfileModel profile, CancellationToken cancellationToken)
        {
            ContentFolderModel contentFolder = new ContentFolderModel(folderName, repositoryPath, profile);
            await Create(contentFolder, profile, false, true, cancellationToken).ConfigureAwait(false);
            return contentFolder;
        }
    }
}
