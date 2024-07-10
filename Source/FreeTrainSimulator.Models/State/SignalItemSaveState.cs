using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class SignalItemSaveState : SaveStateBase
    {
        public SignalItemType SignalItemType { get; set; }
        public SignalItemFindState SignalItemState { get; set; }
        public int SignalIndex { get; set; }
        public float DistanceFound { get; set; }
        public float DistanceTrain { get; set; }
        public float DistanceObject { get; set; }
        public float PassengerSpeed { get; set; }
        public float FreightSpeed { get; set; }
        public bool Flag { get; set; }
        public float ActualSpeed { get; set; }
        public bool Processed { get; set; }
    }
}
