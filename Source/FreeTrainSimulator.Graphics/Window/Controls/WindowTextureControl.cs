using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Window.Controls
{
    public abstract class WindowTextureControl : WindowControl
    {
        private protected Texture2D texture;
        public Color TextColor { get; set; }

        protected WindowTextureControl(FormBase window, int x, int y, int width, int height) :
            base(window, x, y, width, height)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
            base.Dispose(disposing);
        }
    }
}
