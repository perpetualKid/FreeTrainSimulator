
using System;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics.Xna;

namespace Orts.Graphics.DrawableComponents
{
    public class TextShape : ResourceGameComponent<Texture2D>
    {
        private readonly SpriteBatch spriteBatch;
        private readonly TextTextureRenderer textRenderer; 

        public OutlineRenderOptions OutlineRenderOptions { get; set; }

        private TextShape(Game game, SpriteBatch spriteBatch) : base(game)
        {
            this.spriteBatch = spriteBatch;
            textRenderer = TextTextureRenderer.Instance(game) ?? throw new InvalidOperationException("TextTextureRenderer not found");
        }

        public static TextShape Instance(Game game, SpriteBatch spriteBatch)
        {
            if (null == game)
                throw new ArgumentNullException(nameof(game));

            TextShape instance;
            if ((instance = game.Components.OfType<TextShape>().FirstOrDefault()) == null)
            {
                instance = new TextShape(game, spriteBatch);
            }
            return instance;
        }

        /// <summary>
        /// Draw a text message (string) with transparent background
        /// to support redraw, compiled textures are cached for a short while <seealso cref="SweepInterval"/>
        /// </summary>
        public void DrawString(Vector2 point, Color color, string message, System.Drawing.Font font, Vector2 scale, 
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left, VerticalAlignment verticalAlignment = VerticalAlignment.Bottom,
            SpriteEffects effects = SpriteEffects.None, SpriteBatch spriteBatch = null)
        {
            int identifier = HashCode.Combine(font, message, OutlineRenderOptions);
            Texture2D texture = Get(identifier, () =>
            {
                return textRenderer.RenderText(message, font, OutlineRenderOptions);
            });
            point -= new Vector2(texture.Width * ((int)horizontalAlignment / 2f), texture.Height * ((int)verticalAlignment / 2f));

            (spriteBatch ?? this.spriteBatch).Draw(texture, point, null, color, 0, Vector2.Zero, scale, effects, 0);
        }
    }
}
