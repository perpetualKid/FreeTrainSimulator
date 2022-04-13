using System;
using System.Collections.Generic;
using System.Text;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Graphics.Window;
using Orts.Settings;

namespace Orts.ActivityRunner.Viewer3D.Dispatcher.PopupWindows
{
    internal class SettingsWindow : WindowBase
    {
        public SettingsWindow(WindowManager owner, DispatcherSettings settings, 
            Point relativeLocation, Catalog catalog = null) : base(owner, "Settings", relativeLocation, new Point(200, 200), catalog)
        {
        }
    }
}
