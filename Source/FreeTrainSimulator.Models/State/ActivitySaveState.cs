using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.State
{
    [MemoryPackable]
    public sealed partial class ActivitySaveState : SaveStateBase
    {
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<ActivityTaskSaveState> Tasks { get; set; }
        public int CurrentTask { get; set; }
        public double PreviousTrainSpeed { get; set; }
        public bool Completed { get; set; }
        public bool Succeeded { get; set; }
        public int StartTime { get; set; }
        public Collection<ActivityEventSaveState> Events { get; set; }
        public int TriggeredEvent { get; set; }
        public bool LogStationStops { get; set; }
        public string StationStopFile { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
