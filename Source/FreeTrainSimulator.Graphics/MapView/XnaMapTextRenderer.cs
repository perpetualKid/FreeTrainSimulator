using System;

using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapTextRenderer : IMapTextRenderer
    {
        private readonly IMapTextCache textCache;
        private readonly SpriteBatch spriteBatch;

        public XnaMapTextRenderer(IMapTextCache textCache, SpriteBatch spriteBatch)
        {
            this.textCache = textCache;
            this.spriteBatch = spriteBatch;
        }

        public void DrawString(Vector2 point, Color color, string message, System.Drawing.Font font, Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left, VerticalAlignment verticalAlignment = VerticalAlignment.Bottom,
            OutlineRenderOptions outlineRenderOptions = null)
        {
            Texture2D texture = textCache.GetTextTexture(message, font, outlineRenderOptions);
            Vector2 center = point;
            point -= new Vector2(texture.Width * ((int)horizontalAlignment / 2f), texture.Height * ((int)verticalAlignment / 2f));
            Vector2 vector = point - center;
            float x = (float)((Math.Cos(angle) * vector.X) - (Math.Sin(angle) * vector.Y));
            float y = (float)((Math.Sin(angle) * vector.X) + (Math.Cos(angle) * vector.Y));
            point = center + new Vector2(x, y);
            spriteBatch.Draw(texture, point, null, outlineRenderOptions == null ? color : Color.White, angle, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
    }
}
