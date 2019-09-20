using System;

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

    #region Path
    // This relates to TrPathFlags, which is not always present in .pat file
    // Bit 0 - connected pdp-entry references a reversal-point (1/x1)
    // Bit 1 - waiting point (2/x2)
    // Bit 2 - intermediate point between switches (4/x4)
    // Bit 3 - 'other exit' is used (8/x8)
    // Bit 4 - 'optional Route' active (16/x10)
     [Flags]
    public enum PathFlags: uint
    {
        None = 0x0,
        ReversalPoint = 1 << 0,
        WaitPoint = 1 << 1,
        IntermediatePoint = 1 << 2,
        OtherExit = 1 << 3,
        OptionalRoute = 1 << 4,
        NotPlayerPath = 1 << 5,
    }

    #endregion

    #region AceFile
    [Flags]
    public enum SimisAceFormatOptions
    {
        Default = 0,
        MipMaps = 0x01,
        RawData = 0x10,
    }

    public enum SimisAceChannelId
    {
        Mask = 2,
        Red = 3,
        Green = 4,
        Blue = 5,
        Alpha = 6,
    }

    #endregion

    #region Light
    /// <summary>
    /// Specifies whether a wagon light is glow (simple light texture) or cone (projected light cone).
    /// </summary>
    public enum LightType
    {
        Glow,
        Cone,
    }

    /// <summary>
    /// Specifies in which headlight positions (off, dim, bright) the wagon light is illuminated.
    /// </summary>
    public enum LightHeadlightCondition
    {
        Ignore,
        Off,
        Dim,
        Bright,
        DimBright, // MSTSBin
        OffBright, // MSTSBin
        OffDim, // MSTSBin
        // TODO: DimBright?, // MSTSBin labels this the same as DimBright. Not sure what it means.
    }

    /// <summary>
    /// Specifies on which units of a consist (first, middle, last) the wagon light is illuminated.
    /// </summary>
    public enum LightUnitCondition
    {
        Ignore,
        Middle,
        First,
        Last,
        LastRev, // MSTSBin
        FirstRev, // MSTSBin
    }

    /// <summary>
    /// Specifies in which penalty states (no, yes) the wagon light is illuminated.
    /// </summary>
    public enum LightPenaltyCondition
    {
        Ignore,
        No,
        Yes,
    }

    /// <summary>
    /// Specifies on which types of trains (AI, player) the wagon light is illuminated.
    /// </summary>
    public enum LightControlCondition
    {
        Ignore,
        AI,
        Player,
    }

    /// <summary>
    /// Specifies in which in-service states (no, yes) the wagon light is illuminated.
    /// </summary>
    public enum LightServiceCondition
    {
        Ignore,
        No,
        Yes,
    }

    /// <summary>
    /// Specifies during which times of day (day, night) the wagon light is illuminated.
    /// </summary>
    public enum LightTimeOfDayCondition
    {
        Ignore,
        Day,
        Night,
    }

    /// <summary>
    /// Specifies in which weather conditions (clear, rain, snow) the wagon light is illuminated.
    /// </summary>
    public enum LightWeatherCondition
    {
        Ignore,
        Clear,
        Rain,
        Snow,
    }

    /// <summary>
    /// Specifies on which units of a consist by coupling (front, rear, both) the wagon light is illuminated.
    /// </summary>
    public enum LightCouplingCondition
    {
        Ignore,
        Front,
        Rear,
        Both,
    }
    #endregion

    #region Activity
    public enum OrtsActivitySoundFileType
    {
        None,
        Everywhere,
        Cab,
        Pass,
        Ground,
        Location
    }
    #endregion

}
