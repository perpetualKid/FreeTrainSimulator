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

using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.ActivityRunner.Viewer3D.Popups
{

    public abstract class Control
    {
        private Rectangle Position;

        protected Control(int x, int y, int width, int height)
        {
            Position = new Rectangle(x, y, width, height);
        }

        public virtual void Initialize(WindowManager windowManager)
        {
        }

        internal abstract void Draw(SpriteBatch spriteBatch, Point offset);

        internal virtual void MoveBy(int x, int y)
        {
            Position.X += x;
            Position.Y += y;
        }
    }

    public enum LabelAlignment
    {
        Left,
        Center,
        Right,
    }

    public abstract class ControlLayout : Control
    {
        public const int SeparatorSize = 5;
        public const int SeparatorPadding = 2;

        private readonly List<Control> controls = new List<Control>();
        public IEnumerable<Control> Controls { get { return controls; } }
        public int TextHeight { get; internal set; }

        protected ControlLayout(int x, int y, int width, int height)
            : base(x, y, width, height)
        {
        }

        public override void Initialize(WindowManager windowManager)
        {
            base.Initialize(windowManager);
            foreach (var control in Controls)
                control.Initialize(windowManager);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            foreach (var control in controls)
                control.Draw(spriteBatch, offset);
        }

        internal override void MoveBy(int x, int y)
        {
            foreach (var control in controls)
                control.MoveBy(x, y);
            base.MoveBy(x, y);
        }
    }
}
