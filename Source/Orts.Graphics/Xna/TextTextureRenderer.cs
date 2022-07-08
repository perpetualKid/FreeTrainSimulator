using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Xna
{
    public static class TextTextureRenderer
    {
        [ThreadStatic]
        private static Texture2D emptyTexture;
        [ThreadStatic]
        private static Brush whiteBrush;
        [ThreadStatic]
        private static Bitmap measureBitmap;

        public static Size Measure(string text, Font font)
        {
            using (System.Drawing.Graphics measureGraphics = System.Drawing.Graphics.FromImage(measureBitmap ??= new Bitmap(1, 1)))
            {
                return measureGraphics.MeasureString(text, font).ToSize();
            }
        }

        public static Size Measure(string text, Font font, System.Drawing.Graphics measureGraphics)
        {
            return measureGraphics?.MeasureString(text, font).ToSize() ?? throw new ArgumentNullException(nameof(measureGraphics));
        }

        public static void Resize(string text, Font font, ref Texture2D texture, GraphicsDevice graphicsDevice)
        {
            using (System.Drawing.Graphics measureGraphics = System.Drawing.Graphics.FromImage(measureBitmap ??= new Bitmap(1, 1)))
            {
                Size size = measureGraphics.MeasureString(text, font).ToSize();
                if (size.ToPoint() != texture?.Bounds.Size)
                {
                    Texture2D current = texture;
                    texture = (size.Width == 0 || size.Height == 0) ? (emptyTexture ??= new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Bgra32)) : new Texture2D(graphicsDevice, size.Width, size.Height, false, SurfaceFormat.Bgra32);
                    current?.Dispose();
                }
            }
        }

        public static Texture2D Resize(string text, Font font, GraphicsDevice graphicsDevice)
        {
            using (System.Drawing.Graphics measureGraphics = System.Drawing.Graphics.FromImage(measureBitmap ??= new Bitmap(1, 1)))
            {
                Size size = measureGraphics.MeasureString(text, font).ToSize();
                return (size.Width == 0 || size.Height == 0) ? (emptyTexture ??= new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Bgra32)) : new Texture2D(graphicsDevice, size.Width, size.Height, false, SurfaceFormat.Bgra32);
            }
        }

        public static Texture2D Resize(string text, Font font, GraphicsDevice graphicsDevice, System.Drawing.Graphics measureGraphics)
        {
            Size size = measureGraphics?.MeasureString(text, font).ToSize() ?? throw new ArgumentNullException(nameof(measureGraphics));
            return (size.Width == 0 || size.Height == 0) ? (emptyTexture ??= new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Bgra32)) : new Texture2D(graphicsDevice, size.Width, size.Height, false, SurfaceFormat.Bgra32);
        }

        public static void RenderText(string text, Font font, Texture2D texture)
        {
            if (null == texture)
                throw new ArgumentNullException(nameof(texture));
            if (texture == emptyTexture || (texture.Width == 1 && texture.Height == 1))
                return;

            // Create the final bitmap
            using (Bitmap bmpSurface = new Bitmap(texture.Width, texture.Height))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmpSurface))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

                    // Draw the text to the clean bitmap
                    g.Clear(Color.Transparent);
                    g.DrawString(text, font, whiteBrush ??= new SolidBrush(Color.White), PointF.Empty);

                    BitmapData bmd = bmpSurface.LockBits(new Rectangle(0, 0, bmpSurface.Width, bmpSurface.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
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
    }
}
