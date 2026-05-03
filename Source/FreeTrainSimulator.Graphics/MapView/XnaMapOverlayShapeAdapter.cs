using FreeTrainSimulator.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapOverlayShapeAdapter : IMapOverlayShapeAdapter
    {
        private readonly BasicShapes basicShapes;

        public XnaMapOverlayShapeAdapter(BasicShapes basicShapes)
        {
            this.basicShapes = basicShapes;
        }

        public void DrawLine(float width, Color color, Vector2 point, float length, double angle, SpriteBatch spriteBatch)
        {
            basicShapes.DrawLine(width, color, point, length, angle, spriteBatch);
        }

        public void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize, SpriteBatch spriteBatch)
        {
            basicShapes.DrawArc(width, color, point, radius, angle, arcSize, spriteBatch);
        }

        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color, SpriteBatch spriteBatch)
        {
            basicShapes.DrawTexture(texture, point, angle, size, color, spriteBatch);
        }
    }
}
