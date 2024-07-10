using System.Collections.ObjectModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class SignalSaveState: SaveStateBase
    {
        public int SignalIndex { get; set; }
        public TrackCircuitPartialPathRouteSaveState SignalRoute {  get; set; }
        public int EnabledTrainNumber { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<int> NextActiveSignals { get; set; }
        public int TrainRouteIndex { get; set; }
        public SignalHoldState HoldState { get; set; }
        public Collection<int>  JunctionsPassed { get; set; }
        public bool FullRoute { get; set; }
        public bool AllowPartialRoute { get; set; }
        public bool PropagatedAhead { get; set; }
        public bool PropagatedPreviously {  get; set; }            
        public bool ForcePropagationOnApproachControl { get; set; }
        public int SignalNumClearAheadActive { get; set; }
        public int RequestedNumClearAhead { get; set; }
        public bool ApproachControlCleared { get; set; }
        public bool ApproachControlSet {  get; set; }
        public bool ClaimLocked { get; set; }
        public SignalPermission OverridePermission { get; set; }
        public Collection<(int, int)> LockedTrains { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
