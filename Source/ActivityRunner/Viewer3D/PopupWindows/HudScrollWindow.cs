using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GetText;

using Microsoft.Xna.Framework;

using Orts.Graphics.Window;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.ActivityRunner.Viewer3D.PopupWindows
{
    internal class HudScrollWindow : WindowBase
    {
        private Viewer viewer;

        public HudScrollWindow(WindowManager owner, Point relativeLocation, Viewer viewer, Catalog catalog = null) : 
            base(owner, (catalog ??= CatalogManager.Catalog).GetString("HUD Scroll"), relativeLocation, new Point(100, 200), catalog)
        {
            this.viewer = viewer;
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            return base.Layout(layout, headerScaling);
        }

        protected override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);
        }
    }
}
