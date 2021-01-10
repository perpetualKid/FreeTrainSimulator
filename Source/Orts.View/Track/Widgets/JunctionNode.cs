
using Orts.Common.Position;
using Orts.Formats.Msts.Models;

namespace Orts.View.Track.Widgets
{
    internal class JunctionNode: PointWidget
    {
        private const int diameter = 3;

        public JunctionNode(TrackJunctionNode junctionNode)
        {
            Width = diameter;
            ref readonly WorldLocation location = ref junctionNode.UiD.Location;
            base.location = PointD.FromWorldLocation(location);

        }
    }
}
