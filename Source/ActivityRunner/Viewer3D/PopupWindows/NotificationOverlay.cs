using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GetText;

using Orts.Graphics.Window;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class NotificationOverlay : OverlayBase
    {
        public NotificationOverlay(WindowManager owner, Catalog catalog = null) : base(owner, catalog)
        {
        }
    }
}
