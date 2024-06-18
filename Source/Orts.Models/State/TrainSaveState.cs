using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Common.Api;

using MemoryPack;

using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Models.Simplified;

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
#pragma warning restore CA2227 // Collection properties should be read only
        public AiTrainSaveState AiTrainSaveState { get; set; }
        public TimetableTrainSaveState TimetableTrainSaveState { get; set; }
    }

    [MemoryPackable]
    public sealed partial class AiTrainSaveState : SaveStateBase
    {
        public int PlayerLocomotiveIndex { get; set; }
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

    [MemoryPackable]
    public sealed partial class TimetableTrainSaveState : SaveStateBase
    {
        public bool CloseUpStabling { get; set; }
        public bool Created { get; set; }
        public string CreatedAhead { get; set; }
        public string CreatedFromPool { get; set; }
        public string CreatedInPool { get; set; }
        public string ConsistName { get; set; }
        public PoolExitDirection CreatePoolDirection { get; set; }
        public float MaxAccelerationPassenger { get; set; }
        public float MaxDecelerationPassenger { get; set; }
        public float MaxAccelerationFreight { get; set; }
        public float MaxDecelerationFreight { get; set; }
        public int? ActivationTime { get; set; }
        public bool TriggeredActivationRequired { get; set; }
        public Collection<TriggerActivationSaveState> ActivationTriggerSaveStates { get; set; }
        public Collection<WaitInfoSaveState> WaitInfoSaveStates { get; set; }
        public Dictionary<int, Collection<WaitInfoSaveState>> WaitInfoAnySaveStates { get; set; }
        public bool StableCallOn { get; set; }
        public int FormedTrainNumber { get; set; }
        public bool FormedStaticTrain { get; set; }
        public string ExitPool { get; set; }
        public int PoolAccessSection { get; set; }
        public int PoolStorageIndex { get; set; }
        public PoolExitDirection PoolExitDirection { get; set; }
        public int FormedFromTrainNumber { get; set; }
        public TimetableFormationCommand TimetableFormationCommand { get; set; }
        public int OriginalAiTrainNumber { get; set; }
        public bool InheritStationStop { get; set; }
        public bool FormsAtStation { get; set; }
        public AttachInfoSaveState AttachInfoSaveState { get; set; }
        public Dictionary<int, Collection<DetachInfoSaveState>> DetachInfoSaveStates { get; set; }
        public TimetableTurntableControlSaveState TrainOnTurntableSaveState { get; set; }
        public int[] ActiveDetaches { get; set; }
        public int DetachUnits { get; set; }
        public bool DetachPosition { get; set; }
        public bool DetachPending { get; set; }
        public Collection<int> PickupTrains { get; set; }
        public Collection<int> PickupStaticTrains { get; set; }
        public bool PickupStaticOnForms { get; set; }
        public bool PickupNeeded { get; set; }
        public Dictionary<int, TransferInfoSaveState> TransferStationDetailsSaveStates { get; set; }
        public Dictionary<int, Collection<TransferInfoSaveState>> TransferTrainDetailSateStates { get; set; }
        public Dictionary<int, Collection<int>> NeedAttach { get; set; }
        public Dictionary<int, Collection<int>> NeedStationTransfer { get; set; }
        public Dictionary<int, int> NeedTrainTransfer { get; set; }
        public DelayedStart[] DelayedStarts { get; set; }
        public float ReverseAddedDelay { get; set; }
        public AiStartMovement DelayedStartState { get; set; }
        public bool DelayedStart { get; set; }
        public float RemainingDelay { get; set; }
        public float?[] SpeedSettings { get; set; }
        public bool SpeedRestrictionActive { get; set; }
        public int? CruiseDelayMax { get; set; }
        public bool DriverOnlyOperation { get; set; }
        public bool ForceReversal { get; set; }
        public string Briefing { get; set; }
    }
}
