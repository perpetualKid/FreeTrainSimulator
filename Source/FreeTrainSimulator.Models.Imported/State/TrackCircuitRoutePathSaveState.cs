using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.State
{
    [MemoryPackable]
    public sealed partial class TrackCircuitRoutePathSaveState : SaveStateBase
    {
        public int ActivePath { get; set; }
        public int ActiveAlternativePath { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<TrackCircuitPartialPathRouteSaveState> RoutePaths { get; set; }
        public Collection<TrackCircuitPartialPathRouteSaveState> AlternativePaths { get; set; }
        public Collection<int[]> Waitpoints { get; set; }
        public Collection<int> LoopEnd { get; set; }
        public int OriginalSubPath { get; set; }
        public Collection<TrackCircuitReversalInfoSaveState> ReversalInfoSaveStates { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
