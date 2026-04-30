using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Xna
{
    public interface ITextureRenderHelper
    {
        void DrawSpriteBatch(Action<SpriteBatch> drawAction);

        RenderTarget2D CreateRenderTarget(int width, int height);

        Texture2D CreateTexture(int width, int height);

        void RenderToTarget(RenderTarget2D renderTarget, Color clearColor, Action<SpriteBatch> drawAction);
    }
}
