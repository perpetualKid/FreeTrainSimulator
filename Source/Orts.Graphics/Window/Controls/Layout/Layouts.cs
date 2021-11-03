using System.Linq;

namespace Orts.Graphics.Window.Controls.Layout
{
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
        public override int CurrentLeft => Controls.Count > 0 ? Controls.Max(c => c.Position.Right) - Position.Left : 0;
    }

    public class ControlLayoutVertical : ControlLayout
    {
        internal ControlLayoutVertical(int width, int height)
            : base(0, 0, width, height)
        {
        }

        public override int RemainingHeight => base.RemainingHeight - CurrentTop;
        public override int CurrentTop => Controls.Count > 0 ? Controls.Max(c => c.Position.Bottom) - Position.Top : 0;
    }
}
