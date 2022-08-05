using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls
{
    public class ImageControl : WindowTextureControl
    {
        public Rectangle? ClippingRectangle { get; set; }

        public ImageControl(WindowBase window, Texture2D image, int x, int y) :
            base(window, x, y, image?.Width ?? throw new ArgumentNullException(nameof(image)), image.Height)
        {
            texture = image;
        }
        public ImageControl(WindowBase window, Texture2D image, int x, int y, Rectangle clipping) :
            base(window, x, y, clipping.Width, clipping.Height)
        {
            texture = image;
            ClippingRectangle = clipping;
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            Rectangle destination = Bounds;
            destination.Offset(offset);
            spriteBatch.Draw(texture, destination, ClippingRectangle, Color.White);
            base.Draw(spriteBatch, offset); 
        }
    }
}
