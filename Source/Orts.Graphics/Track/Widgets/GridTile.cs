
using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Graphics.Track.Shapes;

namespace Orts.Graphics.Track.Widgets
{
    internal class GridTile: PointWidget, ITileCoordinate<Tile>
    {
        private readonly PointD lowerLeft;
        private readonly PointD upperLeft;
        private readonly PointD lowerRight;
        private readonly PointD upperRight;

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

            lowerLeft = PointD.FromWorldLocation(new WorldLocation(this.tile.X, this.tile.Z, -1024, 0, -1024));
            upperLeft = PointD.FromWorldLocation(new WorldLocation(this.tile.X, this.tile.Z, -1024, 0, 1024));
            lowerRight = PointD.FromWorldLocation(new WorldLocation(this.tile.X, this.tile.Z, 1024, 0, -1024));
            upperRight = PointD.FromWorldLocation(new WorldLocation(this.tile.X, this.tile.Z, 1024, 0, 1024));
            location = lowerLeft;

        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            Color color = GetColor<GridTile>(colorVariation);
            BasicShapes.DrawLine(1, color, contentArea.WorldToScreenCoordinates(lowerLeft), contentArea.WorldToScreenCoordinates(lowerRight), contentArea.SpriteBatch);
            BasicShapes.DrawLine(1, color, contentArea.WorldToScreenCoordinates(lowerRight), contentArea.WorldToScreenCoordinates(upperRight), contentArea.SpriteBatch);
            BasicShapes.DrawLine(1, color, contentArea.WorldToScreenCoordinates(lowerLeft), contentArea.WorldToScreenCoordinates(upperLeft), contentArea.SpriteBatch);
            BasicShapes.DrawLine(1, color, contentArea.WorldToScreenCoordinates(upperLeft), contentArea.WorldToScreenCoordinates(upperRight), contentArea.SpriteBatch);
        }
    }
}
