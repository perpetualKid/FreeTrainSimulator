using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls
{
    public class ImageControl : WindowTextureControl
    {
        public Rectangle? ClippingRectangle { get; set; }

        public ImageControl(WindowBase window, Texture2D image, int x, int y) :
            base(window ?? throw new ArgumentNullException(nameof(window)), x, y, 
                (int)((image?.Width ?? throw new ArgumentNullException(nameof(image))) * window.Owner.DpiScaling), (int)(image.Height * window.Owner.DpiScaling))
        {
            texture = image;
        }

        public ImageControl(WindowBase window, Texture2D image, int x, int y, int width, int height) :
            base(window ?? throw new ArgumentNullException(nameof(window)), x, y, width, height)
        {
            texture = image;
        }

        public ImageControl(WindowBase window, Texture2D image, int x, int y, Rectangle clipping) :
            base(window ?? throw new ArgumentNullException(nameof(window)), x, y, (int)(clipping.Width * window.Owner.DpiScaling), (int)(clipping.Height * window.Owner.DpiScaling))
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
