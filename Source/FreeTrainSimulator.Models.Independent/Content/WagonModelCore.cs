using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record WagonModelCore: ModelBase<WagonModelCore>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".wagon";
        }
        public override WagonSetModel Parent => _parent as WagonSetModel;

        public TrainCarType TrainCarType { get; init; } 
        public string Description { get; init; }
        public string Comment { get; init; }
        // Unique Id within the current trainset
        public int Uid { get; init; }
        public  bool Flipped { get; init; }       
    }
}
