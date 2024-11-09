using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record WagonReferenceModel: ModelBase<WagonReferenceModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".wagon";
            subFolder = ".Wagons";
        }

        public override FolderModel Parent => _parent as FolderModel;

        public TrainCarType TrainCarType { get; init; } 
        public string Description { get; init; }
        // Unique Id within the current trainset
        public int Uid { get; init; }
        public  bool Reverse { get; init; }
        public string Reference { get; init; }
    }
}
