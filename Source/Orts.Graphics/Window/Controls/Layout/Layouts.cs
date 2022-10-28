using System.Linq;

using Microsoft.Xna.Framework;

namespace Orts.Graphics.Window.Controls.Layout
{
    public class ControlLayoutOffset : ControlLayout
    {
        internal ControlLayoutOffset(FormBase window, int width, int height, int left, int top, int right, int bottom) :
            base(window, left, top, width - left - right, height - top - bottom)
        {
        }

        internal ControlLayoutOffset(FormBase window, int width, int height, int offset) :
            base(window, offset, offset, width - offset * 2, height - offset * 2)
        {
        }
    }

    public class ControlLayoutPanel : ControlLayout
    {
        internal ControlLayoutPanel(FormBase window, int width, int height) :
            base(window, 0, 0, width, height)
        {
        }
    }

    public class ControlLayoutHorizontal : ControlLayout
    {
        internal ControlLayoutHorizontal(FormBase window, int width, int height)
            : base(window, 0, 0, width, height)
        {
            VerticalChildAlignment = VerticalAlignment.Center;
        }

        public override int RemainingWidth => base.RemainingWidth - CurrentLeft;
        public override int CurrentLeft => HorizontalChildAlignment switch
        {
            HorizontalAlignment.Left => Controls.Count > 0 ? Controls.Max(c => c.Bounds.Right) - Bounds.Left : 0,
            HorizontalAlignment.Right => Controls.Count > 0 ? Controls.Min(c => c.Bounds.Left) - Bounds.Left : Bounds.Right - Bounds.Left,
            HorizontalAlignment.Center => Controls.Count > 0 ? Controls.Max(c => c.Bounds.Right) - Bounds.Left : Bounds.Width / 2,
            _ => 0,
        }; 

        private protected override int HorizontalChildAlignmentOffset(in Rectangle childBounds)
        {
            return HorizontalChildAlignment switch
            {
                HorizontalAlignment.Left => 0,
                HorizontalAlignment.Right => -childBounds.Width,
                HorizontalAlignment.Center => (Bounds.Width - childBounds.Width) / 2,
                _ => 0,
            };
        }

    }

    public class ControlLayoutVertical : ControlLayout
    {
        internal ControlLayoutVertical(FormBase window, int width, int height)
            : base(window, 0, 0, width, height)
        {
            HorizontalChildAlignment = HorizontalAlignment.Center;
        }

        public override int RemainingHeight => base.RemainingHeight - CurrentTop;
        public override int CurrentTop => Controls.Count > 0 ? Controls.Max(c => c.Bounds.Bottom) - Bounds.Top : 0;
    }
}
