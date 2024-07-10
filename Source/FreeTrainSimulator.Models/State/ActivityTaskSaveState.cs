using System;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class ActivityTaskSaveState : SaveStateBase
    {
        public bool? Completed { get; set; }
        public TimeSpan CompletedAt { get; set; }
        public string Message { get; set; }
        public TimeSpan ScheduledArrival { get; set; }
        public TimeSpan ScheduledDeparture { get; set; }
        public TimeSpan? ActualArrival { get; set; }
        public TimeSpan? ActualDeparture { get; set; }
        public int PlatformEnd1 { get; set; }
        public int PlatformEnd2 { get; set; }
        public double BooardingTime { get; set; }
        public double BoardingEndTime { get; set; }
        public int TimerCheck { get; set; }
        public bool Arrived { get; set; }
        public bool ReadyToDepart { get; set; }
        public float DistanceToSignal { get; set; }
    }
}
