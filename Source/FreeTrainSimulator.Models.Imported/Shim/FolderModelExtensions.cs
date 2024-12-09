using System;
using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Imported.Shim
{
    public static class FolderModelExtensions
    {
        public static readonly FolderModel MstsFolder = new FolderModel("Train Simulator", FolderStructure.MstsFolder, null);

        public static FolderModel TrainSimulatorFolder(this ProfileModel profileModel)
        {
            FolderModel mstsFolder = MstsFolder;
            mstsFolder.Initialize(profileModel);
            return mstsFolder;
        }
        public static FolderStructure.ContentFolder MstsContentFolder(this FolderModel folderModel) => FileResolver.ContentFolderResolver(folderModel).MstsContentFolder;
    }
}
