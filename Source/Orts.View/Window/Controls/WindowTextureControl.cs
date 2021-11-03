using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.View.Window.Controls
{
    public abstract class WindowTextureControl : WindowControl
    {
        private protected Texture2D texture;
        private protected Color color;

        protected WindowTextureControl(int x, int y, int width, int height) :
            base(x, y, width, height)
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

        public virtual void UpdateColor(Color color)
        {
            this.color = color;
        }

    }
}
