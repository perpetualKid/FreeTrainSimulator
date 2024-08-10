using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public sealed class ContentFolderHandler: ContentHandlerBase<ContentFolderModel>
    {
        public static ValueTask<ContentFolderModel> Create(string folderName, string repositoryPath, ContentProfileModel profile, CancellationToken cancellationToken)
        {
            ContentFolderModel contentFolder = new ContentFolderModel(folderName, repositoryPath, profile);
            contentFolder.Initialize(ModelFileResolver<ContentFolderModel>.FilePath(contentFolder, profile), profile);

            string directory = ModelFileResolver<ContentFolderModel>.FolderPath(contentFolder);
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

            return ValueTask.FromResult(contentFolder);
        }
    }
}
