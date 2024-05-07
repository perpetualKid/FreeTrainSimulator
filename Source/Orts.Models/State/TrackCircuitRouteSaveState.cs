using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TrackCircuitRouteElementAlternativePathSaveState: SaveStateBase
    {
        public int PathIndex { get; set; }
        public int TrackCircuitSectionIndex { get; set; }
    }

    [MemoryPackable]
    public sealed partial class TrackCircuitRouteElementSaveState: SaveStateBase
    {
        public int TrackCircuitSectionIndex { get; set; }
        public TrackDirection Direction { get; set; }
        public TrackDirection[] OutPin { get; set; }
        public TrackCircuitRouteElementAlternativePathSaveState AlternativePathStart { get; set; }
        public TrackCircuitRouteElementAlternativePathSaveState AlternativePathEnd { get; set; }
        public bool FacingPoint {  get; set; }
        public int AlternativePathIndex { get; set; }
        public int MovingTableApproachPath {  get; set; }
    }

    [MemoryPackable]
    public sealed partial class TrackCircuitPartialPathRouteSaveState: SaveStateBase
    {
        public Collection<TrackCircuitRouteElementSaveState> RouteElements { get; set; }
    }
}
