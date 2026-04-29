using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Xna
{
    public sealed class XnaTextureComponentHost : ITextureComponentHost
    {
        private readonly Game game;

        public XnaTextureComponentHost(Game game)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
        }

        public GraphicsDevice GraphicsDevice => game.GraphicsDevice;

        public Point ClientSize => game.Window.ClientBounds.Size;

        public event EventHandler ClientSizeChanged
        {
            add
            {
                if (value != null)
                    game.Window.ClientSizeChanged += new EventHandler<EventArgs>(value);
            }
            remove
            {
                if (value != null)
                    game.Window.ClientSizeChanged -= new EventHandler<EventArgs>(value);
            }
        }

        public SpriteBatch CreateSpriteBatch()
        {
            return new SpriteBatch(game.GraphicsDevice);
        }
    }
}
