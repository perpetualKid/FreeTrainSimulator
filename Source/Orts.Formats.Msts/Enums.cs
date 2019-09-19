namespace Orts.Formats.Msts
{
    #region Activity
    public enum EventType
    {
        AllStops = 0,
        AssembleTrain,
        AssembleTrainAtLocation,
        DropOffWagonsAtLocation,
        PickUpPassengers,
        PickUpWagons,
        ReachSpeed
    }

    public enum ActivityMode
    {
        IntroductoryTrainRide = 0,
        Player = 2,
        Tutorial = 3,
    }

    public enum SeasonType
    {
        Spring = 0,
        Summer,
        Autumn,
        Winter
    }

    public enum WeatherType
    {
        Clear = 0, Snow, Rain
    }

    public enum Difficulty
    {
        Easy = 0, Medium, Hard
    }
    #endregion

    #region Signalling
    /// <summary>
    /// Describe the various states a block (roughly a region between two signals) can be in.
    /// </summary>
    public enum MstsBlockState
    {
        /// <summary>Block ahead is clear and accesible</summary>
        CLEAR,
        /// <summary>Block ahead is occupied by one or more wagons/locos not moving in opposite direction</summary>
        OCCUPIED,
        /// <summary>Block ahead is impassable due to the state of a switch or occupied by moving train or not accesible</summary>
        JN_OBSTRUCTED,
    }

    /// <summary>
    /// Describe the various aspects (or signal indication states) that MSTS signals can have.
    /// Within MSTS known as SIGASP_ values.  
    /// Note: They are in order from most restrictive to least restrictive.
    /// </summary>
    public enum MstsSignalAspect
    {
        /// <summary>Stop (absolute)</summary>
        STOP,
        /// <summary>Stop and proceed</summary>
        STOP_AND_PROCEED,
        /// <summary>Restricting</summary>
        RESTRICTING,
        /// <summary>Final caution before 'stop' or 'stop and proceed'</summary>
        APPROACH_1,
        /// <summary>Advanced caution</summary>
        APPROACH_2,
        /// <summary>Least restrictive advanced caution</summary>
        APPROACH_3,
        /// <summary>Clear to next signal</summary>
        CLEAR_1,
        /// <summary>Clear to next signal (least restrictive)</summary>
        CLEAR_2,
        /// <summary>Signal aspect is unknown (possibly not yet defined)</summary>
        UNKNOWN,
    }

    /// <summary>
    /// Describe the function of a particular signal head.
    /// Only SIGFN_NORMAL signal heads will require a train to take action (e.g. to stop).  
    /// The other values act only as categories for signal types to belong to.
    /// Within MSTS known as SIGFN_ values.  
    /// </summary>
    public enum MstsSignalFunction
    {
        /// <summary>Signal head showing primary indication</summary>
        NORMAL,
        /// <summary>Distance signal head</summary>
        DISTANCE,
        /// <summary>Repeater signal head</summary>
        REPEATER,
        /// <summary>Shunting signal head</summary>
        SHUNTING,
        /// <summary>Signal is informational only e.g. direction lights</summary>
        INFO,
        /// <summary>Speedpost signal (not part of MSTS SIGFN_)</summary>
        SPEED,
        /// <summary>Alerting function not part of MSTS SIGFN_)</summary>
        ALERT,
        /// <summary>Unknown (or undefined) signal type</summary>
        UNKNOWN, // needs to be last because some code depends this for looping. That should be changed of course.
    }
    #endregion
}
