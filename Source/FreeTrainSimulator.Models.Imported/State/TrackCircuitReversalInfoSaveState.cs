using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class TrackCircuitReversalInfoSaveState : SaveStateBase
    {
        public bool Valid { get; set; }
        public int LastDivergeIndex { get; set; }
        public int FirstDivergeIndex { get; set; }
        public int DivergeSectorIndex { get; set; }
        public float DivergeOffset { get; set; }
        public bool SignalAvailable { get; set; }
        public bool SignalUsed { get; set; }
        public int LastSignalIndex { get; set; }
        public int FirstSignalIndex { get; set; }
        public int SignalSectorIndex { get; set; }
        public float SignalOffset { get; set; }
        public float ReverseReversalOffset { get; set; }
        public int ReversalIndex { get; set; }
        public int ReversalSectionIndex { get; set; }
        public bool ReversalActionInserted { get; set; }

    }
}
