using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class AttachInfoSaveState : SaveStateBase
    {
        public int TrainNumber { get; set; }
        public string TrainName { get; set; }
        public int StationPlatformReference { get; set; }
        public bool FirstToArrive { get; set; }
        public bool Reverse { get; set; }
        public bool Valid { get; set; }
        public bool ReadyToAttach { get; set; }
    }
}
