using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Xna
{
    public interface ITextureComponentHost
    {
        GraphicsDevice GraphicsDevice { get; }

        Point ClientSize { get; }

        event EventHandler ClientSizeChanged;

        SpriteBatch CreateSpriteBatch();
    }
}
