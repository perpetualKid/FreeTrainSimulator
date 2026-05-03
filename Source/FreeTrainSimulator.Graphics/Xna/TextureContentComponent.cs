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
        private protected IMapOverlayContext content;
        private protected Vector2 position;
        private protected Vector2 positionOffset;

        private protected readonly SpriteBatch spriteBatch;
        private protected readonly ITextureComponentHost host;
        private protected readonly ITextureRenderHelper renderHelper;
        private protected Color color;

        protected TextureContentComponent(Game game, Color color, Vector2 position) :
            this(game, new XnaTextureComponentHost(game), color, position)
        {
        }

        protected TextureContentComponent(Game game, ITextureComponentHost host, Color color, Vector2 position) :
            base(game)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            spriteBatch = host.CreateSpriteBatch();
            renderHelper = new XnaTextureRenderHelper(this.host, spriteBatch);
            this.color = color;
            this.position = position;
            if (position.X < 0 || position.Y < 0)
                positionOffset = position;
            this.host.ClientSizeChanged += Window_ClientSizeChanged;
        }

        private protected virtual void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            if (null != texture && (positionOffset.X < 0 || positionOffset.Y < 0))
                position = new Vector2(positionOffset.X > 0 ? positionOffset.X : host.ClientSize.X + positionOffset.X - texture.Width, positionOffset.Y > 0 ? positionOffset.Y : host.ClientSize.Y + positionOffset.Y - texture.Height);
        }

        internal virtual void Enable(IMapOverlayContext content)
        {
            this.content = content;
            DrawOrder = content is DrawableGameComponent drawable ? drawable.DrawOrder + 10 : 99;
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
                host.ClientSizeChanged -= Window_ClientSizeChanged;
            }
            base.Dispose(disposing);
        }

        public virtual void UpdateColor(Color color)
        {
            this.color = color;
        }
    }
}
