using System;
using System.Collections.ObjectModel;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls.Layout
{
    public abstract class ControlLayout : WindowControl
    {
        public const int SeparatorPadding = 2;

        internal protected static double ScaleFactor { get; set; }

        public Collection<WindowControl> Controls { get; } = new Collection<WindowControl>();
        public int TextHeight { get; internal set; }

        protected ControlLayout(int x, int y, int width, int height)
            : base(x, y, width, height)
        {
        }

        public virtual int RemainingWidth => Position.Width;

        public virtual int RemainingHeight => Position.Height;

        public virtual int CurrentLeft => 0;

        public virtual int CurrentTop => 0;

        protected T InternalAdd<T>(T control) where T : WindowControl
        {
            if (null == control)
                throw new ArgumentNullException(nameof(control));

            if (control is ControlLayout controlLayout)
            {
                controlLayout.TextHeight = TextHeight;
            }
            // Offset control by our position and current values. Don't touch its size!
            control.MoveBy(Position.Left + CurrentLeft, Position.Top + CurrentTop);
            Controls.Add(control);
            return control;
        }

        public void Add(WindowControl control)
        {
            InternalAdd(control);
        }

        public void AddSpace(int width, int height)
        {
            Add(new Spacer(width, height));
        }


        public void AddHorizontalSeparator(bool padding = true)
        {
            Add(new Separator(RemainingWidth, (int)((2 * (padding ? SeparatorPadding : 0) + 1) * ScaleFactor), padding ? (int)(SeparatorPadding * ScaleFactor) : 0));
        }

        public void AddVerticalSeparator(bool padding = true)
        {
            Add(new Separator((int)((2 * (padding ? SeparatorPadding : 0) + 1) * ScaleFactor), RemainingHeight, padding ? (int)(SeparatorPadding * ScaleFactor) : 0));
        }

        public ControlLayoutOffset AddLayoutOffset(int left, int top, int right, int bottom)
        {
            return InternalAdd(new ControlLayoutOffset(RemainingWidth, RemainingHeight, left, top, right, bottom));
        }

        public ControlLayoutOffset AddLayoutOffset(int offset)
        {
            return InternalAdd(new ControlLayoutOffset(RemainingWidth, RemainingHeight, offset));
        }

        public ControlLayoutHorizontal AddLayoutHorizontal()
        {
            return AddLayoutHorizontal(RemainingHeight);
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

        //public ControlLayout AddLayoutScrollboxHorizontal(int height)
        //{
        //    var sb = InternalAdd(new ControlLayoutScrollboxHorizontal(RemainingWidth, height));
        //    sb.Initialize();
        //    return sb.Client;
        //}

        //public ControlLayout AddLayoutScrollboxVertical(int width)
        //{
        //    var sb = InternalAdd(new ControlLayoutScrollboxVertical(width, RemainingHeight));
        //    sb.Initialize();
        //    return sb.Client;
        //}
        public override void Initialize(WindowManager windowManager)
        {
            base.Initialize(windowManager);
            foreach (WindowControl control in Controls)
                control.Initialize(windowManager);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            foreach (WindowControl control in Controls)
                control.Draw(spriteBatch, offset);
        }

        internal override bool HandleMouseClicked(WindowMouseEvent e)
        {
            foreach (WindowControl control in Controls.Where(c => c.Position.Contains(e.MousePosition)))
                if (control.HandleMouseClicked(e))
                    return true;
            return base.HandleMouseClicked(e);
        }

        internal override bool HandleMouseDown(WindowMouseEvent e)
        {
            foreach (WindowControl control in Controls.Where(c => c.Position.Contains(e.MousePosition)))
                if (control.HandleMouseDown(e))
                    return true;
            return base.HandleMouseDown(e);
        }

        internal override bool HandleMouseReleased(WindowMouseEvent e)
        {
            foreach (WindowControl control in Controls.Where(c => c.Position.Contains(e.MousePosition)))
                if (control.HandleMouseReleased(e))
                    return true;
            return base.HandleMouseReleased(e);
        }

        internal override bool HandleMouseMove(WindowMouseEvent e)
        {
            foreach (WindowControl control in Controls.Where(c => c.Position.Contains(e.MousePosition)))
                if (control.HandleMouseMove(e))
                    return true;
            return base.HandleMouseMove(e);
        }

        internal override bool HandleMouseScroll(WindowMouseEvent e)
        {
            foreach (WindowControl control in Controls.Where(c => c.Position.Contains(e.MousePosition)))
                if (control.HandleMouseScroll(e))
                    return true;
            return base.HandleMouseScroll(e);
        }

        internal override void MoveBy(int x, int y)
        {
            foreach (WindowControl control in Controls)
                control.MoveBy(x, y);
            base.MoveBy(x, y);
        }

        protected override void Dispose(bool disposing)
        {
            foreach (WindowControl control in Controls)
                control.Dispose();
            base.Dispose(disposing);
        }
    }
}
