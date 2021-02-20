
using System;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.View.Track.Shapes;
using Orts.View.Xna;

namespace Orts.View.Track.Widgets
{
    internal class GridTile: PointWidget, ITileCoordinate<Tile>
    {
        private readonly PointD lowerLeft;
        private readonly PointD upperLeft;
        private readonly PointD lowerRight;
        private readonly PointD upperRight;

        [ThreadStatic]
        private static readonly Color color = Color.Black;
        [ThreadStatic]
        private static readonly Color colorHighlight = ColorExtension.ComplementColor(color);

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

        internal override void Draw(ContentArea contentArea)
        {            
            BasicShapes.DrawLine(1, color, contentArea.WorldToScreenCoordinates(lowerLeft), contentArea.WorldToScreenCoordinates(lowerRight), contentArea.SpriteBatch);
            BasicShapes.DrawLine(1, color, contentArea.WorldToScreenCoordinates(lowerRight), contentArea.WorldToScreenCoordinates(upperRight), contentArea.SpriteBatch);
            BasicShapes.DrawLine(1, color, contentArea.WorldToScreenCoordinates(lowerLeft), contentArea.WorldToScreenCoordinates(upperLeft), contentArea.SpriteBatch);
            BasicShapes.DrawLine(1, color, contentArea.WorldToScreenCoordinates(upperLeft), contentArea.WorldToScreenCoordinates(upperRight), contentArea.SpriteBatch);
        }

        internal override void DrawHighlight(ContentArea contentArea)
        {
            BasicShapes.DrawLine(1, colorHighlight, contentArea.WorldToScreenCoordinates(lowerLeft), contentArea.WorldToScreenCoordinates(lowerRight), contentArea.SpriteBatch);
            BasicShapes.DrawLine(1, colorHighlight, contentArea.WorldToScreenCoordinates(lowerRight), contentArea.WorldToScreenCoordinates(upperRight), contentArea.SpriteBatch);
            BasicShapes.DrawLine(1, colorHighlight, contentArea.WorldToScreenCoordinates(lowerLeft), contentArea.WorldToScreenCoordinates(upperLeft), contentArea.SpriteBatch);
            BasicShapes.DrawLine(1, colorHighlight, contentArea.WorldToScreenCoordinates(upperLeft), contentArea.WorldToScreenCoordinates(upperRight), contentArea.SpriteBatch);
        }
    }
}
