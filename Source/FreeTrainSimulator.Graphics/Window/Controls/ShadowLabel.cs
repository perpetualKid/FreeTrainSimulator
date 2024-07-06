using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Window.Controls
{
    public class ShadowLabel : Label
    {
        private const int cornerRadius = 8;

        private Texture2D roundedShadowTexture;
        private Point topRight;
        private Rectangle rectangleShadow;
        private Point alignmentOffset;

        public ShadowLabel(FormBase window, int width, int height, string text) : base(window, width, height, text)
        {
        }

        public ShadowLabel(FormBase window, int width, int height, string text, System.Drawing.Font font) : base(window, width, height, text, font)
        {
        }

        public ShadowLabel(FormBase window, int width, int height, string text, HorizontalAlignment align) : base(window, width, height, text, align)
        {
        }

        public ShadowLabel(FormBase window, int x, int y, int width, int height, string text) : base(window, x, y, width, height, text)
        {
        }

        public ShadowLabel(FormBase window, int width, int height, string text, HorizontalAlignment align, Color color) : base(window, width, height, text, align, color)
        {
        }

        public ShadowLabel(FormBase window, int x, int y, int width, int height, string text, HorizontalAlignment alignment, System.Drawing.Font font, Color color) :
            base(window, x, y, width, height, text, alignment, font, color)
        {
        }

        protected override void InitializeText(string text)
        {
            base.InitializeText(text);
            if (texture == resourceHolder.EmptyTexture)
            {
                roundedShadowTexture = null;
                return;
            }
            topRight = new Point(texture.Width - cornerRadius, 0);
            rectangleShadow = new Rectangle(cornerRadius, 0, texture.Width - (2 * cornerRadius), texture.Height);
            PrepareShadowTexture();

            switch (Alignment)
            {
                case HorizontalAlignment.Left:
                    alignmentOffset = Point.Zero;
                    break;
                case HorizontalAlignment.Center:
                    alignmentOffset = new Point((Bounds.Width - Math.Min(texture.Width, Bounds.Width)) / 2, 0);
                    break;
                case HorizontalAlignment.Right:
                    alignmentOffset = new Point(Bounds.Width - Math.Min(texture.Width, Bounds.Width), 0);
                    break;
            }
        }

        private void PrepareShadowTexture()
        {
            int height = texture.Height;
            if (!Window.Owner.RoundedShadows.TryGetValue(height, out roundedShadowTexture))
            {
                Color[] data = new Color[height * cornerRadius];
                // Rounded corner background.
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < cornerRadius; x++)
                        if (y > cornerRadius && y < height - cornerRadius
                            || Math.Sqrt(((x - cornerRadius) * (x - cornerRadius)) + ((y - cornerRadius) * (y - cornerRadius))) < cornerRadius
                            || Math.Sqrt(((x - cornerRadius) * (x - cornerRadius)) + ((y - height + cornerRadius) * (y - height + cornerRadius))) < cornerRadius)
                            data[(y * cornerRadius) + x] = Window.Owner.BackgroundColor;
                roundedShadowTexture = new Texture2D(Window.Owner.GraphicsDevice, cornerRadius, height, false, SurfaceFormat.Color);
                roundedShadowTexture.SetData(data);

                _ = Window.Owner.RoundedShadows.TryAdd(height, roundedShadowTexture);
            }
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            if (null == roundedShadowTexture || texture == resourceHolder.EmptyTexture)
                return;
            spriteBatch.Draw(roundedShadowTexture, (Bounds.Location + offset + alignmentOffset).ToVector2(), TextColor);
            spriteBatch.Draw(roundedShadowTexture, (Bounds.Location + offset + alignmentOffset + topRight).ToVector2(), null, TextColor, 0, Vector2.Zero, 1, SpriteEffects.FlipHorizontally, 0);
            Rectangle target = rectangleShadow;
            target.Offset(Bounds.Location + offset + alignmentOffset);
            spriteBatch.Draw(Window.Owner.BackgroundTexture, target, TextColor);
            base.Draw(spriteBatch, offset);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            roundedShadowTexture?.Dispose();
        }
    }
}
