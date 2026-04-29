using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapTextRenderer : IMapTextRenderer
    {
        private readonly DrawableComponents.TextShape textShape;
        private readonly SpriteBatch spriteBatch;

        public XnaMapTextRenderer(DrawableComponents.TextShape textShape, SpriteBatch spriteBatch)
        {
            this.textShape = textShape;
            this.spriteBatch = spriteBatch;
        }

        public void DrawString(Vector2 point, Color color, string message, System.Drawing.Font font, Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left, VerticalAlignment verticalAlignment = VerticalAlignment.Bottom,
            OutlineRenderOptions outlineRenderOptions = null)
        {
            textShape.DrawString(point, color, message, font, scale, angle, horizontalAlignment, verticalAlignment, SpriteEffects.None, spriteBatch, outlineRenderOptions);
        }
    }
}
