using FreeTrainSimulator.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapOverlayShapeAdapter
    {
        void DrawLine(float width, Color color, Vector2 point, float length, double angle, SpriteBatch spriteBatch);

        void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize, SpriteBatch spriteBatch);

        void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color, SpriteBatch spriteBatch);
    }
}
