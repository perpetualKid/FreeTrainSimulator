using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class WaitInfoSaveState : SaveStateBase
    {
        public WaitInfoType WaitInfoType { get; set; }
        public bool ActiveWait {  get; set; }
        public int ActiveRouteIndex { get; set; }
        public int ActiveSubrouteIndex { get; set; }
        public int ActiveSectionIndex { get; set; }
        public int TrainNumber { get; set; }
        public int? MaxDelay { get; set; }
        public int? OwnDelay { get; set; }
        public bool? NotStarted { get; set; }
        public bool? AtStart { get; set; }
        public int? WaitTrigger { get; set; }
        public int? WaitEndTrigger { get; set; }
        public int WaitingTrainSubpathIndex { get; set; }
        public int WaitingTrainRouteIndex { get; set; }
        public int StationIndex { get; set; }
        public int? HoldTime { get; set; }
        public TrackCircuitPartialPathRouteSaveState CheckPath { get; set; }
        public PathCheckDirection CheckDirection { get; set; }
             

    }
}
