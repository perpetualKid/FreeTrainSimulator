using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapOverlayContext
    {
        Rectangle ContentBounds { get; }

        bool UseMetricUnits { get; }

        double Scale { get; }

        Point WindowSize { get; }

        PointD TopLeftBound { get; }

        PointD BottomRightBound { get; }

        PointD ScreenToWorldCoordinates(in Point screenLocation);

        void DrawOverlayLine(float width, Color color, Vector2 point, float length, double angle, SpriteBatch spriteBatch);

        void DrawOverlayArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize, SpriteBatch spriteBatch);

        void DrawOverlayTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color, SpriteBatch spriteBatch);
    }
}
