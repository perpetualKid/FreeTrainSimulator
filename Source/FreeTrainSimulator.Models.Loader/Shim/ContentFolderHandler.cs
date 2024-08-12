using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent;
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

        public static async ValueTask<ContentFolderModel> Get(string folderName, ContentProfileModel parent, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parent, nameof(parent));
            if (!parent.Initialized)
            {
                Trace.TraceWarning($"Uninitialized parent {nameof(ContentProfileModel)}[{parent.Name}]");
                parent = await ContentProfileHandler.Get(parent.Name, cancellationToken).ConfigureAwait(false);
            }

            return parent.Where((folder) => string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }
    }
}
