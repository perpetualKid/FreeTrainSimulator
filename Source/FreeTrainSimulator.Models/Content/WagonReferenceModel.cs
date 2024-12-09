using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record WagonReferenceModel: ModelBase, IFileResolve
    {
        static string IFileResolve.SubFolder => "Wagons";
        static string IFileResolve.DefaultExtension => ".wagon";

        public override FolderModel Parent => _parent as FolderModel;

        public TrainCarType TrainCarType { get; init; } 
        public string Description { get; init; }
        // Unique Id within the current trainset
        public int Uid { get; init; }
        public  bool Reverse { get; init; }
        public string Reference { get; init; }
    }
}
