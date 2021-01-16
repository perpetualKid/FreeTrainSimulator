
using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.View.Track.Shapes;

namespace Orts.View.Track.Widgets
{
    internal class JunctionNode: PointWidget
    {
        private const int diameter = 3;

        public JunctionNode(TrackJunctionNode junctionNode)
        {
            Size = diameter;
            ref readonly WorldLocation location = ref junctionNode.UiD.Location;
            base.location = PointD.FromWorldLocation(location);
        }

        internal override void Draw(ContentArea contentArea)
        {
            BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), Color.DarkRed, false, false, false, contentArea.SpriteBatch);
        }
    }
}
