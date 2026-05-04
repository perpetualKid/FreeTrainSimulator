using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class MapOverlayBridge : IMapBaseOverlayContext, IMapInsetOverlayContext, IMapRulerOverlayContext, IMapCoordinateOverlayContext
    {
        private readonly ContentBase content;
        private readonly IMapViewStateAdapter viewStateAdapter;
        private readonly IMapOverlayShapeAdapter overlayShapeAdapter;

        public MapOverlayBridge(ContentBase content, IMapViewStateAdapter viewStateAdapter, IMapOverlayShapeAdapter overlayShapeAdapter)
        {
            this.content = content;
            this.viewStateAdapter = viewStateAdapter;
            this.overlayShapeAdapter = overlayShapeAdapter;
        }

        Rectangle IMapInsetOverlayContext.ContentBounds => content.Bounds;

        bool IMapRulerOverlayContext.UseMetricUnits => content.UseMetricUnits;

        double IMapRulerOverlayContext.Scale => viewStateAdapter.Scale;

        Point IMapRulerOverlayContext.WindowSize => viewStateAdapter.WindowSize;

        PointD IMapInsetOverlayContext.TopLeftBound => viewStateAdapter.TopLeftBound;

        PointD IMapInsetOverlayContext.BottomRightBound => viewStateAdapter.BottomRightBound;

        PointD IMapCoordinateOverlayContext.ScreenToWorldCoordinates(in Point screenLocation) => viewStateAdapter.ScreenToWorldCoordinates(screenLocation);

        void IMapInsetOverlayContext.DrawOverlayLine(float width, Color color, Vector2 point, float length, double angle, SpriteBatch spriteBatch)
        {
            overlayShapeAdapter.DrawLine(width, color, point, length, angle, spriteBatch);
        }

        void IMapInsetOverlayContext.DrawOverlayArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize, SpriteBatch spriteBatch)
        {
            overlayShapeAdapter.DrawArc(width, color, point, radius, angle, arcSize, spriteBatch);
        }

        void IMapInsetOverlayContext.DrawOverlayTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color, SpriteBatch spriteBatch)
        {
            overlayShapeAdapter.DrawTexture(texture, point, angle, size, color, spriteBatch);
        }
    }
}
