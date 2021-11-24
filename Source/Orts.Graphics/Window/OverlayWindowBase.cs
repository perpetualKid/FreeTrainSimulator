
using Microsoft.Xna.Framework;

using Orts.Graphics.Window.Controls.Layout;

namespace Orts.Graphics.Window
{
    public abstract class OverlayWindowBase : WindowBase
    {
        public override bool Interactive => false;

        protected OverlayWindowBase(WindowManager owner, string caption, Point relativeLocation, Point size) :
            base(owner, caption, relativeLocation, size)
        {
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            return layout;
        }

        internal override void RenderWindow()
        {
        }
    }
}
