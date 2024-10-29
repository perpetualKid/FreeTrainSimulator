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
        }

        public override WagonSetModel Parent => _parent as WagonSetModel;

        public TrainCarType TrainCarType { get; init; } 
        // Unique Id within the current trainset
        public int Uid { get; init; }
        public  bool Reverse { get; init; }
        public string Reference { get; init; }
    }
}
