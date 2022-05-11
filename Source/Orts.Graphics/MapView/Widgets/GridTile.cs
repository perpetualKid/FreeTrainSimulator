
using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    internal class GridTile: VectorWidget, ITileCoordinate<Tile>
    {
        private readonly PointD upperLeft;
        private readonly PointD lowerRight;

        static GridTile()
        {
            SetColors<GridTile>(Color.Black);
        }

        public GridTile(ITile tile)
        {
            if (tile is Tile t)
                this.tile = t;
            else
                this.tile = new Tile(tile);

            location = PointD.FromWorldLocation(new WorldLocation(this.tile.X, this.tile.Z, -1024, 0, -1024));
            upperLeft = PointD.FromWorldLocation(new WorldLocation(this.tile.X, this.tile.Z, -1024, 0, 1024));
            lowerRight = PointD.FromWorldLocation(new WorldLocation(this.tile.X, this.tile.Z, 1024, 0, -1024));
            vectorEnd = PointD.FromWorldLocation(new WorldLocation(this.tile.X, this.tile.Z, 1024, 0, 1024));

        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color color = GetColor<GridTile>(colorVariation);
            BasicShapes.DrawLine((float)(1 * scaleFactor), color, contentArea.WorldToScreenCoordinates(location), contentArea.WorldToScreenCoordinates(lowerRight), contentArea.SpriteBatch);
            BasicShapes.DrawLine((float)(1 * scaleFactor), color, contentArea.WorldToScreenCoordinates(lowerRight), contentArea.WorldToScreenCoordinates(vectorEnd), contentArea.SpriteBatch);
            BasicShapes.DrawLine((float)(1 * scaleFactor), color, contentArea.WorldToScreenCoordinates(location), contentArea.WorldToScreenCoordinates(upperLeft), contentArea.SpriteBatch);
            BasicShapes.DrawLine((float)(1 * scaleFactor), color, contentArea.WorldToScreenCoordinates(upperLeft), contentArea.WorldToScreenCoordinates(vectorEnd), contentArea.SpriteBatch);
        }

        public override double DistanceSquared(in PointD point)
        {
            throw new System.NotImplementedException();
        }
    }
}
