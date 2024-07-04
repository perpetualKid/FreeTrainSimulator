using System.ComponentModel;

namespace FreeTrainSimulator.Common.Input
{
#pragma warning disable CA1008 // Enums should have zero value
    public enum KeyEventType
#pragma warning restore CA1008 // Enums should have zero value
    {
        /// <summary>
        /// Key just pressed down
        /// </summary>
        [Description("Key Pressed")] KeyPressed = 1,
        /// <summary>
        /// Key held down
        /// </summary>
        [Description("Key Down")] KeyDown = 2,
        /// <summary>
        /// Key released
        /// </summary>
        [Description("Key Released")] KeyReleased = 3,
    }

    public enum MouseMovedEventType
    {
        MouseMoved,
        MouseMovedLeftButtonDown,
        MouseMovedRightButtonDown,
    }

    public enum MouseWheelEventType
    {
        MouseWheelChanged,
        MouseHorizontalWheelChanged,
    }

    public enum MouseButtonEventType
    {
        LeftButtonPressed,
        LeftButtonDown,
        LeftButtonReleased,
        RightButtonPressed,
        RightButtonDown,
        RightButtonReleased,
        MiddleButtonPressed,
        MiddleButtonDown,
        MiddleButtonReleased,
        XButton1Pressed,
        XButton1Down,
        XButton1Released,
        XButton2Pressed,
        XButton2Down,
        XButton2Released,
    }

    public enum MouseEventType
    {
        MouseMoved,
        MouseMovedLeftButtonDown,
        MouseMovedRightButtonDown,
        MouseWheelChanged,
        MouseHorizontalWheelChanged,
        LeftButtonPressed,
        LeftButtonDown,
        LeftButtonReleased,
        RightButtonPressed,
        RightButtonDown,
        RightButtonReleased,
        MiddleButtonPressed,
        MiddleButtonDown,
        MiddleButtonReleased,
        XButton1Pressed,
        XButton1Down,
        XButton1Released,
        XButton2Pressed,
        XButton2Down,
        XButton2Released,
    }

    public enum CommonUserCommand
    {
        PointerMoved,
        PointerPressed,
        PointerDown,
        PointerReleased,
        PointerDragged,
        AlternatePointerPressed,
        AlternatePointerDown,
        AlternatePointerReleased,
        AlternatePointerDragged,
        VerticalScrollChanged,
        HorizontalScrollChanged,
    }

    public enum AnalogUserCommand
    {
        None,
        Wiper,
        Light,
        Direction,
        Throttle,
        DynamicBrake,
        TrainBrake,
        EngineBrake,
        BailOff,
        Emergency,
        CabActivity,
    }

    public enum GenericButtonEventType
    {
        None,
        Pressed,
        Down,
        Released,
    }

    public enum CommandControllerInput
    {
        Activate,
        Speed,
    }

    public enum RailDriverHandleEventType
    {
        Direction,
        Throttle,
        DynamicBrake,
        TrainBrake,
        EngineBrake,
        BailOff,
        Wipers,
        Lights,
        Emergency,
        CabActivity,
    }

    public enum RailDriverCalibrationSetting
    {
        [Description("Reverser Neutral")] ReverserNeutral,
        [Description("Reverser Full Reversed")] ReverserFullReversed,
        [Description("Reverser Full Forward")] ReverserFullForward,
        [Description("Throttle Idle")] ThrottleIdle,
        [Description("Full Throttle")] ThrottleFull,
        [Description("Dynamic Brake")] DynamicBrake,
        [Description("Dynamic Brake Setup")] DynamicBrakeSetup,
        [Description("Auto Brake Released")] AutoBrakeRelease,
        [Description("Full Auto Brake ")] AutoBrakeFull,
        [Description("Emergency Brake")] EmergencyBrake,
        [Description("Independent Brake Released")] IndependentBrakeRelease,
        [Description("Independent Brake Full")] IndependentBrakeFull,
        [Description("Bail Off Disengaged (in Released position)")] BailOffDisengagedRelease,
        [Description("Bail Off Engaged (in Released position)")] BailOffEngagedRelease,
        [Description("Bail Off Disengaged (in Full position)")] BailOffDisengagedFull,
        [Description("Bail Off Engaged (in Full position)")] BailOffEngagedFull,
        [Description("Rotary Switch 1-Position 1(OFF)")] Rotary1Position1,
        [Description("Rotary Switch 1-Position 2(SLOW)")] Rotary1Position2,
        [Description("Rotary Switch 1-Position 3(FULL)")] Rotary1Position3,
        [Description("Rotary Switch 2-Position 1(OFF)")] Rotary2Position1,
        [Description("Rotary Switch 2-Position 2(DIM)")] Rotary2Position2,
        [Description("Rotary Switch 2-Position 3(FULL)")] Rotary2Position3,
        [Description("Reverse Reverser Direction")] ReverseReverser,
        [Description("Reverse Throttle Direction")] ReverseThrottle,
        [Description("Reverse Auto Brake Direction")] ReverseAutoBrake,
        [Description("Reverse Independent Brake Direction")] ReverseIndependentBrake,
        [Description("Full Range Throttle")] FullRangeThrottle,
        [Description("Cut Off Delta")] CutOffDelta,
    }


}
