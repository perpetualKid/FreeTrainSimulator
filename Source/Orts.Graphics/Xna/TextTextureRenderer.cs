using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Xna
{
    public class TextTextureRenderer : IDisposable
    {
        private readonly Texture2D emptyTexture;
        private readonly Bitmap measureBitmap;
        private readonly Microsoft.Xna.Framework.Game game;
        private readonly ConcurrentQueue<(System.Drawing.Graphics, StringFormat)> measureGraphicsHolder = new ConcurrentQueue<(System.Drawing.Graphics, StringFormat)>();
        private readonly ConcurrentQueue<Brush> whiteBrushHolder = new ConcurrentQueue<Brush>();
        private bool disposedValue;

        private TextTextureRenderer(Microsoft.Xna.Framework.Game game)
        {
            this.game = game;
            emptyTexture = new Texture2D(game.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            measureBitmap = new Bitmap(1, 1);
        }

        public static TextTextureRenderer Instance(Microsoft.Xna.Framework.Game game)
        {
            if (null == game)
                throw new ArgumentNullException(nameof(game));

            TextTextureRenderer instance;
            if ((instance = game.Services.GetService<TextTextureRenderer>()) == null)
            {
                instance = new TextTextureRenderer(game);
                game.Services.AddService(instance);
            }
            return instance;
        }

        public Size Measure(string text, Font font)
        {
            if (!measureGraphicsHolder.TryDequeue(out (System.Drawing.Graphics measureGraphics, StringFormat formatHolder) measureContainer))
            {
                measureContainer.measureGraphics = System.Drawing.Graphics.FromImage(measureBitmap);
                measureContainer.measureGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                measureContainer.formatHolder = new StringFormat(StringFormat.GenericDefault);
            }

            Size size = Size.Empty; // measureGraphics.MeasureString(text, font).ToSize();

            if (!string.IsNullOrEmpty(text) && font != null)
            {
                measureContainer.formatHolder.SetMeasurableCharacterRanges(new CharacterRange[] { new CharacterRange(0, text.Length) });
                Region[] ranges = measureContainer.measureGraphics.MeasureCharacterRanges(text, font, new RectangleF(0, 0, text.Length * font.Height, text.Length * font.Height), measureContainer.formatHolder);
                SizeF actual = ranges[0].GetBounds(measureContainer.measureGraphics).Size;
                int padding = (int)Math.Ceiling(font.Size * 0.2);
                size = new Size((int)Math.Ceiling(actual.Width + 2 * padding), (int)Math.Ceiling(actual.Height + padding / 2));
            }
            measureGraphicsHolder.Enqueue(measureContainer);
            return size;
        }

        public Texture2D Resize(string text, Font font)
        {
            Size size = Measure(text, font);
            return (size.Width == 0 || size.Height == 0) ? emptyTexture : new Texture2D(game.GraphicsDevice, size.Width, size.Height, false, SurfaceFormat.Color);
        }

        public void RenderText(string text, Font font, Texture2D texture)
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
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                    // Draw the text to the clean bitmap
                    g.Clear(Color.Transparent);
#pragma warning disable CA2000 // Dispose objects before losing scope
                    if (!whiteBrushHolder.TryDequeue(out Brush whiteBrush))
                        whiteBrush = new SolidBrush(Color.White);
#pragma warning restore CA2000 // Dispose objects before losing scope
                    g.DrawString(text, font, whiteBrush, Point.Empty);
                    whiteBrushHolder.Enqueue(whiteBrush);
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    while (measureGraphicsHolder.TryDequeue(out (System.Drawing.Graphics measureGraphics, StringFormat formatHolder) measureContainer))
                    {
                        measureContainer.measureGraphics?.Dispose();
                        measureContainer.formatHolder?.Dispose();
                    }
                    while (whiteBrushHolder.TryDequeue(out Brush brush))
                        brush?.Dispose();

                    emptyTexture?.Dispose();
                    measureBitmap?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
