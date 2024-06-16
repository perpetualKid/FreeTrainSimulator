using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

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
        public double InitialSpeed { get; set; }
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
        public TrackCircuitRoutePathSaveState RoutePathSaveState { get; set; }
        public Collection<int> OccupiedTracks { get; set; }
        public Collection<int> HoldingSignals { get; set; }
        public Collection<StationStopSaveState> StationStopSaveStates { get; set; }
        public StationStopSaveState LastStationStop { get; set; }
        public bool AtStation { get; set; }
        public bool ReadyToDepart { get; set; }
        public bool CheckStations { get; set; }
        public int AttachToTrainNumber { get; set; }
        public string DisplayMessage { get; set; }
        public TimeSpan? Delay { get; set; }
        public Dictionary<int, float> PassedSignalSpeeds { get; set; }
#pragma warning disable CA1819 // Properties should not return arrays
        public int[] LastPassedSignals { get; set; }
        public int LoopSection { get; set; }
        public AuthoritySaveState[] AuthoritySaveStates { get; set; }
#pragma warning restore CA1819 // Properties should not return arrays
        public int ServiceTrafficTime { get; set; }
        public Collection<ServiceTrafficItemSaveState> ServiceTrafficItemSaveStates { get; set; }
        public TrainControlMode TrainControlMode { get; set; }
        public OutOfControlReason OutOfControlReason { get; set; }
        public double DistanceTravelled { get; set; }
        public Collection<TrackCircuitPositionSaveState> PresentPositions { get; set; }
        public Collection<TrackCircuitPositionSaveState> PreviousPositions { get; set; }
        public Collection<ActionItemSaveState> DistanceTravelledActions { get; set; }
        public bool Pathless { get; set; }
        public Dictionary<int, List<Dictionary<int, int>>> DeadlockInfo { get; set; }
        public Collection<AuxActionRefSaveState> AuxActionsSaveStates { get; set; }
        public AiTrainSaveState AiTrainSaveState { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }

    [MemoryPackable]
    public sealed partial class AiTrainSaveState : SaveStateBase
    {
        public int? StartTime { get; set; }
        public float MaxAcceleration { get; set; }
        public float MaxDeceleration { get; set; }
        public bool PowerState { get; set; }
        public int Alpha10 { get; set; }
        public AiMovementState MovementState { get; set; }
        public float Efficiency { get; set; }
        public float MaxVelocity { get; set; }
        public bool UnconditionalAttach { get; set; }
        public float DoorsCloseAdvance { get; set; }
        public float DoorsOpenDelay { get; set; }
        public LevelCrossingHornPattern LevelCrossingHornPattern { get; set; }
        public Collection<TrafficItemSaveState> TrafficItemSaveStates { get; set; }
    }
}
