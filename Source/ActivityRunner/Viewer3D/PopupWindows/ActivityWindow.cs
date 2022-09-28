
using GetText;

using Microsoft.Xna.Framework;

using Orts.Graphics.Window;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class ActivityWindow : WindowBase
    {
        public ActivityWindow(WindowManager owner, Point relativeLocation, Catalog catalog = null) : 
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("Activity Events"), relativeLocation, new Point(300, 200), catalog)
        {
        }
    }
}
