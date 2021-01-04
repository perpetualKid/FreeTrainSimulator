
using System;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.View.Xna;

namespace Orts.View.DrawableComponents
{
    public class TextDrawShape : ResourceGameComponent<Texture2D>
    {
        [ThreadStatic]
        private static TextDrawShape instance;
        private readonly SpriteBatch spriteBatch;

        private readonly System.Drawing.Bitmap measureBitmap = new System.Drawing.Bitmap(1, 1);
        private readonly System.Drawing.Graphics measureGraphics;

        private readonly System.Drawing.Brush fontBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White);


        private TextDrawShape(Game game, SpriteBatch spriteBatch) : base(game)
        {
            this.spriteBatch = spriteBatch;
            measureGraphics = System.Drawing.Graphics.FromImage(measureBitmap);
        }

        public static void Initialize(Game game, SpriteBatch spriteBatch)
        {
            if (null == instance)
                instance = new TextDrawShape(game, spriteBatch);
        }

        /// <summary>
        /// Draw a text message (string) with transparent background
        /// to support redraw, compiled textures are cached for a short while <seealso cref="SweepInterval"/>
        /// </summary>
        public static void DrawString(Vector2 point, Color color, string message, System.Drawing.Font font, TextAlignment alignment = TextAlignment.Left, SpriteEffects effects = SpriteEffects.None)
        {
            int identifier = GetHashCode(font, message);
            if (!instance.currentResources.TryGetValue(identifier, out Texture2D texture))
            {
                if (!instance.previousResources.TryGetValue(identifier, out texture))
                {
                    texture = DrawString(message, font);
                    instance.currentResources.Add(identifier, texture);
                }
                else
                {
                    instance.currentResources.Add(identifier, texture);
                    instance.previousResources.Remove(identifier);
                }
            }
            switch (alignment)
            {
                case TextAlignment.Right:
                    point -= new Vector2(texture.Width, 0); break;
                case TextAlignment.Center:
                    point -= new Vector2(texture.Width / 2, 0); break;
            }

            instance.spriteBatch.Draw(texture, point, null, color, 0, Vector2.Zero, Vector2.One, effects, 0);
        }

        /// <summary>
        /// Draws a text message with transparent background and returns a texture
        /// </summary>
        /// <returns></returns>
        public static Texture2D DrawString(string message, System.Drawing.Font font)
        {
            // Measure the text with the already instantated measureGraphics object.
            System.Drawing.Size measuredsize = instance.measureGraphics.MeasureString(message, font).ToSize();

            // Create the final bitmap
            using (System.Drawing.Bitmap bmpSurface = new System.Drawing.Bitmap(measuredsize.Width, measuredsize.Height))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmpSurface))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                    // Draw the text to the clean bitmap
                    g.Clear(System.Drawing.Color.Transparent);
                    g.DrawString(message, font, instance.fontBrush, System.Drawing.PointF.Empty);

                    System.Drawing.Imaging.BitmapData bmd = bmpSurface.LockBits(new System.Drawing.Rectangle(0, 0, bmpSurface.Width, bmpSurface.Height),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    int bufferSize = bmd.Height * bmd.Stride;
                    //create data buffer 
                    byte[] bytes = new byte[bufferSize];
                    // copy bitmap data into buffer
                    Marshal.Copy(bmd.Scan0, bytes, 0, bytes.Length);

                    // copy our buffer to the texture
                    Texture2D texture = new Texture2D(instance.Game.GraphicsDevice, bmpSurface.Width, bmpSurface.Height, false, SurfaceFormat.Bgra32);
                    texture.SetData(bytes);
                    // unlock the bitmap data
                    bmpSurface.UnlockBits(bmd);

                    return texture;
                }
            }
        }

        /// <summary>
        /// draws a text message to an existing texture, which is preferable to update text
        /// </summary>
        public static void ReDrawString(string message, System.Drawing.Font font, Texture2D texture)
        {
            if (null == texture)
                throw new ArgumentNullException(nameof(texture));

            // Create the final bitmap
            using (System.Drawing.Bitmap bmpSurface = new System.Drawing.Bitmap(texture.Width, texture.Height))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmpSurface))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                    // Draw the text to the clean bitmap
                    g.Clear(System.Drawing.Color.Transparent);
                    g.DrawString(message, font, instance.fontBrush, System.Drawing.PointF.Empty);

                    System.Drawing.Imaging.BitmapData bmd = bmpSurface.LockBits(new System.Drawing.Rectangle(0, 0, bmpSurface.Width, bmpSurface.Height),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    int bufferSize = bmd.Height * bmd.Stride;
                    //create data buffer 
                    byte[] bytes = new byte[bufferSize];
                    // copy bitmap data into buffer
                    Marshal.Copy(bmd.Scan0, bytes, 0, bytes.Length);

                    // copy our buffer to the texture
                    texture.SetData(bytes);
                    // unlock the bitmap data
                    bmpSurface.UnlockBits(bmd);
                }
            }
        }

        private static int GetHashCode(System.Drawing.Font font, string message)
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash = hash * 23 + font?.GetHashCode() ?? 0;
                hash = hash * 23 + message?.GetHashCode() ?? 0;
                return hash;
            }
        }
    }
}
