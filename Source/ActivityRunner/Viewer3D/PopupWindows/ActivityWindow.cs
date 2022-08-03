
using GetText;

using Microsoft.Xna.Framework;

using Orts.Graphics.Window;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class ActivityWindow : WindowBase
    {
        public ActivityWindow(WindowManager owner, string caption, Point relativeLocation, Point size, Catalog catalog = null) : 
            base(owner, caption, relativeLocation, size, catalog)
        {
        }
    }
}
