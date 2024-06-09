using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class AuxActionRefSaveState: SaveStateBase
    {
        public AuxiliaryAction NextAction { get; set; }
        public float RequiredSpeed { get; set; }
        public float RequiredDistance { get; set; }
        public int RouteIndex { get; set; }
        public int SubRouteIndex { get; set; }
        public int TrackCircuitSectionIndex { get; set; }
        public Direction Direction { get; set; }
        public int TriggerDistance { get; set; }
        public bool GenericAction { get; set; }
        public int EndSignalIndex { get; set; }
        public int? Duration { get; set; }
        public int Delay { get; set; }
        public float BrakeSection { get; set; }
        public bool Absolute { get; set; }
        public bool WaitPointAction { get; set; }
        public LevelCrossingHornPattern LevelCrossingHornPattern { get; set; }
    }
}
