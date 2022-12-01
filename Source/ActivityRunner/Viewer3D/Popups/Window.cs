// COPYRIGHT 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;

namespace Orts.ActivityRunner.Viewer3D.Popups
{
    public abstract class Window : RenderPrimitive
    {
        private const int BaseFontSize = 16; // DO NOT CHANGE without also changing the graphics for the windows.

        protected WindowManager Owner { get; }
        private bool visible;
        private Rectangle location;

        private ControlLayout windowLayout;

        protected Window(WindowManager owner, int width, int height, string caption)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            // We need to correct the window height for the ACTUAL font size, so that the title bar is shown correctly.
            location = new Rectangle(0, 0, width, height - BaseFontSize + owner.TextFontDefault.Height);

            Owner.Add(this);
        }

        internal protected virtual void Initialize()
        {
            VisibilityChanged();
            Layout();
        }

        protected virtual void VisibilityChanged()
        {
            if (Visible)
            {
                if (windowLayout != null)
                    PrepareFrame(ElapsedTime.Zero, true);
            }
        }

        internal virtual void ScreenChanged()
        {
        }

        public bool Visible
        {
            get => visible;
            set
            {
                if (visible != value)
                {
                    visible = value;
                    VisibilityChanged();
                }
            }
        }

        public Rectangle Location => location;

        public virtual void TabAction()
        {
        }

        public void MoveTo(int x, int y)
        {
            x = (int)MathHelper.Clamp(x, 0, Owner.ScreenSize.X - location.Width);
            y = (int)MathHelper.Clamp(y, 0, Owner.ScreenSize.Y - location.Height);

            if ((location.X != x) || (location.Y != y))
            {
                location.X = x;
                location.Y = y;
            }
        }

        internal protected void Layout()
        {
            WindowControlLayout windowLayout = new WindowControlLayout(this, location.Width, location.Height)
            {
                TextHeight = Owner.TextFontDefault.Height
            };

            windowLayout.Initialize(Owner);
            this.windowLayout = windowLayout;
        }

        public virtual void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime, bool updateFull)
        {
            if (Visible)
                PrepareFrame(elapsedTime, updateFull);
        }

        public virtual void PrepareFrame(in ElapsedTime elapsedTime, bool updateFull)
        {
        }

        public virtual void Draw(SpriteBatch spriteBatch)
        {
            windowLayout.Draw(spriteBatch, Location.Location);
        }

        public virtual void Mark()
        {
        }
    }

    internal class WindowControlLayout : ControlLayout
    {
        public readonly Window Window;

        public WindowControlLayout(Window window, int width, int height)
            : base(0, 0, width, height)
        {
            Window = window;
        }
    }
}
