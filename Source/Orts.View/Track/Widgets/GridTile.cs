
using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.View.Track.Shapes;

namespace Orts.View.Track.Widgets
{
    internal class GridTile : ITileCoordinate<Tile>
    {
        private readonly Tile tile;
        private readonly PointD lowerLeft;
        private readonly PointD upperLeft;
        private readonly PointD lowerRight;
        private readonly PointD upperRight;

        public ref readonly Tile Tile => ref tile;

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

        }

        internal void Draw(ContentArea contentArea)
        {
            BasicShapes.DrawLine(1, Color.Black, contentArea.WorldToScreenCoordinates(lowerLeft), contentArea.WorldToScreenCoordinates(lowerRight), contentArea.SpriteBatch);
            BasicShapes.DrawLine(1, Color.Black, contentArea.WorldToScreenCoordinates(lowerRight), contentArea.WorldToScreenCoordinates(upperRight), contentArea.SpriteBatch);
            BasicShapes.DrawLine(1, Color.Black, contentArea.WorldToScreenCoordinates(lowerLeft), contentArea.WorldToScreenCoordinates(upperLeft), contentArea.SpriteBatch);
            BasicShapes.DrawLine(1, Color.Black, contentArea.WorldToScreenCoordinates(upperLeft), contentArea.WorldToScreenCoordinates(upperRight), contentArea.SpriteBatch);
        }

    }
}
