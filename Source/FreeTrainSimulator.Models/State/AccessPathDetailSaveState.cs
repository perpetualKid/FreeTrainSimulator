using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.State
{
    [MemoryPackable]
    public sealed partial class AccessPathDetailSaveState : SaveStateBase
    {
        public TrackCircuitPartialPathRouteSaveState AccessPath { get; set; }
        public TravellerSaveState AccessTraveller { get; set; }
        public string AccessPathName { get; set; }
        public int TableExitIndex { get; set; }
        public int TableVectorIndex { get; set; }
        public float TableMiddleEntry { get; set; }
        public float TableMiddleExit { get; set; }
        public float TableApproachOffset { get; set; }
    }
}
