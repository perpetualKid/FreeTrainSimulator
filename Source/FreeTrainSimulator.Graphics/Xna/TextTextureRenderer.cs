using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

using FreeTrainSimulator.Common;

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

        public OutlineRenderOptions(float width, Microsoft.Xna.Framework.Color outlineColor, Microsoft.Xna.Framework.Color fillColor) :
            this(width, outlineColor.ToSystemDrawingColor(), fillColor.ToSystemDrawingColor())
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
        /// <summary>Renderer-owned sentinel borrowed by empty text results; consumers must not dispose it.</summary>
        internal Texture2D EmptyTexture { get; }
        private readonly Bitmap measureBitmap;
        private readonly Microsoft.Xna.Framework.Game game;
        private readonly ConcurrentQueue<(System.Drawing.Graphics, StringFormat)> measureGraphicsHolder = new ConcurrentQueue<(System.Drawing.Graphics, StringFormat)>();
        private readonly ConcurrentQueue<Brush> whiteBrushHolder = new ConcurrentQueue<Brush>();
        private Microsoft.Xna.Framework.Game sharedGame;
        private bool disposedValue;
        private ExceptionDispatchInfo releaseFailure;

        private TextTextureRenderer(Microsoft.Xna.Framework.Game game)
        {
            this.game = game;
            EmptyTexture = new Texture2D(game.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            try
            {
                measureBitmap = new Bitmap(1, 1);
            }
            catch
            {
                EmptyTexture.Dispose();
                throw;
            }
        }

        /// <summary>Creates a private renderer whose caller owns disposal.</summary>
        public static TextTextureRenderer Create(Microsoft.Xna.Framework.Game game)
        {
            ArgumentNullException.ThrowIfNull(game);
            return new TextTextureRenderer(game);
        }

        /// <summary>Gets the game-shared renderer, borrowed by consumers and released by the host at shutdown.</summary>
        public static TextTextureRenderer Instance(Microsoft.Xna.Framework.Game game)
        {
            ArgumentNullException.ThrowIfNull(game);

            SharedRendererLifetime lifetime = game.Services.GetService<SharedRendererLifetime>();
            lifetime?.VerifyOpen();
            TextTextureRenderer instance;
            if ((instance = game.Services.GetService<TextTextureRenderer>()) == null)
            {
                instance = new TextTextureRenderer(game);
                try
                {
                    game.Services.AddService(instance);
                }
                catch
                {
                    instance.Dispose();
                    throw;
                }
            }
            if (instance.sharedGame == null)
            {
                lifetime ??= new SharedRendererLifetime(game);
                lifetime.Register(instance);
                instance.sharedGame = game;
            }
            return instance;
        }

        /// <summary>Releases game-owned shared renderers after their borrowing components; failures survive retries.</summary>
        public static void ReleaseSharedResources(Microsoft.Xna.Framework.Game game)
        {
            ArgumentNullException.ThrowIfNull(game);
            game.Services?.GetService<SharedRendererLifetime>()?.Release();
        }

        private sealed class SharedRendererLifetime
        {
            private readonly Microsoft.Xna.Framework.Game game;
            private readonly List<TextTextureRenderer> renderers = new List<TextTextureRenderer>();
            private bool releaseAttempted;
            private ExceptionDispatchInfo releaseFailure;

            public SharedRendererLifetime(Microsoft.Xna.Framework.Game game)
            {
                this.game = game;
                game.Services.AddService(this);
                game.Disposed += Game_Disposed;
            }

            public void Register(TextTextureRenderer renderer)
            {
                VerifyOpen();
                renderers.Add(renderer);
            }

            public void VerifyOpen() => ObjectDisposedException.ThrowIf(releaseAttempted, this);

            private void Game_Disposed(object sender, EventArgs e) => Release();

            public void Release()
            {
                if (releaseAttempted)
                {
                    releaseFailure?.Throw();
                    return;
                }
                releaseAttempted = true;
                try
                {
                    List<Exception> errors = new List<Exception>();
                    foreach (TextTextureRenderer renderer in renderers)
                    {
                        try
                        {
                            renderer.Dispose();
                        }
                        catch (Exception failure) when (CleanupExceptionPolicy.CanContinue(failure))
                        {
                            errors.Add(failure);
                        }
                    }
                    if (errors.Count > 0)
                    {
                        releaseFailure = ExceptionDispatchInfo.Capture(errors.Count == 1 ? errors[0] : new AggregateException(errors));
                        releaseFailure.Throw();
                    }

                    // Keep the fallback attached on failure so plain Game.Dispose retries cannot report success.
                    game.Services.RemoveService(typeof(SharedRendererLifetime));
                    game.Disposed -= Game_Disposed;
                    renderers.Clear();
                }
                catch (Exception failure)
                {
                    releaseFailure = ExceptionDispatchInfo.Capture(failure);
                    throw;
                }
            }
        }

        public Size Measure(string text, Font font, OutlineRenderOptions outlineOptions = null)
        {
            if (!measureGraphicsHolder.TryDequeue(out (System.Drawing.Graphics measureGraphics, StringFormat formatHolder) measureContainer))
            {
                measureContainer.measureGraphics = System.Drawing.Graphics.FromImage(measureBitmap);
                measureContainer.measureGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                measureContainer.formatHolder = new StringFormat(StringFormat.GenericDefault) { FormatFlags = StringFormatFlags.MeasureTrailingSpaces };
            }

            try
            {
                Size size = Size.Empty;

                if (!string.IsNullOrEmpty(text) && font != null)
                {
                    measureContainer.formatHolder.SetMeasurableCharacterRanges(new CharacterRange[] { new CharacterRange(0, text.Length) });
                    Region[] ranges = measureContainer.measureGraphics.MeasureCharacterRanges(text, font, new RectangleF(0, 0, text.Length * font.Height, text.Length * font.Height), measureContainer.formatHolder);
                    SizeF actual;
                    try
                    {
                        actual = ranges[0].GetBounds(measureContainer.measureGraphics).Size;
                    }
                    finally
                    {
                        foreach (Region range in ranges)
                            range.Dispose();
                    }
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
                return size;
            }
            finally
            {
                measureGraphicsHolder.Enqueue(measureContainer);
            }
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
            if (disposedValue)
            {
                releaseFailure?.Throw();
                return;
            }
            disposedValue = true;
            try
            {
                if (disposing)
                {
                    List<Exception> errors = new List<Exception>();
                    void Release(Action release)
                    {
                        try
                        {
                            release();
                        }
                        catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                        {
                            errors.Add(error);
                        }
                    }

                    if (sharedGame != null)
                    {
                        Release(() =>
                        {
                            if (ReferenceEquals(sharedGame.Services.GetService<TextTextureRenderer>(), this))
                                sharedGame.Services.RemoveService(typeof(TextTextureRenderer));
                        });
                        sharedGame = null;
                    }
                    while (measureGraphicsHolder.TryDequeue(out (System.Drawing.Graphics measureGraphics, StringFormat formatHolder) measureContainer))
                    {
                        Release(() => measureContainer.measureGraphics?.Dispose());
                        Release(() => measureContainer.formatHolder?.Dispose());
                    }
                    while (whiteBrushHolder.TryDequeue(out Brush brush))
                        Release(() => brush?.Dispose());

                    Release(() => EmptyTexture?.Dispose());
                    Release(() => measureBitmap?.Dispose());
                    if (errors.Count > 0)
                    {
                        releaseFailure = ExceptionDispatchInfo.Capture(errors.Count == 1 ? errors[0] : new AggregateException(errors));
                        releaseFailure.Throw();
                    }
                }
            }
            catch (Exception error)
            {
                releaseFailure = ExceptionDispatchInfo.Capture(error);
                throw;
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
