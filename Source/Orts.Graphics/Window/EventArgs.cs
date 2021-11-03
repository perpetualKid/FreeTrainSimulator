using System;

using Microsoft.Xna.Framework;

using Orts.Common.Input;

namespace Orts.Graphics.Window
{
    public class MouseClickEventArgs : EventArgs
    {
        public Point Position { get; }
        public KeyModifiers KeyModifiers { get; }

        public MouseClickEventArgs(Point position, KeyModifiers keyModifiers)
        {
            Position = position;
            KeyModifiers = keyModifiers;
        }

    }
}
