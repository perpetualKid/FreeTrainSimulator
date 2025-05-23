﻿
using System;
using System.Linq;

using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.DrawableComponents
{
    public class TextShape : ResourceGameComponent<Texture2D>
    {
        private readonly SpriteBatch spriteBatch;
        private readonly TextTextureRenderer textRenderer;

        private TextShape(Game game, SpriteBatch spriteBatch) : base(game)
        {
            this.spriteBatch = spriteBatch;
            textRenderer = TextTextureRenderer.Instance(game) ?? throw new InvalidOperationException("TextTextureRenderer not found");
        }

        public static TextShape Instance(Game game, SpriteBatch spriteBatch)
        {
            ArgumentNullException.ThrowIfNull(game);

            TextShape instance;
            if ((instance = game.Components.OfType<TextShape>().FirstOrDefault()) == null)
                instance = new TextShape(game, spriteBatch);
            return instance;
        }

        /// <summary>
        /// Draw a text message (string) with transparent background
        /// to support redraw, compiled textures are cached for a short while <seealso cref="SweepInterval"/>
        /// Outlined text will use the color from <paramref name="outlineRenderOptions"/>, and <paramref name="color"/> is ignored
        /// </summary>
        public void DrawString(Vector2 point, Color color, string message, System.Drawing.Font font, Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left, VerticalAlignment verticalAlignment = VerticalAlignment.Bottom,
            SpriteEffects effects = SpriteEffects.None, SpriteBatch spriteBatch = null, OutlineRenderOptions outlineRenderOptions = null)
        {
            int identifier = HashCode.Combine(font, message, outlineRenderOptions);
            Texture2D texture = Get(identifier, () =>
            {
                return textRenderer.RenderText(message, font, outlineRenderOptions);
            });
            Vector2 center = point;
            point -= new Vector2(texture.Width * ((int)horizontalAlignment / 2f), texture.Height * ((int)verticalAlignment / 2f));
            Vector2 vector = point - center;
            float x = (float)((Math.Cos(angle) * vector.X) - (Math.Sin(angle) * vector.Y));
            float y = (float)((Math.Sin(angle) * vector.X) + (Math.Cos(angle) * vector.Y));
            point = center + new Vector2(x, y);
            //p'x = cos(theta) * (px-ox) - sin(theta) * (py-oy) + ox
            //p'y = sin(theta) * (px-ox) + cos(theta) * (py-oy) + oy
            (spriteBatch ?? this.spriteBatch).Draw(texture, point, null, outlineRenderOptions == null ? color : Color.White, angle, Vector2.Zero, scale, effects, 0);
        }
    }
}
