
using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Graphics.MapView.Shapes;

using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace Orts.Graphics.MapView.Widgets
{
    internal class JunctionSegment : PointWidget
    {
        private const int diameter = 3;

        public IJunction Junction { get; }

        private readonly float direction;

        public JunctionSegment(TrackJunctionNode junctionNode, TrackVectorNode connectedVectorNode, TrackSections trackSections)
        {
            Size = diameter;
            ref readonly WorldLocation location = ref junctionNode.UiD.Location;
            base.location = PointD.FromWorldLocation(location);
            base.tile = new Tile(location.TileX, location.TileZ);
            Junction = RuntimeData.Instance.RuntimeReferenceResolver?.SwitchById(junctionNode.TrackCircuitCrossReferences[0].Index);

            if (connectedVectorNode.TrackVectorSections.Length < 1)
                throw new System.IO.InvalidDataException($"TrackVectorNode {connectedVectorNode.Index} has no TrackVectorSections attached.");
            // find the direction angle of the facing (in) track 
            if (junctionNode.TrackPins[0].Direction == TrackDirection.Reverse)
            {
                // if the attached track is reverse, we can take just the angle
                direction = connectedVectorNode.TrackVectorSections[0].Direction.Y + MathHelper.Pi;
            }
            else
            {
                // else we'll need to find the angle at the other end, which is same for straight tracks, but changes for curved tracks
                TrackSection trackSection = trackSections.TryGet(connectedVectorNode.TrackVectorSections[^1].SectionIndex);
                if (null == trackSection)
                    throw new System.IO.InvalidDataException($"TrackVectorSection {connectedVectorNode.TrackVectorSections[^1].SectionIndex} not found in TSection.dat");

                if (trackSection.Curved) 
                {
                    direction = connectedVectorNode.TrackVectorSections[^1].Direction.Y + MathHelper.ToRadians(trackSection.Angle);
                }
                else
                    direction = connectedVectorNode.TrackVectorSections[^1].Direction.Y;
            }
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Size = contentArea.Scale switch
            {
                double i when i < 0.5 => 30,
                double i when i < 0.75 => 15,
                double i when i < 1 => 10,
                double i when i < 3 => 7,
                double i when i < 5 => 5,
                double i when i < 8 => 4,
                _ => 3,
            };

            Color drawColor = GetColor<JunctionSegment>(colorVariation);
            BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
            //BasicShapes.DrawTexture(BasicTextureType.PathNormal, contentArea.WorldToScreenCoordinates(in Location), direction, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }
    }
}
