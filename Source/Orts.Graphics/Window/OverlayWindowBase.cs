
using GetText;

using Microsoft.Xna.Framework;

using Orts.Graphics.Window.Controls.Layout;

namespace Orts.Graphics.Window
{
    public abstract class OverlayWindowBase : WindowBase
    {
        protected OverlayWindowBase(WindowManager owner, string caption, Catalog catalog = null) :
            base(owner, caption, Point.Zero, Point.Zero, catalog)
        {
            Interactive = false;
            CloseButton = false;
            borderRect.Size = owner.ClientBounds.Size;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling)
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
