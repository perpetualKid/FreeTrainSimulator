
using Microsoft.Xna.Framework;

using Orts.Graphics.Window.Controls.Layout;

namespace Orts.Graphics.Window
{
    public abstract class OverlayWindowBase : WindowBase
    {
        protected OverlayWindowBase(WindowManager owner, string caption, Point relativeLocation, Point size) :
            base(owner, caption, relativeLocation, size)
        {
            Interactive = false;
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            return layout;
        }

        internal protected override void WindowDraw()
        {
        }

        public override bool Open()
        {
            return base.Open();
        }

        public override bool Close()
        {
            return base.Close();
        }
    }
}
