using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Xna
{
    public sealed class XnaTextureRenderHelper : ITextureRenderHelper
    {
        private readonly ITextureComponentHost host;
        private readonly SpriteBatch spriteBatch;

        public XnaTextureRenderHelper(ITextureComponentHost host, SpriteBatch spriteBatch)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.spriteBatch = spriteBatch ?? throw new ArgumentNullException(nameof(spriteBatch));
        }

        public void DrawSpriteBatch(Action<SpriteBatch> drawAction)
        {
            ArgumentNullException.ThrowIfNull(drawAction);
            spriteBatch.Begin();
            drawAction(spriteBatch);
            spriteBatch.End();
        }

        public RenderTarget2D CreateRenderTarget(int width, int height)
        {
            return new RenderTarget2D(host.GraphicsDevice, width, height);
        }

        public Texture2D CreateTexture(int width, int height)
        {
            return new Texture2D(host.GraphicsDevice, width, height, false, SurfaceFormat.Color);
        }

        public void RenderToTarget(RenderTarget2D renderTarget, Color clearColor, Action<SpriteBatch> drawAction)
        {
            ArgumentNullException.ThrowIfNull(renderTarget);
            ArgumentNullException.ThrowIfNull(drawAction);

            host.GraphicsDevice.SetRenderTarget(renderTarget);
            host.GraphicsDevice.Clear(clearColor);
            DrawSpriteBatch(drawAction);
            host.GraphicsDevice.SetRenderTarget(null);
        }
    }
}
