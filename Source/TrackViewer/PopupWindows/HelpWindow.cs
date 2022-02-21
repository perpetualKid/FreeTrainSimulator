using System;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Graphics.Window;

namespace Orts.TrackViewer.PopupWindows
{
    public class HelpWindow : WindowBase
    {
        public HelpWindow(WindowManager owner, Point relativeLocation) : 
            base(owner ?? throw new ArgumentNullException(nameof(owner)), CatalogManager.Catalog.GetString("Help"), 
                relativeLocation, new Point(owner.DefaultFontSize * 36, (int)(owner.DefaultFontSize * 5.5f + 20)))
        {
        }
    }
}
