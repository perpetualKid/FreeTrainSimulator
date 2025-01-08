using System;
using System.ComponentModel;

namespace Orts.Formats.Msts
{
#pragma warning disable CA1707 // Identifiers should not contain underscores
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
    #endregion

    #region Signalling
    /// <summary>
    /// Describe the various states a block (roughly a region between two signals) can be in.
    /// </summary>
    public enum SignalBlockState
    {
        /// <summary>Block ahead is clear and accesible</summary>
        Clear,
        /// <summary>Block ahead is occupied by one or more wagons/locos not moving in opposite direction</summary>
        Occupied,
        /// <summary>Block ahead is impassable due to the state of a switch or occupied by moving train or not accesible</summary>
        Jn_Obstructed,
    }

    /// <summary>
    /// Describe the various aspects (or signal indication states) that MSTS signals can have.
    /// Within MSTS known as SIGASP_ values.  
    /// Note: They are in order from most restrictive to least restrictive.
    /// </summary>
    public enum SignalAspectState
    {
        /// <summary>Stop (absolute)</summary>
        [Description("Stop")]
        Stop,
        /// <summary>Stop and proceed</summary>
        [Description("StopProceed")]
        Stop_And_Proceed,
        /// <summary>Restricting</summary>
        [Description("Restricting")]
        Restricting,
        /// <summary>Final caution before 'stop' or 'stop and proceed'</summary>
        [Description("Approach1")]
        Approach_1,
        /// <summary>Advanced caution</summary>
        [Description("Approach2")]
        Approach_2,
        /// <summary>Least restrictive advanced caution</summary>
        [Description("Approach3")]
        Approach_3,
        /// <summary>Clear to next signal</summary>
        [Description("Clear1")]
        Clear_1,
        /// <summary>Clear to next signal (least restrictive)</summary>
        [Description("Clear2")]
        Clear_2,
        /// <summary>Signal aspect is unknown (possibly not yet defined)</summary>
        [Description("Unknown")]
        Unknown,
    }

    /// <summary>
    /// List of allowed signal sub types, as defined by MSTS (SIGSUBT_ values)
    /// </summary>
    public enum SignalSubType
    {
        None = -1,
        Decor,
        Signal_Head,
        Dummy1,
        Dummy2,
        Number_Plate,
        Gradient_Plate,
        User1,
        User2,
        User3,
        User4,
    }

    /// <summary>
    /// Describe the function of a particular signal head.
    /// Only SIGFN_NORMAL signal heads will require a train to take action (e.g. to stop).  
    /// The other values act only as categories for signal types to belong to.
    /// Within MSTS known as SIGFN_ values.  
    /// </summary>
    public enum SignalFunction
    {
        /// <summary>Signal head showing primary indication</summary>
        Normal,
        /// <summary>Distance signal head</summary>
        Distance,
        /// <summary>Repeater signal head</summary>
        Repeater,
        /// <summary>Shunting signal head</summary>
        Shunting,
        /// <summary>Signal is informational only e.g. direction lights</summary>
        Info,
        /// <summary>Speedpost signal (not part of MSTS SIGFN_)</summary>
        Speed,
        /// <summary>Alerting function not part of MSTS SIGFN_)</summary>
        Alert,
        /// <summary>Unknown (or undefined) signal type</summary>
        Unknown, // needs to be last because some code depends this for looping. That should be changed of course.
    }
    #endregion

    #region Path
    public enum TrackNodeType
    {
        Track,
        Junction,
        End,
    }

