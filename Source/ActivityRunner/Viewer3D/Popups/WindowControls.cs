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
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Input;

namespace Orts.ActivityRunner.Viewer3D.Popups
{

    public abstract class Control
    {
        public Rectangle Position;
        public object Tag { get; set; }
        public event Action<Control, Point> Click;

        protected Control(int x, int y, int width, int height)
        {
            Position = new Rectangle(x, y, width, height);
        }

        public virtual void Initialize(WindowManager windowManager)
        {
        }

        internal abstract void Draw(SpriteBatch spriteBatch, Point offset);

        internal virtual bool HandleMousePressed(WindowMouseEvent e)
        {
            return false;
        }

        internal virtual bool HandleMouseReleased(WindowMouseEvent e)
        {
            MouseClick(e);
            return false;
        }

        internal virtual bool HandleMouseDown(WindowMouseEvent e)
        {
            return false;
        }

        internal virtual bool HandleMouseMove(WindowMouseEvent e)
        {
            return false;
        }

        internal virtual bool HandleMouseScroll(WindowMouseEvent e)
        {
            return false;
        }

        internal virtual void MoveBy(int x, int y)
        {
            Position.X += x;
            Position.Y += y;
        }

        internal virtual void MouseClick(WindowMouseEvent e)
        {
            Click?.Invoke(this, e.MousePosition - Position.Location);
        }
    }

    public class Spacer : Control
    {
        public Spacer(int width, int height)
            : base(0, 0, width, height)
        {
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
        }
    }

    public class Separator : Control
    {
        public int Padding;

        public Separator(int width, int height, int padding)
            : base(0, 0, width, height)
        {
            Padding = padding;
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            spriteBatch.Draw(WindowManager.WhiteTexture, new Rectangle(offset.X + Position.X + Padding, offset.Y + Position.Y + Padding, Position.Width - 2 * Padding, Position.Height - 2 * Padding), Color.White);
        }
    }

    public enum LabelAlignment
    {
        Left,
        Center,
        Right,
    }

    public class Label : Control
    {
        public string Text;
        public LabelAlignment Align;
        public Color Color;
        protected WindowTextFont Font;

        public Label(int x, int y, int width, int height, string text, LabelAlignment align)
            : base(x, y, width, height)
        {
            Text = text;
            Align = align;
            Color = Color.White;
        }

        public Label(int width, int height, string text, LabelAlignment align)
            : this(0, 0, width, height, text, align)
        {
        }

        public Label(int width, int height, string text)
            : this(0, 0, width, height, text, LabelAlignment.Left)
        {
        }

        public override void Initialize(WindowManager windowManager)
        {
            base.Initialize(windowManager);
            Font = windowManager.TextFontDefault;
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            Font.Draw(spriteBatch, Position, offset, Text, Align, Color);
        }
    }

    public abstract class ControlLayout : Control
    {
        public const int SeparatorSize = 5;
        public const int SeparatorPadding = 2;

        protected readonly List<Control> controls = new List<Control>();
        public IEnumerable<Control> Controls { get { return controls; } }
        public int TextHeight { get; internal set; }

        protected ControlLayout(int x, int y, int width, int height)
            : base(x, y, width, height)
        {
        }

        public virtual int RemainingWidth
        {
            get
            {
                return Position.Width;
            }
        }

        public virtual int RemainingHeight
        {
            get
            {
                return Position.Height;
            }
        }

        public virtual int CurrentLeft
        {
            get
            {
                return 0;
            }
        }

        public virtual int CurrentTop
        {
            get
            {
                return 0;
            }
        }

        protected T InternalAdd<T>(T control) where T : Control
        {
            var controlLayout = control as ControlLayout;
            if (controlLayout != null)
            {
                controlLayout.TextHeight = TextHeight;
            }
            // Offset control by our position and current values. Don't touch its size!
            control.Position.X += Position.Left + CurrentLeft;
            control.Position.Y += Position.Top + CurrentTop;
            controls.Add(control);
            return control;
        }

        public void Add(Control control)
        {
            InternalAdd(control);
        }

        public void AddSpace(int width, int height)
        {
            Add(new Spacer(width, height));
        }

        public void AddHorizontalSeparator()
        {
            Add(new Separator(RemainingWidth, SeparatorSize, SeparatorPadding));
        }

        public ControlLayoutOffset AddLayoutOffset(int left, int top, int right, int bottom)
        {
            return InternalAdd(new ControlLayoutOffset(RemainingWidth, RemainingHeight, left, top, right, bottom));
        }

        public ControlLayoutHorizontal AddLayoutHorizontalLineOfText()
        {
            return AddLayoutHorizontal(TextHeight);
        }

        public ControlLayoutHorizontal AddLayoutHorizontal(int height)
        {
            return InternalAdd(new ControlLayoutHorizontal(RemainingWidth, height));
        }

        public ControlLayoutVertical AddLayoutVertical()
        {
            return AddLayoutVertical(RemainingWidth);
        }

        public ControlLayoutVertical AddLayoutVertical(int width)
        {
            return InternalAdd(new ControlLayoutVertical(width, RemainingHeight));
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

        internal override bool HandleMousePressed(WindowMouseEvent e)
        {
            foreach (var control in controls.Where(c => c.Position.Contains(e.MousePosition)))
                if (control.HandleMousePressed(e))
                    return true;
            return base.HandleMousePressed(e);
        }

        internal override bool HandleMouseDown(WindowMouseEvent e)
        {
            foreach (var control in controls.Where(c => c.Position.Contains(e.MousePosition)))
                if (control.HandleMouseDown(e))
                    return true;
            return base.HandleMouseDown(e);
        }

        internal override bool HandleMouseReleased(WindowMouseEvent e)
        {
            foreach (var control in controls.Where(c => c.Position.Contains(e.MousePosition)))
                if (control.HandleMouseReleased(e))
                    return true;
            return base.HandleMouseReleased(e);
        }

        internal override bool HandleMouseMove(WindowMouseEvent e)
        {
            foreach (var control in controls.Where(c => c.Position.Contains(e.MousePosition)))
                if (control.HandleMouseMove(e))
                    return true;
            return base.HandleMouseMove(e);
        }

        internal override bool HandleMouseScroll(WindowMouseEvent e)
        {
            foreach (var control in controls.Where(c => c.Position.Contains(e.MousePosition)))
                if (control.HandleMouseScroll(e))
                    return true;
            return base.HandleMouseScroll(e);
        }

        internal override void MoveBy(int x, int y)
        {
            foreach (var control in controls)
                control.MoveBy(x, y);
            base.MoveBy(x, y);
        }
    }

    public class ControlLayoutOffset : ControlLayout
    {
        internal ControlLayoutOffset(int width, int height, int left, int top, int right, int bottom)
            : base(left, top, width - left - right, height - top - bottom)
        {
        }
    }

    public class ControlLayoutHorizontal : ControlLayout
    {
        internal ControlLayoutHorizontal(int width, int height)
            : base(0, 0, width, height)
        {
        }

        public override int RemainingWidth => base.RemainingWidth - CurrentLeft;

        public override int CurrentLeft => controls.Count > 0 ? controls.Max(c => c.Position.Right) - Position.Left : 0;
    }

    public class ControlLayoutVertical : ControlLayout
    {
        internal ControlLayoutVertical(int width, int height)
            : base(0, 0, width, height)
        {
        }

        public override int RemainingHeight => base.RemainingHeight - CurrentTop;

        public override int CurrentTop => controls.Count > 0 ? controls.Max(c => c.Position.Bottom) - Position.Top : 0;
    }
}
