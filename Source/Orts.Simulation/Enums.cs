﻿using System.ComponentModel;

namespace Orts.Simulation
{
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
        [Description("SPAD")]PassedAtDanger,   //SignalPassedAtDanger
        [Description("SPAD-Rear")] RearPassedAtDanger,
        [Description("Misalg Sw")] MisalignedSwitch,
        [Description("Off Auth")] OutOfAuthority,
        [Description("Off Path")] OutOfPath,
        [Description("Splipped")] SlippedIntoPath,
        [Description("Slipped")] SlippedToEndOfTrack,
        [Description("Off Track")] OutOfTrack,
        [Description("Slip Turn")] SlippedIntoTurnTable,
        [Description("Undefined")] UnDefined
    }

    public enum EndAuthorityType
    {
#pragma warning disable CA1700 // Do not name enum values 'Reserved'
        [Description("End Trck")]EndOfTrack,
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

    public enum StationStopType
    {
        Station,
        Siding,
        Manual,
        WaitingPoint,
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
}