    // This relates to TrPathFlags, which is not always present in .pat file
    // Bit 0 - connected pdp-entry references a reversal-point (1/x1)
    // Bit 1 - waiting point (2/x2)
    // Bit 2 - intermediate point between switches (4/x4)
    // Bit 3 - 'other exit' is used (8/x8)
    // Bit 4 - 'optional Route' active (16/x10)
    [Flags]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public enum PathFlags
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        None = 0x0,
        ReversalPoint = 1 << 0,
        WaitPoint = 1 << 1,
        IntermediatePoint = 1 << 2,
        OtherExit = 1 << 3,
        OptionalRoute = 1 << 4,
        NotPlayerPath = 1 << 5,
    }

    public enum PathNodeType
    {
        /// <summary>Node is a regular node on a junction</summary>
        Junction,
        /// <summary>Node is an intermediate point node </summary>
        Intermediate,
        /// <summary>Node is the start node </summary>
        Start,
        /// <summary>Node is the end node (not just the last node) </summary>
        End,
        /// <summary>Node is a wait/stop node</summary>
        Wait,
        /// <summary>Node is a reversal node</summary>
        Reversal,
        /// <summary>Temporary node for editing purposes</summary>
        Temporary,
    };

    #endregion

    #region AceFile
    [Flags]
    public enum SimisAceFormatOptions
    {
        None = 0,
        MipMaps = 0x01,
        RawData = 0x10,
    }

    public enum SimisAceChannelId
    {
        None = 0,
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

    /// <summary>
    /// Specifies if the light must be illuminated on if low voltage power supply is on or off.
    /// </summary>
    public enum LightBatteryCondition
    {
        Ignore,
        On,
        Off,
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

    public enum ActivationType
    {
        Activate,
        Deactivate,
    }
    #endregion

    #region CabView
    public enum CabViewControlType
    {
        None,
        Speedometer,
        Main_Res,
        Main_Res_Pipe,
        Eq_Res,
        Brake_Cyl,
        Brake_Pipe,
        Line_Voltage,
        AmMeter,
        AmMeter_Abs,
        Load_Meter,
        Throttle,
        Pantograph,
        Train_Brake,
        Friction_Brake,
        Engine_Brake,
        Brakeman_Brake,
        Dynamic_Brake,
        Dynamic_Brake_Display,
        Sanders,
        Wipers,
        Vacuum_Exhauster,
        Horn,
        Bell,
        Front_HLight,
        Direction,
        Aspect_Display,
        Throttle_Display,
        Cph_Display,
        Panto_Display,
        Direction_Display,
        Cp_Handle,
        Pantograph2,
        Clock,
        Sanding,
        Alerter_Display,
        Traction_Braking,
        Accelerometer,
        WheelSlip,
        Friction_Braking,
        Penalty_App,
        Emergency_Brake,
        Reset,
        Cab_Radio,
        OverSpeed,
        SpeedLim_Display,
        Fuel_Gauge,
        Whistle,
        Regulator,
        Cyl_Cocks,
        Blower,
        Steam_Inj1,
        Steam_Inj2,
        Orts_BlowDown_Valve,
        Dampers_Front,
        Dampers_Back,
        Steam_Heat,
        Water_Injector1,
        Water_Injector2,
        Small_Ejector,
        Steam_Pr,
        SteamChest_Pr,
        Tender_Water,
        Boiler_Water,
        Reverser_Plate,
        SteamHeat_Pressure,
        FireBox,
        Rpm,
        Rpm2,
        FireHole,
        CutOff,
        Vacuum_Reservoir_Pressure,
        Gears,
        Doors_Display,
        Speed_Projected,
        SpeedLimit,
        Pantographs_4,
        Pantographs_4C,
        Pantographs_5,
        Orts_Oil_Pressure,
        Orts_Diesel_Temperature,
        Orts_Cyl_Comp,
        Gears_Display,
        Dynamic_Brake_Force,
        Orts_Circuit_Breaker_Driver_Closing_Order,
        Orts_Circuit_Breaker_Driver_Opening_Order,
        Orts_Circuit_Breaker_Driver_Closing_Authorization,
        Orts_Circuit_Breaker_State,
        Orts_Circuit_Breaker_Closed,
        Orts_Circuit_Breaker_Open,
        Orts_Circuit_Breaker_Authorized,
        Orts_Circuit_Breaker_Open_And_Authorized,
        Orts_Traction_CutOff_Relay_Driver_Closing_Order,
        Orts_Traction_CutOff_Relay_Driver_Opening_Order,
        Orts_Traction_CutOff_Relay_Driver_Closing_Authorization,
        Orts_Traction_CutOff_Relay_State,
        Orts_Traction_CutOff_Relay_Closed,
        Orts_Traction_CutOff_Relay_Open,
        Orts_Traction_CutOff_Relay_Authorized,
        Orts_Traction_CutOff_Relay_Open_And_Authorized,
        Orts_Player_Diesel_Engine,
        Orts_Helpers_Diesel_Engines,
        Orts_Player_Diesel_Engine_State,
        Orts_Player_Diesel_Engine_Starter,
        Orts_Player_Diesel_Engine_Stopper,
        Orts_CabLight,
        Orts_LeftDoor,
        Orts_RightDoor,
        Orts_Mirros,
        Orts_Pantograph3,
        Orts_Pantograph4,
        Orts_Water_Scoop,
        Orts_Large_Ejector,
        Orts_HourDial,
        Orts_MinuteDial,
        Orts_SecondDial,
        Orts_Signed_Traction_Braking,
        Orts_Signed_Traction_Total_Braking,
        Orts_Bailoff,
        Orts_QuickRelease,
        Orts_Overcharge,
        Orts_Battery_Switch_Command_Switch,
        Orts_Battery_Switch_Command_Button_Close,
        Orts_Battery_Switch_Command_Button_Open,
        Orts_Battery_Switch_On,
        Orts_Master_Key,
        Orts_Current_Cab_In_Use,
        Orts_Other_Cab_In_Use,
        Orts_Service_Retention_Button,
        Orts_Service_Retention_Cancellation_Button,
        Orts_Electric_Train_Supply_Command_Switch,
        Orts_Electric_Train_Supply_On,
        Orts_2DExternalWipers,
        Orts_Generic_Item1,
        Orts_Generic_Item2,
        Orts_Screen_Select,
        Orts_Static_Display,
        Orts_Eot_Brake_Pipe,
        Orts_Eot_State_Display,
        Orts_Eot_Id,
        Orts_Eot_Comm_Test,
        Orts_Eot_Disarm,
        Orts_Eot_Arm_Two_Way,
        Orts_Eot_Emergency_Brake,

        // TCS Controls
        Orts_TCS,
        Orts_Etcs,
        // Cruise Control
        Orts_Selected_Speed,
        Orts_Selected_Speed_Display,
        Orts_Selected_Speed_Mode,
        Orts_Selected_Speed_Regulator_Mode,
        Orts_Selected_Speed_Maximum_Acceleration,
        Orts_Selected_Speed_Selector,
        Orts_Restricted_Speed_Zone_Active,
        Orts_Number_Of_Axes_Display_Units,
        Orts_Number_Of_Axes_Display_Tens,
        Orts_Number_Of_Axes_Display_Hundreds,
        Orts_Train_Length_Metres,
        Orts_Remaining_Train_Length_Speed_Restricted,
        Orts_Remaining_Train_Length_Percent,
        Orts_Motive_Force,
        Orts_Motive_Force_KiloNewton,
        Orts_Maximum_Force,
        Orts_Force_In_Percent_Throttle_And_Dynamic_Brake,
        Orts_Train_Type_Pax_Or_Cargo,
        Orts_Controller_Voltage,
        Orts_Ampers_By_Controller_Voltage,
        Orts_Acceleration_In_Time,
        Orts_CC_Selected_Speed,
        Orts_Number_Of_Axes_Increase,
        Orts_Number_Of_Axes_Decrease,
        Orts_Multi_Position_Controller,
        Orts_CC_Speed_0,
        Orts_CC_Speed_Delta,
        Orts_Odometer,
        Orts_Odometer_Reset,
        Orts_Odometer_Direction,
        Orts_DistributedPower,
        Orts_DistributedPower_MoveToFront,
        Orts_DistributedPower_MoveToBack,
        Orts_DistributedPower_Idle,
        Orts_DistributedPower_Traction,
        Orts_DistributedPower_Brake,
        Orts_DistributedPower_Increase,
        Orts_DistributedPower_Decrease,

        // Further CabViewControlTypes must be added above this line, to avoid their malfunction in 3DCabs
        ExternalWipers,
        LeftDoor,
        RightDoor,
        Mirrors,
        Orts_Item1Continuous,
        Orts_Item2Continuous,
        Orts_Item1TwoState,
        Orts_Item2TwoState,
    }

    public enum CabViewControlStyle
    {
        None,
        Needle,
#pragma warning disable CA1720 // Identifier contains type name
        Pointer,
#pragma warning restore CA1720 // Identifier contains type name
        Solid,
        Liquid,
        Sprung,
        Not_Sprung,
        While_Pressed,
        Pressed,
        OnOff,
        Hour24,
        Hour12,
    }

    public enum CabViewControlUnit
    {
        None,
        Bar,
        Psi,
        KiloPascals,
        Kgs_Per_Square_Cm,
        Amps,
        Volts,
        KiloVolts,

        Km_Per_Hour,
        Miles_Per_Hour,
        Metres_Sec_Sec,
        Km_Hour_Hour,
        Km_Hour_Sec,
        Metres_Sec_Hour,
        Miles_Hour_Min,
        Miles_Hour_Hour,

        Newtons,
        Kilo_Newtons,
        Kilo_Lbs,
        Metres_Per_Sec,
        Litres,
        Gallons,
        Inches_Of_Mercury,
        Mili_Amps,
        Rpm,
        Lbs,

        Kilometres,
        Metres,
        Miles,
        Feet,
        Yards,
    }

    public enum CabViewControlDiscreteState
    {
        Lever,
        TwoState,
        TriState,
        MultiState,
        CombinedControl,
        CabSignalDisplay,
    }

    #endregion

    #region WorldFile
    // These relate to the general properties settable for scenery objects in RE
    [Flags]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public enum StaticFlag
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        RoundShadow = 0x00002000,
        RectangularShadow = 0x00004000,
        TreelineShadow = 0x00008000,
        DynamicShadow = 0x00010000,
        AnyShadow = 0x0001E000,
        Terrain = 0x00040000,
        Animate = 0x00080000,
        Global = 0x00200000,
    }

    [Flags]
    public enum PlatformData
    {
        PlatformLeft = 0x00000002,
        PlatformRight = 0x00000004,
    }
    #endregion

    #region TrackItem

    #endregion

    #region Train Car and Engines
    public enum EngineType
    {
        [Description("Unknown")]
        Unknown,
        [Description("Steam")]
        Steam,
        [Description("Diesel")]
        Diesel,
        [Description("Electric")]
        Electric,
        [Description("Control")]
        Control,
    }

    public enum WagonType
    {
        Unknown,
        Engine,
        Tender,
        Passenger,
        Freight,
    }

    public enum SteamEngineType
    {
        Unknown,
        Simple,
        Geared,
        Compound,
    }

    public enum WagonSpecialType
    {
        Unknown,
        HeatingBoiler,
        Heated,
        PowerVan,
    }
    #endregion
    public enum BrakeSystemType
    {
        ManualBraking,
        AirPiped,
        AirTwinPipe,
        AirSinglePipe,
        VacuumPiped,
        VacuumSinglePipe,
        VacuumTwinPipe,
        StraightVacuumSinglePipe,
        Ecp,
        Ep,
        Sme,
    }
#pragma warning restore CA1707 // Identifiers should not contain underscores
}
