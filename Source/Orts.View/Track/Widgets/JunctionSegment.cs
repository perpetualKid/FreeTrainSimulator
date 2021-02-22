
using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.View.Track.Shapes;

namespace Orts.View.Track.Widgets
{
    internal class JunctionSegment: PointWidget
    {
        private const int diameter = 3;

        public JunctionSegment(TrackJunctionNode junctionNode)
        {
            Size = diameter;
            ref readonly WorldLocation location = ref junctionNode.UiD.Location;
            base.location = PointD.FromWorldLocation(location);
            base.tile = new Tile(location.TileX, location.TileZ);
        }

        internal override void Draw(ContentArea contentArea, bool highlight = false)
        {
            BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), Color.DarkRed, false, false, false, contentArea.SpriteBatch);
        }
    }
}
