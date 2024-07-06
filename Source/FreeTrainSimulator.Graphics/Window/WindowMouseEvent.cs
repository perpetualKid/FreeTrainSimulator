using System;

using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace Orts.Graphics.Window
{
    public class WindowMouseEvent
    {
        public Point MousePosition { get; }
        public int MouseWheelDelta { get; }
        public Point Movement { get; }
        public bool ButtonDown { get; }
        public KeyModifiers KeyModifiers { get; }

        internal WindowMouseEvent(FormBase window, Point mouseLocation, int mouseWheelDelta, KeyModifiers modifiers)
        {
            MousePosition = mouseLocation - window?.Borders.Location ?? throw new ArgumentNullException(nameof(window));
            MouseWheelDelta = mouseWheelDelta;
            KeyModifiers = modifiers;
        }

        internal WindowMouseEvent(FormBase window, Point mouseLocation, bool buttonDown, KeyModifiers modifiers)
        {
            MousePosition = mouseLocation - window?.Borders.Location ?? throw new ArgumentNullException(nameof(window));
            ButtonDown = buttonDown;
            KeyModifiers = modifiers;
        }

        internal WindowMouseEvent(FormBase window, Point mouseLocation, Vector2 delta, KeyModifiers modifiers)
        {
            MousePosition = mouseLocation - window?.Borders.Location ?? throw new ArgumentNullException(nameof(window));
            Movement = delta.ToPoint();
            ButtonDown = true;
            KeyModifiers = modifiers;
        }
    }
}
