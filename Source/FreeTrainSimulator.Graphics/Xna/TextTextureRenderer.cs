using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Xna
{
    public class OutlineRenderOptions : IDisposable
    {
        public static OutlineRenderOptions Default { get; } = new OutlineRenderOptions(2.0f, Color.Black, Color.White);
        public static OutlineRenderOptions DefaultTransparent { get; } = new OutlineRenderOptions(1.0f, Color.Black, Color.Transparent);

        internal readonly Pen Pen;
        internal readonly Brush FillBrush;

        private bool disposedValue;
        private readonly int hash;

        public float OutlineWidth => Pen.Width;
        public Color OutlineColor => Pen.Color;

        public OutlineRenderOptions(float width, Microsoft.Xna.Framework.Color outlineColor, Microsoft.Xna.Framework.Color fillColor):
            this (width, outlineColor.ToSystemDrawingColor(), fillColor.ToSystemDrawingColor())
        {
        }

        public OutlineRenderOptions(float width, Color outlineColor, Color fillColor)
        {
            //changing Color format from ARGB to Monogame ABGR
            outlineColor = Color.FromArgb(outlineColor.A, outlineColor.B, outlineColor.G, outlineColor.R);
            Pen = new Pen(outlineColor, width)
            {
                LineJoin = LineJoin.Round
            };
            fillColor = Color.FromArgb(fillColor.A, fillColor.B, fillColor.G, fillColor.R);
            FillBrush = new SolidBrush(fillColor);

            hash = HashCode.Combine(Pen, FillBrush);
        }

        public override int GetHashCode()
        {
            return hash;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Pen?.Dispose();
                    FillBrush?.Dispose();
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
    public class TextTextureRenderer : IDisposable
    {
        internal Texture2D EmptyTexture { get; }
        private readonly Bitmap measureBitmap;
        private readonly Microsoft.Xna.Framework.Game game;
        private readonly ConcurrentQueue<(System.Drawing.Graphics, StringFormat)> measureGraphicsHolder = new ConcurrentQueue<(System.Drawing.Graphics, StringFormat)>();
        private readonly ConcurrentQueue<Brush> whiteBrushHolder = new ConcurrentQueue<Brush>();
        private bool disposedValue;

        private TextTextureRenderer(Microsoft.Xna.Framework.Game game)
        {
            this.game = game;
            EmptyTexture = new Texture2D(game.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            measureBitmap = new Bitmap(1, 1);
        }

        public static TextTextureRenderer Instance(Microsoft.Xna.Framework.Game game)
        {
            ArgumentNullException.ThrowIfNull(game);

            TextTextureRenderer instance;
            if ((instance = game.Services.GetService<TextTextureRenderer>()) == null)
            {
                instance = new TextTextureRenderer(game);
                game.Services.AddService(instance);
            }
            return instance;
        }

        public Size Measure(string text, Font font, OutlineRenderOptions outlineOptions = null)
        {
            if (!measureGraphicsHolder.TryDequeue(out (System.Drawing.Graphics measureGraphics, StringFormat formatHolder) measureContainer))
            {
                measureContainer.measureGraphics = System.Drawing.Graphics.FromImage(measureBitmap);
                measureContainer.measureGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                measureContainer.formatHolder = new StringFormat(StringFormat.GenericDefault) { FormatFlags = StringFormatFlags.MeasureTrailingSpaces };
            }

            Size size = Size.Empty;

            if (!string.IsNullOrEmpty(text) && font != null)
            {
                measureContainer.formatHolder.SetMeasurableCharacterRanges(new CharacterRange[] { new CharacterRange(0, text.Length) });
                Region[] ranges = measureContainer.measureGraphics.MeasureCharacterRanges(text, font, new RectangleF(0, 0, text.Length * font.Height, text.Length * font.Height), measureContainer.formatHolder);
                SizeF actual = ranges[0].GetBounds(measureContainer.measureGraphics).Size;
                int padding = (int)Math.Ceiling(font.Size * 0.2);
                int paddingWidth = padding;
                if (outlineOptions != null)
                {
                    if (actual.Height < font.Height * 1.2)
                        paddingWidth += (int)(text.Length * outlineOptions.OutlineWidth / 2);
                    else
                        paddingWidth += (int)(actual.Width / font.Height * outlineOptions.OutlineWidth);
                }
                size = new Size((int)Math.Ceiling(actual.Width + paddingWidth + 1), (int)Math.Ceiling(actual.Height + (padding / 2)));
            }
            measureGraphicsHolder.Enqueue(measureContainer);
            return size;
        }

        public Texture2D Resize(string text, Font font, OutlineRenderOptions outlineOptions = null)
        {
            Size size = Measure(text, font, outlineOptions);
            return size.Width == 0 || size.Height == 0 ? EmptyTexture : new Texture2D(game.GraphicsDevice, size.Width, size.Height, false, SurfaceFormat.Color);
        }

        public Texture2D RenderText(string text, Font font, OutlineRenderOptions outlineOptions = null)
        {
            Texture2D result = Resize(text, font, outlineOptions);
            RenderText(text, font, result, outlineOptions);
            return result;
        }

        public void RenderText(string text, Font font, Texture2D texture, OutlineRenderOptions outlineOptions = null)
        {
            ArgumentNullException.ThrowIfNull(texture);
            if (texture == EmptyTexture || texture.Width == 1 && texture.Height == 1)
                return;
            ArgumentNullException.ThrowIfNull(font);

            // Create the final bitmap
            using (Bitmap bmpSurface = new Bitmap(texture.Width, texture.Height, PixelFormat.Format32bppArgb))
            {
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmpSurface))
                {
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.High;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

                    // Draw the text to the clean bitmap
                    graphics.Clear(Color.Transparent);
                    if (outlineOptions != null)
                    {
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            path.AddString(text, font.FontFamily, (int)font.Style, graphics.DpiY * font.SizeInPoints / 72, Point.Empty, null);
                            graphics.DrawPath(outlineOptions.Pen, path);
                            graphics.FillPath(outlineOptions.FillBrush, path);
                        }
                    }
                    else
                    {
#pragma warning disable CA2000 // Dispose objects before losing scope
                        if (!whiteBrushHolder.TryDequeue(out Brush whiteBrush))
                            whiteBrush = new SolidBrush(Color.White);
#pragma warning restore CA2000 // Dispose objects before losing scope
                        graphics.DrawString(text, font, whiteBrush, Point.Empty);
                        whiteBrushHolder.Enqueue(whiteBrush);
                    }
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

                    EmptyTexture?.Dispose();
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
