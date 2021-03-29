namespace Orts.Common.Input
{
#pragma warning disable CA1008 // Enums should have zero value
    public enum KeyEventType
#pragma warning restore CA1008 // Enums should have zero value
    {
        /// <summary>
        /// Key just pressed down
        /// </summary>
        KeyPressed = InputGameComponent.KeyPressShift,
        /// <summary>
        /// Key held down
        /// </summary>
        KeyDown = InputGameComponent.KeyDownShift,
        /// <summary>
        /// Key released
        /// </summary>
        KeyReleased = InputGameComponent.KeyUpShift,
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
        PointerDragged,
        ZoomChanged,
    }
}
