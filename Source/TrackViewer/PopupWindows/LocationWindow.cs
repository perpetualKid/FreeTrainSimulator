
using Microsoft.Xna.Framework;

using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.TrackViewer.PopupWindows
{
    public class LocationWindow : WindowBase
    {
        public LocationWindow(WindowManager owner, string caption, Point relativeLocation) : 
            base(owner, "Location", relativeLocation, new Point(200, 200))
        {
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            layout = base.Layout(layout);

            return layout;
        }
    }
}
