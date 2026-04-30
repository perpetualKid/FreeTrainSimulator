using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Xna
{
    public sealed class XnaTextureComponentHost : ITextureComponentHost
    {
        private readonly Game game;
        private readonly GameWindow window;

        public XnaTextureComponentHost(Game game)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            window = game.Window;
        }

        public GraphicsDevice GraphicsDevice => game.GraphicsDevice;

        public Point ClientSize => window?.ClientBounds.Size ?? Point.Zero;

        public event EventHandler ClientSizeChanged
        {
            add
            {
                if (value != null && window != null)
                    window.ClientSizeChanged += new EventHandler<EventArgs>(value);
            }
            remove
            {
                if (value != null && window != null)
                    window.ClientSizeChanged -= new EventHandler<EventArgs>(value);
            }
        }

        public SpriteBatch CreateSpriteBatch()
        {
            return new SpriteBatch(game.GraphicsDevice);
        }
    }
}
