using System.ComponentModel;

namespace Orts.Simulation
{
    public static class FourLetterEnumExtension
    {
        public static string FourLetter(this AiMovementState movementState)
        {
            return movementState switch
            {
                AiMovementState.Static => "STIC",
                AiMovementState.Init => "INIT",
                AiMovementState.Stopped => "STOP",
                AiMovementState.StationStop => "STAT",
                AiMovementState.Braking => "BRAK",
                AiMovementState.Accelerating => "ACCL",
                AiMovementState.Following => "FLLW",
                AiMovementState.Running => "RUNN",
                AiMovementState.ApproachingEndOfPath => "AEOP",
                AiMovementState.StoppedExisting => "STPE",
                AiMovementState.InitAction => "INIA",
                AiMovementState.HandleAction => "HANA",
                AiMovementState.Suspended => "SUSP",
                AiMovementState.Frozen => "FROZ",
                AiMovementState.Turntable => "TURN",
                _ => "NONE",
            };
        }
    }

    public enum SignalItemFindState
    {
        None = 0,
        Item = 1,
        EndOfTrack = -1,
        PassedDanger = -2,
        PassedMaximumDistance = -3,
        TdbError = -4,
        EndOfAuthority = -5,
        EndOfPath = -6,
    }

    public enum SignalItemType
    {
        Any,
        Signal,
        SpeedLimit,
    }

    public enum SpeedItemType
    {
        Standard = 0,
        TemporaryRestrictionStart = 1,
        TemporaryRestrictionStartResume = 2,
    }

    public enum TrainPathItemType
    {
        Signal,
        SpeedSignal,
        Speedpost,
        Station,
        Authority,
        Reversal,
        OutOfControl,
        WaitingPoint,
        Milepost,
        FacingSwitch,
        TrailingSwitch,
        GenericSignal,
        Tunnel,
    }

    public enum OutOfControlReason
    {
        [Description("SPAD")] PassedAtDanger,   //SignalPassedAtDanger
        [Description("SPAD-Rear")] RearPassedAtDanger,
        [Description("Misalg Sw")] MisalignedSwitch,
        [Description("Off Auth")] OutOfAuthority,
        [Description("Off Path")] OutOfPath,
        [Description("Slip Path")] SlippedIntoPath,
        [Description("Slip Track")] SlippedToEndOfTrack,
        [Description("Off Track")] OutOfTrack,
        [Description("Slip Turn")] SlippedIntoTurnTable,
        [Description("Undefined")] UnDefined
    }

    public enum EndAuthorityType
    {
#pragma warning disable CA1700 // Do not name enum values 'Reserved'
        [Description("End Trck")] EndOfTrack,
        [Description("End Path")] EndOfPath,
        [Description("Switch")] ReservedSwitch,
        [Description("TrainAhd")] TrainAhead,
        [Description("Max Dist")] MaxDistance,
        [Description("Loop")] Loop,
        [Description("Signal")] Signal,                                       // in Manual mode only
        [Description("End Auth")] EndOfAuthority,                             // when moving backward in Auto mode
        [Description("No Path")] NoPathReserved,
#pragma warning restore CA1700 // Do not name enum values 'Reserved'
    }

    public enum TrackCircuitType
    {
        Normal,
        Junction,
        Crossover,
        EndOfTrack,
        Empty,
    }

    public enum AuxWagonType
    {
        //keep in sync with WagonType enum to allow mapping for Engine and Tender 
        Unknown,
        Engine,
        Tender,
        AuxiliaryTender,
    }

    public enum AiMovementState
    {
        Static,
        Init,
        Stopped,
        StationStop,
        Braking,
        Accelerating,
        Following,
        Running,
        ApproachingEndOfPath,
        StoppedExisting,
        InitAction,
        HandleAction,
        Suspended,
        Frozen,
        Turntable,
        Unknown
    }

    public enum AiStartMovement
    {
        SignalCleared,
        SignalRestricted,
        FollowTrain,
        EndStationStop,
        NewTrain,
        PathAction,
        Turntable,
        Reset             // used to clear state
    }

    public enum ActivityEventType
    {
        Timer,
        TrainStart,
        TrainStop,
        Couple,
        Uncouple
    }

    public enum TrainCarLocation
    {
        Front,
        Rear,
    }

    public enum FreightAnimationType
    {
        Default,
        Container
    }

    public enum ContainerStatus
    {
        OnEarth,
        Loading,
        Unloading,
        WaitingForLoading,
        WaitingForUnloading
    }

    public enum CruiseControlLogic
    {
        None,
        Full,
        SpeedOnly
    }

    public enum ControllerPosition
    {
        Default,
        Stable,
        SpringLoadedForwards,
        SpringLoadedForwardsImmediately,
        SpringLoadedBackwards,
        SpringLoadedBackwardsImmediately,
        CCNeedIncreaseAfterAnyBrake
    }
    public enum MovingTableState
    {
        WaitingMovingTableAvailability,
        WaitingAccessToMovingTable,
        AccessToMovingTable,
        AccessOnMovingTable,
        WaitingStorageToMovingTable,
        StorageToMovingTable,
        StorageOnMovingTable,
        Completed,
        Inactive,
    }

    public enum MovingTableAction
    {
        FromAccess,
        FromStorage,
        Turning,
        Undefined,
    }
}
