using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework.Graphics;
using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls
{
    public abstract class TextControl : WindowTextureControl
    {
        private protected Font font;
        private protected static Brush whiteBrush = new SolidBrush(Color.White);

        protected TextControl(WindowBase window, int x, int y, int width, int height) : 
            base(window, x, y, width, height)
        {
        }

        internal override void Initialize()
        {
            base.Initialize();
        }

        protected virtual void InitializeText(string text)
        {
            TextTextureRenderer.Resize(text, font, ref texture, Window.Owner.GraphicsDevice);
        }

        protected virtual void RenderText(string text)
        {
            TextTextureRenderer.RenderText(text, font, texture);
        }

    }
}
