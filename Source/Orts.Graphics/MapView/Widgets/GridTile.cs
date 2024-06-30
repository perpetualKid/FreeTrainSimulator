
using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

namespace Orts.Graphics.MapView.Widgets
{
    internal class GridTile: VectorPrimitive, IDrawable<VectorPrimitive>
    {
        private readonly PointD upperLeft;
        private readonly PointD lowerRight;

        static GridTile()
        {
            WidgetColorCache.SetColors<GridTile>(Color.Black);
        }

        public GridTile(ITile tile): base(WorldLocationFromTile(tile, -1024, -1024), WorldLocationFromTile(tile, 1024, 1024))
        {
            upperLeft = PointD.FromWorldLocation(WorldLocationFromTile(tile, -1024, 1024));
            lowerRight = PointD.FromWorldLocation(WorldLocationFromTile(tile, 1024, -1024));
        }

        private static WorldLocation WorldLocationFromTile(ITile tile, int x, int z)
        {
            return new WorldLocation(tile.X, tile.Z, x, 0, z);
        }

        public void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color color = this.GetColor<GridTile>(colorVariation);
            contentArea.BasicShapes.DrawLine((float)(1 * scaleFactor), color, contentArea.WorldToScreenCoordinates(Location), contentArea.WorldToScreenCoordinates(lowerRight), contentArea.SpriteBatch);
            contentArea.BasicShapes.DrawLine((float)(1 * scaleFactor), color, contentArea.WorldToScreenCoordinates(lowerRight), contentArea.WorldToScreenCoordinates(Vector), contentArea.SpriteBatch);
            contentArea.BasicShapes.DrawLine((float)(1 * scaleFactor), color, contentArea.WorldToScreenCoordinates(Location), contentArea.WorldToScreenCoordinates(upperLeft), contentArea.SpriteBatch);
            contentArea.BasicShapes.DrawLine((float)(1 * scaleFactor), color, contentArea.WorldToScreenCoordinates(upperLeft), contentArea.WorldToScreenCoordinates(Vector), contentArea.SpriteBatch);
        }

        public override double DistanceSquared(in PointD point)
        {
            throw new System.NotImplementedException();
        }
    }
}
