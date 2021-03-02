using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.View.Track.Shapes;

namespace Orts.View.Track.Widgets
{
    internal class TrackEndSegment: PointWidget
    {
        private const int width = 3;

        internal const int Length = 2;

        internal readonly float Direction;

        public TrackEndSegment(TrackEndNode trackEndNode, TrackVectorNode connectedVectorNode, TrackSections sections)
        {
            ref readonly WorldLocation location = ref trackEndNode.UiD.Location;
            base.location = PointD.FromWorldLocation(location);
            tile = new Tile(location.TileX, location.TileZ);
            Size = width;

            if (null == connectedVectorNode)
                return;
            if (connectedVectorNode.TrackPins[0].Link == trackEndNode.Index)
            {
                //find angle at beginning of vector node
                TrackVectorSection tvs = connectedVectorNode.TrackVectorSections[0];
                Direction = tvs.Direction.Y;
            }
            else
            {
                //find angle at end of vector node
                TrackVectorSection tvs = connectedVectorNode.TrackVectorSections.Last();
                Direction = tvs.Direction.Y;
                // try to get even better in case the last section is curved
                TrackSection section = sections.Get(tvs.SectionIndex);
                if (section != null && section.Curved)
                {
                    Direction += MathHelper.ToRadians(section.Angle);
                }
            }
            Direction -= MathHelper.PiOver2;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            Color drawColor = GetColor<TrackEndSegment>(colorVariation);
            BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }
}
