
using GetText;

using Microsoft.Xna.Framework;

namespace Orts.Graphics.Window
{
    public abstract class OverlayBase : FormBase
    {
        protected OverlayBase(WindowManager owner, Catalog catalog) :
            base(owner, catalog)
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
