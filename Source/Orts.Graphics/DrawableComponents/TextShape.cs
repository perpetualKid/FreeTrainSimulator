
using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics.Xna;

namespace Orts.Graphics.DrawableComponents
{
    public class TextShape : ResourceGameComponent<Texture2D>
    {
        [ThreadStatic]
        private static TextShape instance;
        private readonly SpriteBatch spriteBatch;

        private readonly System.Drawing.Bitmap measureBitmap = new System.Drawing.Bitmap(1, 1);
        private readonly System.Drawing.Graphics measureGraphics;

        private TextShape(Game game, SpriteBatch spriteBatch) : base(game)
        {
            this.spriteBatch = spriteBatch;
            measureGraphics = System.Drawing.Graphics.FromImage(measureBitmap);
        }

        public static void Initialize(Game game, SpriteBatch spriteBatch)
        {
            if (null == game)
                throw new ArgumentNullException(nameof(game));
            if (null == instance)
                instance = new TextShape(game, spriteBatch);
        }

        /// <summary>
        /// Draw a text message (string) with transparent background
        /// to support redraw, compiled textures are cached for a short while <seealso cref="SweepInterval"/>
        /// </summary>
        public static void DrawString(Vector2 point, Color color, string message, System.Drawing.Font font, Vector2 scale, 
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left, VerticalAlignment verticalAlignment = VerticalAlignment.Bottom,
            SpriteEffects effects = SpriteEffects.None, SpriteBatch spriteBatch = null)
        {
            int identifier = HashCode.Combine(font, message);
            if (!instance.currentResources.TryGetValue(identifier, out Texture2D texture))
            {
                if (!instance.previousResources.TryGetValue(identifier, out texture))
                {
                    texture = TextTextureRenderer.Resize(message, font, instance.Game.GraphicsDevice, instance.measureGraphics);
                    TextTextureRenderer.RenderText(message, font, texture);
                    instance.currentResources.Add(identifier, texture);
                }
                else
                {
                    instance.currentResources.Add(identifier, texture);
                    instance.previousResources.Remove(identifier);
                }
            }
            point -= new Vector2(texture.Width * ((int)horizontalAlignment / 2f), texture.Height * ((int)verticalAlignment / 2f));

            (spriteBatch ?? instance.spriteBatch).Draw(texture, point, null, color, 0, Vector2.Zero, scale, effects, 0);
        }
    }
}
