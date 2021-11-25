using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls
{
    public abstract class WindowTextureControl : WindowControl
    {
        private protected Texture2D texture;
        public Color TextColor { get; set; }

        protected WindowTextureControl(WindowBase window, int x, int y, int width, int height) :
            base(window, x, y, width, height)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                texture?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
