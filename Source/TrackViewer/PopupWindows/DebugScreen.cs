using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics.Window;

namespace Orts.TrackViewer.PopupWindows
{
    public class DebugScreen : OverlayWindowBase
    {
        public DebugScreen(WindowManager owner, string caption, Point relativeLocation, Point size) : 
            base(owner, caption, Point.Zero, Point.Zero)
        {
        }


        protected override void DrawContent(SpriteBatch spriteBatch)
        {
            base.DrawContent(spriteBatch);
        }
    }
}
