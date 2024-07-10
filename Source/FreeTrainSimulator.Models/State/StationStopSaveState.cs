using System.Collections.Generic;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class StationStopSaveState : SaveStateBase
    {
        public StationStopType StationStopType { get; set; }
        public int PlatformReference { get; set; }
        public int RouteIndex { get; set; }
        public int SubrouteIndex { get; set; }
        public int TrackCircuitSectionIndex { get; set; }
        public TrackDirection TrackDirection { get; set; }
        public int ExitSignal { get; set; }
        public bool HoldSignal { get; set; }
        public bool NoWaitSignal { get; set; }
        public bool NoClaimAllowed { get; set; }
        public bool CallOnAllowed { get; set; }
        public float StopOffset { get; set; }
        public int ArrivalTime { get; set; }
        public int DepartureTime { get; set; }
        public double ActualArrival { get; set; }
        public double ActualDeparture { get; set; }
        public bool StationStopPassed { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<int> ConnectionsWaiting { get; set; }
        public Dictionary<int, int> ConnectionsAwaited { get; set; }
        public Dictionary<int, WaitInfoSaveState> ConnnectionDetails { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
        public int? ActualMinStopTime { get; set; }
        public float? KeepClearFront { get; set; }
        public float? KeepClearRear { get; set; }
        public bool TerminalStop { get; set; }
        public bool ForcePosition { get; set; }
        public bool CloseupSignal { get; set; }
        public bool Closeup { get; set; }
        public bool RestrictPlatformToSignal { get; set; }
        public bool ExtendPlatformToSignal { get; set; }
        public bool EndStop { get; set; }
    }
}
