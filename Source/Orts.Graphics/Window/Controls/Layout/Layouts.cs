using System.Linq;

namespace Orts.Graphics.Window.Controls.Layout
{
    public class ControlLayoutOffset : ControlLayout
    {
        internal ControlLayoutOffset(WindowBase window, int width, int height, int left, int top, int right, int bottom) :
            base(window, left, top, width - left - right, height - top - bottom)
        {
        }

        internal ControlLayoutOffset(WindowBase window, int width, int height, int offset) :
            base(window, offset, offset, width - offset * 2, height - offset * 2)
        {
        }

    }

    public class ControlLayoutHorizontal : ControlLayout
    {
        internal ControlLayoutHorizontal(WindowBase window, int width, int height)
            : base(window, 0, 0, width, height)
        {
            VerticalChildAlignment = VerticalAlignment.Center;
        }

        public override int RemainingWidth => base.RemainingWidth - CurrentLeft;
        public override int CurrentLeft => Controls.Count > 0 ? Controls.Max(c => c.Bounds.Right) - Bounds.Left : 0;
    }

    public class ControlLayoutVertical : ControlLayout
    {
        internal ControlLayoutVertical(WindowBase window, int width, int height)
            : base(window, 0, 0, width, height)
        {
            HorizontalChildAlignment = HorizontalAlignment.Center;
        }

        public override int RemainingHeight => base.RemainingHeight - CurrentTop;
        public override int CurrentTop => Controls.Count > 0 ? Controls.Max(c => c.Bounds.Bottom) - Bounds.Top : 0;
    }
}
