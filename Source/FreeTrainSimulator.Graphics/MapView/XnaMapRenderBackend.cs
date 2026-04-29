using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapRenderBackend : IMapRenderBackend
    {
        private readonly SpriteBatch spriteBatch;
        private readonly BasicShapes basicShapes;
        private readonly IMapTextRenderer textRenderer;

        public XnaMapRenderBackend(SpriteBatch spriteBatch, BasicShapes basicShapes, IMapTextRenderer textRenderer)
        {
            this.spriteBatch = spriteBatch;
            this.basicShapes = basicShapes;
            this.textRenderer = textRenderer;
        }

        public void BeginFrame()
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
        }

        public void EndFrame()
        {
            spriteBatch.End();
        }

        public void DrawLine(float width, Color color, Vector2 point, float length, double angle)
        {
            basicShapes.DrawLine(width, color, point, length, angle, spriteBatch);
        }

        public void DrawLine(float width, Color color, Vector2 point1, Vector2 point2)
        {
            basicShapes.DrawLine(width, color, point1, point2, spriteBatch);
        }

        public void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2)
        {
            basicShapes.DrawDashedLine(width, color, point1, point2, spriteBatch);
        }

        public void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize)
        {
            basicShapes.DrawArc(width, color, point, radius, angle, arcSize, spriteBatch);
        }

        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, bool flipHorizontal, bool flipVertical, bool highlight)
        {
            basicShapes.DrawTexture(texture, point, angle, size, flipHorizontal, flipVertical, highlight, spriteBatch);
        }

        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color)
        {
            basicShapes.DrawTexture(texture, point, angle, size, color, spriteBatch);
        }

        public void DrawText(Vector2 point, Color color, string text, System.Drawing.Font font, Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, OutlineRenderOptions outlineRenderOptions)
        {
            textRenderer.DrawString(point, color, text, font, scale, angle, horizontalAlignment, verticalAlignment, outlineRenderOptions);
        }
    }
}
