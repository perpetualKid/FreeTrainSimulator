using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Window.Controls
{
    public class ImageControl : WindowTextureControl
    {
        public Rectangle? ClippingRectangle { get; set; }

        public Color Color { get; set; } = Color.White;

        public ImageControl(FormBase window, Texture2D image, int x, int y) :
            base(window ?? throw new ArgumentNullException(nameof(window)), x, y,
                (int)((image?.Width ?? throw new ArgumentNullException(nameof(image))) * window.Owner.DpiScaling), (int)(image.Height * window.Owner.DpiScaling))
        {
            texture = image;
        }

        public ImageControl(FormBase window, Texture2D image, int x, int y, int width, int height) :
            base(window ?? throw new ArgumentNullException(nameof(window)), x, y, width, height)
        {
            texture = image;
        }

        public ImageControl(FormBase window, Texture2D image, int x, int y, Rectangle clipping) :
            base(window ?? throw new ArgumentNullException(nameof(window)), x, y, (int)(clipping.Width * window.Owner.DpiScaling), (int)(clipping.Height * window.Owner.DpiScaling))
        {
            texture = image;
            ClippingRectangle = clipping;
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            Rectangle destination = Bounds;
            destination.Offset(offset);
            spriteBatch.Draw(texture, destination, ClippingRectangle, Color);
            base.Draw(spriteBatch, offset);
        }
    }
}
