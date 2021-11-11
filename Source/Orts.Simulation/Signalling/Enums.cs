namespace Orts.Simulation.Signalling
{
    public enum Location
    {
        NearEnd,
        FarEnd,
    }

    public enum InternalBlockstate
    {
#pragma warning disable CA1700 // Do not name enum values 'Reserved'
        Reserved,                   // all sections reserved for requiring train       //
        Reservable,                 // all secetions clear and reservable for train    //
        OccupiedSameDirection,      // occupied by train moving in same direction      //
        ReservedOther,              // reserved for other train                        //
        ForcedWait,                 // train is forced to wait for other train         //
        OccupiedOppositeDirection,  // occupied by train moving in opposite direction  //
        Open,                       // sections are claimed and not accesible          //
        Blocked,                    // switch locked against train                     //
#pragma warning restore CA1700 // Do not name enum values 'Reserved'
    }

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public enum SignalPermission
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        Granted,
        Requested,
        Denied,
    }

    public enum SignalHoldState     // signal is locked in hold
    {
        None,                       // signal is clear
        StationStop,                // because of station stop
        ManualLock,                 // because of manual lock. 
        ManualPass,                 // Sometime you want to set a light green, especially in MP
        ManualApproach,             // Sometime to set approach, in MP again
        //PLEASE DO NOT CHANGE THE ORDER OF THESE ENUMS
    }

    public enum SignalEvent
    {
        RequestMostRestrictiveAspect,
        RequestApproachAspect,
        RequestLeastRestrictiveAspect,
    }
}
