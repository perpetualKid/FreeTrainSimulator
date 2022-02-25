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

        public Collection<WindowControl> Controls { get; } = new Collection<WindowControl>();

        protected ControlLayout(WindowBase window, int x, int y, int width, int height)
            : base(window, x, y, width, height)
        {
        }

        public virtual int RemainingWidth => Bounds.Width;

        public virtual int RemainingHeight => Bounds.Height;

        public virtual int CurrentLeft => 0;

        public virtual int CurrentTop => 0;

        public HorizontalAlignment HorizontalChildAlignment { get; protected set; } = HorizontalAlignment.Left;

        public VerticalAlignment VerticalChildAlignment { get; protected set; } = VerticalAlignment.Top;

        protected T InternalAdd<T>(T control) where T : WindowControl
        {
            if (null == control)
                throw new ArgumentNullException(nameof(control));

            // Offset control by our position and current values. Don't touch its size, also consider alignment
            control.MoveBy(Bounds.Left + CurrentLeft + HorizontalChildAlignmentOffset(control.Bounds), Bounds.Top + CurrentTop + VerticalChildAlignmentOffset(control.Bounds));
            Controls.Add(control);
            control.Container = this;
            return control;
        }

        public void Add(WindowControl control)
        {
            InternalAdd(control);
        }

        public void AddSpace(int width, int height)
        {
            Add(new Spacer(Window, width, height));
        }

        public void AddHorizontalSeparator(bool padding = true)
        {
            Add(new Separator(Window, RemainingWidth, (int)((2 * (padding ? SeparatorPadding : 0) + 1) * Window.Owner.DpiScaling), padding ? (int)(SeparatorPadding * Window.Owner.DpiScaling) : 0));
        }

        public void AddVerticalSeparator(bool padding = true)
        {
            Add(new Separator(Window, (int)((2 * (padding ? SeparatorPadding : 0) + 1) * Window.Owner.DpiScaling), RemainingHeight, padding ? (int)(SeparatorPadding * Window.Owner.DpiScaling) : 0));
        }

        public ControlLayoutOffset AddLayoutOffset(int left, int top, int right, int bottom)
        {
            return InternalAdd(new ControlLayoutOffset(Window, RemainingWidth, RemainingHeight, left, top, right, bottom));
        }

        public ControlLayoutOffset AddLayoutOffset(int offset)
        {
            return InternalAdd(new ControlLayoutOffset(Window, RemainingWidth, RemainingHeight, offset));
        }

        public ControlLayoutHorizontal AddLayoutHorizontal()
        {
            return AddLayoutHorizontal(RemainingHeight);
        }

        public ControlLayoutHorizontal AddLayoutHorizontalLineOfText()
{
            return AddLayoutHorizontal((int)(Window.Owner.DefaultFontSize * 1.25 * Window.Owner.DpiScaling));
        }

        public ControlLayoutHorizontal AddLayoutHorizontal(int height)
        {
            return InternalAdd(new ControlLayoutHorizontal(Window, RemainingWidth, height));
        }

        public ControlLayoutVertical AddLayoutVertical()
        {
            return AddLayoutVertical(RemainingWidth);
        }

        public ControlLayoutVertical AddLayoutVertical(int width)
        {
            return InternalAdd(new ControlLayoutVertical(Window, width, RemainingHeight));
        }

        //public ControlLayout AddLayoutScrollboxHorizontal(int height)
        //{
        //    var sb = InternalAdd(new ControlLayoutScrollboxHorizontal(RemainingWidth, height));
        //    sb.Initialize();
        //    return sb.Client;
        //}

        public ControlLayout AddLayoutScrollboxVertical(int width)
        {
            return InternalAdd(new VerticalScrollboxControlLayout(Window, width, RemainingHeight)).Client;
        }

        internal override void Initialize()
        {
            base.Initialize();
            foreach (WindowControl control in Controls)
                control.Initialize();
        }

        internal override void Update(GameTime gameTime)
        {
            foreach (WindowControl control in Controls)
            {
                if (control.Visible)
                    control.Update(gameTime);
            }
            base.Update(gameTime);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            foreach (WindowControl control in Controls)
            {
                if (control.Visible)
                    control.Draw(spriteBatch, offset);
            }
        }

        internal override bool HandleMouseClicked(WindowMouseEvent e)
        {
            foreach (WindowControl control in Controls.Where(c => c.Bounds.Contains(e.MousePosition)))
                if (control.HandleMouseClicked(e))
                    return true;
            return base.HandleMouseClicked(e);
        }

        internal override bool HandleMouseDown(WindowMouseEvent e)
        {
            foreach (WindowControl control in Controls.Where(c => c.Bounds.Contains(e.MousePosition)))
                if (control.HandleMouseDown(e))
                    return true;
            return base.HandleMouseDown(e);
        }

        internal override bool HandleMouseReleased(WindowMouseEvent e)
        {
            Window.CapturedControl = null;
            foreach (WindowControl control in Controls.Where(c => c.Bounds.Contains(e.MousePosition)))
                if (control.HandleMouseReleased(e))
                    return true;
            return base.HandleMouseReleased(e);
        }

        internal override bool HandleMouseMove(WindowMouseEvent e)
        {
            foreach (WindowControl control in Controls.Where(c => c.Bounds.Contains(e.MousePosition)))
                if (control.HandleMouseMove(e))
                    return true;
            return base.HandleMouseMove(e);
        }

        internal override bool HandleMouseScroll(WindowMouseEvent e)
        {
            foreach (WindowControl control in Controls.Where(c => c.Bounds.Contains(e.MousePosition)))
                if (control.HandleMouseScroll(e))
                    return true;
            return base.HandleMouseScroll(e);
        }

        internal override bool HandleMouseDrag(WindowMouseEvent e)
        {
            foreach (WindowControl control in Controls.Where(c => c.Bounds.Contains(e.MousePosition) || c is ControlLayout controlLayout && TestForDragging(controlLayout)))
                if (control.HandleMouseDrag(e))
                    return true;
            return base.HandleMouseDrag(e);
        }

        private bool TestForDragging(ControlLayout controlLayout)
        {
            return controlLayout == Window.CapturedControl || controlLayout.Controls.Where((c) => c is ControlLayout controlLayout && TestForDragging(controlLayout)).Any();
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

        private int VerticalChildAlignmentOffset(in Rectangle childBounds)
        {
            return VerticalChildAlignment switch
            {
                VerticalAlignment.Top => 0,
                VerticalAlignment.Bottom => Bounds.Height - childBounds.Height,
                VerticalAlignment.Center => (Bounds.Height - childBounds.Height) / 2,
                _ => 0,
            };
        }

        private int HorizontalChildAlignmentOffset(in Rectangle childBounds)
        {
            return HorizontalChildAlignment switch
            {
                HorizontalAlignment.Left => 0,
                HorizontalAlignment.Right => Bounds.Width - childBounds.Width,
                HorizontalAlignment.Center => (Bounds.Width - childBounds.Width) / 2,
                _ => 0,
            };
        }

    }
}
