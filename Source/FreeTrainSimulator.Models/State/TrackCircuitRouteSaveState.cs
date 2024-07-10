using System.Collections.ObjectModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TrackCircuitRouteElementAlternativePathSaveState : SaveStateBase
    {
        public int PathIndex { get; set; }
        public int TrackCircuitSectionIndex { get; set; }
    }

    [MemoryPackable]
    public sealed partial class TrackCircuitRouteElementSaveState : SaveStateBase
    {
        public int TrackCircuitSectionIndex { get; set; }
        public TrackDirection Direction { get; set; }
#pragma warning disable CA1819 // Properties should not return arrays
        public TrackDirection[] OutPin { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays
        public TrackCircuitRouteElementAlternativePathSaveState AlternativePathStart { get; set; }
        public TrackCircuitRouteElementAlternativePathSaveState AlternativePathEnd { get; set; }
        public bool FacingPoint { get; set; }
        public int AlternativePathIndex { get; set; }
        public int MovingTableApproachPath { get; set; }
    }

    [MemoryPackable]
    public sealed partial class TrackCircuitPartialPathRouteSaveState : SaveStateBase
    {
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<TrackCircuitRouteElementSaveState> RouteElements { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
