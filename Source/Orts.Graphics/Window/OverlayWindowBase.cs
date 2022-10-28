
using GetText;

using Microsoft.Xna.Framework;

namespace Orts.Graphics.Window
{
    public abstract class OverlayWindowBase : WindowBase
    {
        protected OverlayWindowBase(WindowManager owner, string caption, Catalog catalog = null) :
            base(owner, caption, catalog)
        {
            Interactive = false;
            location = Point.Zero;
            borderRect.Size = owner.ClientBounds.Size;
        }

        protected internal override void Initialize()
        {
            base.Initialize();
            Layout();
        }
    }
}
