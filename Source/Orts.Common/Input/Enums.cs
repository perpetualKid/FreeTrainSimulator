namespace Orts.Common.Input
{
#pragma warning disable CA1008 // Enums should have zero value
#pragma warning disable CA1027 // Mark enums with FlagsAttribute
    public enum KeyEventType
#pragma warning restore CA1027 // Mark enums with FlagsAttribute
#pragma warning restore CA1008 // Enums should have zero value
    {
        /// <summary>
        /// Key just pressed down
        /// </summary>
        KeyPressed = 1,
        /// <summary>
        /// Key held down
        /// </summary>
        KeyDown = 2,
        /// <summary>
        /// Key released
        /// </summary>
        KeyReleased = 4,
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

    public enum GenericButtonEventType
    { 
        None,
        Pressed,
        Down,
        Released,
    }
}
