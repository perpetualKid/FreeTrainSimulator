
using Microsoft.Xna.Framework;

using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.TrackViewer.PopupWindows
{
    public class LocationWindow : WindowBase
    {
        public LocationWindow(WindowManager owner, string caption, Point relativeLocation, Point size) : 
            base(owner, "Location", relativeLocation, size)
        {
        }

        protected override ControlLayout Layout(ControlLayout layout)
        {
            layout = base.Layout(layout);

            return layout;
        }
    }
}
