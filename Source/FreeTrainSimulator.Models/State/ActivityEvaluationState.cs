using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class ActivityEvaluationState: SaveStateBase
    {
        public int CouplerBreaks { get; set; }
        public int TravellingTooFast { get; set; }
        public int TrainOverTurned { get; set; }
        public int BrakedHoseSnapped { get; set; }
        public double DistanceTravelled { get; set; }
        public int FullTrainBrakeUnder8kmh { get; set; }
        public int FullBrakeAbove16kmh { get; set; }
        public int OverSpeedCoupling { get; set; }
        public int EmergencyButtonStopped { get; set; }
        public int EmergencyButtonMoving { get; set; }
        public int OverSpeed { get; set; }
        public double OverSpeedInitialTime { get; set; }
        public double OverSpeedTime { get; set; }
        public int DepartBeforeBoarding { get; set; }
        public bool AutoPilotRunning { get; set; }
        public double AutoPilotInitialTime { get; set; }
        public double AutoPilotTime { get; set; }

    }
}
