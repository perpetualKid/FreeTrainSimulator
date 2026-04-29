using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal record GridTile : VectorPrimitive, IDrawable<VectorPrimitive>
    {
        private readonly PointD upperLeft;
        private readonly PointD lowerRight;

        static GridTile()
        {
            WidgetDrawingOptions<GridTile>.SetColors(Color.Black);
        }

        public GridTile(Tile tile) : base(WorldLocationFromTile(tile, -1024, -1024), WorldLocationFromTile(tile, 1024, 1024))
        {
            upperLeft = PointD.FromWorldLocation(WorldLocationFromTile(tile, -1024, 1024));
            lowerRight = PointD.FromWorldLocation(WorldLocationFromTile(tile, 1024, -1024));
        }

        private static WorldLocation WorldLocationFromTile(Tile tile, int x, int z)
        {
            return new WorldLocation(tile.X, tile.Z, x, 0, z);
        }

        public void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color color = WidgetDrawingOptions<GridTile>.Colors[colorVariation];
            renderer.DrawLine((float)(1 * scaleFactor), color, renderer.WorldToScreenCoordinates(Location), renderer.WorldToScreenCoordinates(lowerRight));
            renderer.DrawLine((float)(1 * scaleFactor), color, renderer.WorldToScreenCoordinates(lowerRight), renderer.WorldToScreenCoordinates(Vector));
            renderer.DrawLine((float)(1 * scaleFactor), color, renderer.WorldToScreenCoordinates(Location), renderer.WorldToScreenCoordinates(upperLeft));
            renderer.DrawLine((float)(1 * scaleFactor), color, renderer.WorldToScreenCoordinates(upperLeft), renderer.WorldToScreenCoordinates(Vector));
        }

        public override double DistanceSquared(in PointD point)
        {
            throw new System.NotImplementedException();
        }
    }
}
