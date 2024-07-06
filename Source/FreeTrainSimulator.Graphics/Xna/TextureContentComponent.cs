
using System;

using FreeTrainSimulator.Graphics.MapView;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Xna
{
    /// <summary>
    /// Abstract base class for components containing content which gets rendered to a texture when it updates, 
    /// and when drawn, the texture is drawn to screen
    /// Component includes some basic handling for screen positioning
    /// </summary>
    public abstract class TextureContentComponent : DrawableGameComponent
    {
        private protected Texture2D texture;
        private protected ContentArea content;
        private protected Vector2 position;
        private protected Vector2 positionOffset;

        private protected readonly SpriteBatch spriteBatch;
        private protected Color color;

        protected TextureContentComponent(Game game, Color color, Vector2 position) :
            base(game)
        {
            spriteBatch = new SpriteBatch(game?.GraphicsDevice);
            this.color = color;
            this.position = position;
            if (position.X < 0 || position.Y < 0)
                positionOffset = position;
            game.Window.ClientSizeChanged += Window_ClientSizeChanged;
        }

        private protected virtual void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            if (null != texture && (positionOffset.X < 0 || positionOffset.Y < 0))
                position = new Vector2(positionOffset.X > 0 ? positionOffset.X : Game.Window.ClientBounds.Width + positionOffset.X - texture.Width, positionOffset.Y > 0 ? positionOffset.Y : Game.Window.ClientBounds.Height + positionOffset.Y - texture.Height);
        }

        internal protected virtual void Enable(ContentArea content)
        {
            this.content = content;
            DrawOrder = content?.DrawOrder + 10 ?? 99;
            Enabled = true;
            Visible = true;
        }

        internal protected virtual void Disable()
        {
            Enabled = false;
            Visible = false;
            content = null;
            texture = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                spriteBatch?.Dispose();
                texture?.Dispose();
                if (null != Game.Window)
                    Game.Window.ClientSizeChanged -= Window_ClientSizeChanged;
            }
            base.Dispose(disposing);
        }

        public virtual void UpdateColor(Color color)
        {
            this.color = color;
        }
    }
}
