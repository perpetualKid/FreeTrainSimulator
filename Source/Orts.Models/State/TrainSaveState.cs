using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;
using Orts.Formats.Msts;

namespace Orts.Models.State
{
    [MemoryPackable]
    public sealed partial class TrainSaveState : SaveStateBase
    {
#pragma warning disable CA2227 // Collection properties should be read only
        public Collection<TrainCarSaveState> TrainCars { get; set; }
        public int TrainNumber { get; set; }
        public string TrainName { get; set; }
        public double Speed { get; set; }
        public double Acceleration { get; set; }
        public TrainType TrainType { get; set; }
        public MidpointDirection MultiUnitDirection { get; set; }
        public float MultiUnitThrottle { get; set; }
        public int MultiUnitGearboxIndex { get; set; }
        public float MultiUnitDynamicBrake { get; set; }
        public float DistributedPowerThrottle { get; set; }
        public float DistributedPowerDynamicBrake { get; set; }
        public DistributedPowerMode DistributedPowerMode { get; set; }
        public TrainBrakeSaveState TrainBrakeSaveState { get; set; }
        public TravellerSaveState TravellerSaveState { get; set; }
        public int LeadLocomotive { get; set; }
        public float AiBrake { get; set; }
        public float SlipperySpotDistance { get; set; }
        public float SlipperySpotLength { get; set; }
        public float TrainMaximumSpeed { get; set; }
        public float AllowedMaximumSpeed { get; set; }
        public float AllowedSignalMaximumSpeed { get; set; }
        public float AllowedLimitMaximumSpeed { get; set; }
        public float AllowedTemporaryLimitMaximumSpeed { get; set; }
        public float AllowedAbsoluteSignalMaximumSpeed { get; set; }
        public float AllowedAbsoluteLimitMaximumSpeed { get; set; }
        public float AllowedAbsoluteTemporaryLimitMaximumSpeed { get; set; }
        public double BrakingTime { get; set; }
        public double ContinuousBrakingTime { get; set; }
        public double RunningTime { get; set; }
        public int IncorporatedTrainNumber { get; set; }
        public int IncorporatingTrainNumber { get; set; }
        public bool AuxTenderCoupled { get; set; }
        public bool TrainTilting { get; set; }
        public bool ClaimState { get; set; }
        public bool EvaluateTrainSpeed { get; set; }
        public int EvaluationIntervall { get; set; }
        public EvaluationLogContents EvaluationLogContents { get; set; }
        public string EvaluationFile { get; set; }
        public Collection<TrackCircuitPartialPathRouteSaveState> ValidRoutes { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
