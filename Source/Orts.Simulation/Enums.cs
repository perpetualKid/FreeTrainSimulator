using FreeTrainSimulator.Common;

using Orts.Common;

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
}
