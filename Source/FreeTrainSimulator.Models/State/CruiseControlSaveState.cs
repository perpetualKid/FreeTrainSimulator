using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;

using MemoryPack;

namespace FreeTrainSimulator.Models.State
{
    [MemoryPackable]
    public sealed partial class CruiseControlSaveState : SaveStateBase
    {
        public float SelectetdSpeed { get; set; }
        public bool MaxForceDecreasing { get; set; }
        public bool MaxForceIncreasing { get; set; }
        public bool RestrictedRegionOdometerEnabled { get; set; }
        public double RestrictedRegionOdometerValue { get; set; }
        public float RestrictedRegionSelectedSpeed { get; set; }
        public float MaxAcceleration { get; set; }
        public int AxleNumber { get; set; }
        public bool DynamicBrakePriority { get; set; }
        public SpeedRegulatorMode SpeedRegulatorMode { get; set; }
        public SpeedSelectorMode SpeedSelectorMode { get; set; }
        public float TrainBrakePercent { get; set; }
        public int TrainLength { get; set; }
        public bool TrainBrakeActive { get; set; }



    }
}
