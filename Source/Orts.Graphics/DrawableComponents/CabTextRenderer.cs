
using System;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Graphics.MapView.Shapes;
using Orts.Graphics.Xna;

namespace Orts.Graphics.DrawableComponents
{
    /// <summary>
    /// Render text for in-game cab controls
    /// </summary>
    public class CabTextRenderer : ResourceGameComponent<Texture2D>
    {
        private readonly TextTextureRenderer textRenderer;

        private CabTextRenderer(Game game) : base(game)
        {
            textRenderer = TextTextureRenderer.Instance(game) ?? throw new InvalidOperationException("TextTextureRenderer not found");
            SweepInterval = 15;
        }

        public static CabTextRenderer Instance(Game game)
        {
            ArgumentNullException.ThrowIfNull(game);

            CabTextRenderer instance;
            if ((instance = game.Components.OfType<CabTextRenderer>().FirstOrDefault()) == null)
            {
                instance = new CabTextRenderer(game);
            }
            return instance;
        }

        public Texture2D Prepare(string text, System.Drawing.Font font, OutlineRenderOptions outlineOptions = null)
        {
            int identifier = HashCode.Combine(font, text, outlineOptions);
            return Get(identifier, () =>
            {
                return textRenderer.RenderText(text, font, outlineOptions);
            });
        }

        public static void DrawTextTexture(SpriteBatch spriteBatch, Texture2D texture, Rectangle target, Color color, float rotation,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center, VerticalAlignment verticalAlignment = VerticalAlignment.Top)
        {
            ArgumentNullException.ThrowIfNull(texture);
            ArgumentNullException.ThrowIfNull(spriteBatch);

            Vector2 position = new Vector2(target.Left + (target.Width - texture.Width) * ((int)horizontalAlignment / 2f), target.Top + (target.Height - texture.Height) * ((int)verticalAlignment / 2f));
            spriteBatch.Draw(texture, position, null, color, rotation, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
        }

        public void DrawString(SpriteBatch spriteBatch, Vector2 position, string text, System.Drawing.Font font, Color color)
        {
            ArgumentNullException.ThrowIfNull(spriteBatch);
            Texture2D texture = Prepare(text, font, null);
            spriteBatch.Draw(texture, position, null, color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
        }
    }
}
