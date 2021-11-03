using System;

using Microsoft.Xna.Framework;

using Orts.Common.Input;

namespace Orts.Graphics.Window
{
    public class WindowMouseEvent
    {
        public Point MousePosition { get; }
        public int MouseWheelDelta { get; }
        public Point Movement { get; }
        public bool ButtonDown { get; }
        public KeyModifiers KeyModifiers { get; }

        public WindowMouseEvent(WindowBase window, Point mouseLocation, int mouseWheelDelta, KeyModifiers modifiers)
        {
            MousePosition = mouseLocation - window?.Location.Location ?? throw new ArgumentNullException(nameof(window));
            MouseWheelDelta = mouseWheelDelta;
            KeyModifiers = modifiers;
        }

        public WindowMouseEvent(WindowBase window, Point mouseLocation, bool buttonDown, KeyModifiers modifiers)
        {
            MousePosition = mouseLocation - window?.Location.Location ?? throw new ArgumentNullException(nameof(window));
            ButtonDown = buttonDown;
            KeyModifiers = modifiers;
        }

        public WindowMouseEvent(WindowBase window, Point mouseLocation, Vector2 delta, KeyModifiers modifiers)
        {
            MousePosition = mouseLocation - window?.Location.Location ?? throw new ArgumentNullException(nameof(window));
            Movement = delta.ToPoint();
            ButtonDown = true;
            KeyModifiers = modifiers;
        }
    }
}
