using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Xna
{
    /// <summary>
    /// Renders text string to a Texture2D
    /// The texture is kept and reused when updating the text
    /// This class should be used where text updates infrequently
    /// </summary>
    public abstract class ScreenTextComponent : TextureContentComponent
    {
        private protected Font font;

        private protected readonly Brush whiteBrush = new SolidBrush(System.Drawing.Color.White);
        private readonly TextTextureRenderer textRenderer;

        protected ScreenTextComponent(Game game, Font font, Microsoft.Xna.Framework.Color color, Vector2 position) :
            base(game, color, position)
        {
            this.font = font;
            textRenderer = TextTextureRenderer.Instance(game) ?? throw new InvalidOperationException("TextTextureRenderer not found");
        }

        protected virtual void Resize(string text)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            Texture2D updatedTexture = textRenderer.Resize(text, font);
#pragma warning restore CA2000 // Dispose objects before losing scope
            (updatedTexture, texture) = (texture, updatedTexture);
            updatedTexture?.Dispose();
        }

        protected virtual void RenderText(string text)
        {
            textRenderer.RenderText(text, font, texture, OutlineRenderOptions.Default);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                whiteBrush?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Renders text string to a Texture2D
    /// The texture and graphics resources are kept and reused when updating the text
    /// This class should be used where text updates very frequently
    /// </summary>
    public abstract class VolatileTextComponent : ScreenTextComponent
    {
        // Create the final bitmap
        private protected Bitmap bmpSurface;
        private protected System.Drawing.Graphics g;

        protected VolatileTextComponent(Game game, Font font, Microsoft.Xna.Framework.Color color, Vector2 position) :
            base(game, font, color, position)
        {

        }

        protected override void Resize(string text)
        {
            base.Resize(text);

            Bitmap currentSurface = bmpSurface;
            System.Drawing.Graphics currentGraphics = g;
            bmpSurface = new Bitmap(texture.Width, texture.Height);
            g = System.Drawing.Graphics.FromImage(bmpSurface);
            currentGraphics?.Dispose();
            currentSurface?.Dispose();
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        }

        protected override void RenderText(string text)
        {
            // Draw the text to the clean bitmap
            g.Clear(System.Drawing.Color.Transparent);
            g.DrawString(text, font, whiteBrush, PointF.Empty);

            BitmapData bmd = bmpSurface.LockBits(new System.Drawing.Rectangle(0, 0, bmpSurface.Width, bmpSurface.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                g?.Dispose();
                bmpSurface?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
