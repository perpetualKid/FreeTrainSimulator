using System;

using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

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
