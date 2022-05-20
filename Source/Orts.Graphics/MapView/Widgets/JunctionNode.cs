using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Common.DebugInfo;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    /// <summary>
    /// Graphical representation of a track junction (switch)
    /// </summary>
    internal class JunctionNode : PointPrimitive, IDrawable<PointPrimitive>, INameValueInformationProvider
    {
        private const int diameter = 3;
        private protected static NameValueCollection debugInformation = new NameValueCollection() { ["Node Type"] = "Junction" };

        internal readonly int TrackNodeIndex;
        internal readonly float Direction;

        public JunctionNode(TrackJunctionNode junctionNode, List<TrackVectorNode> vectorNodes, TrackSections trackSections): base(junctionNode.UiD.Location)
        {
            Size = diameter;
            TrackNodeIndex = junctionNode.Index;
            Direction = MathHelper.WrapAngle(GetInboundSectionDirection(vectorNodes[0], junctionNode.TrackPins[0].Direction == TrackDirection.Reverse, trackSections));

        }

        public NameValueCollection DebugInfo
        {
            get
            {
                debugInformation["Node Index"] = TrackNodeIndex.ToString(CultureInfo.InvariantCulture);
                return debugInformation;
            }
        }

        public Dictionary<string, FormatOption> FormattingOptions => null;

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Size = contentArea.Scale switch
            {
                double i when i < 0.3 => 30,
                double i when i < 0.5 => 20,
                double i when i < 0.75 => 15,
                double i when i < 1 => 10,
                double i when i < 3 => 7,
                double i when i < 5 => 5,
                double i when i < 8 => 4,
                _ => 3,
            };

            Color drawColor = this.GetColor<JunctionNode>(colorVariation);
            BasicShapes.DrawTexture(BasicTextureType.PathNormal, contentArea.WorldToScreenCoordinates(in Location), Direction, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }

        // find the direction angle of the facing (in) track 
        private protected static float GetInboundSectionDirection(TrackVectorNode vectorNode, bool reverse, TrackSections trackSections)
        {
            if (null == vectorNode)
                return 0;
            if (vectorNode.TrackVectorSections.Length < 1)
                throw new System.IO.InvalidDataException($"TrackVectorNode {vectorNode.Index} has no TrackVectorSections attached.");
            // find the direction angle of the facing (in) track 
            if (reverse)
            {
                // if the attached track is reverse, we can take just the angle
                return vectorNode.TrackVectorSections[0].Direction.Y + MathHelper.Pi;
            }
            else
            {
                // else we'll need to find the angle at the other end, which is same for straight tracks, but changes for curved tracks
                TrackSection trackSection = trackSections.TryGet(vectorNode.TrackVectorSections[^1].SectionIndex);
                return null == trackSection
                    ? throw new System.IO.InvalidDataException($"TrackVectorSection {vectorNode.TrackVectorSections[^1].SectionIndex} not found in TSection.dat")
                    : trackSection.Curved
                    ? vectorNode.TrackVectorSections[^1].Direction.Y + MathHelper.ToRadians(trackSection.Angle)
                    : vectorNode.TrackVectorSections[^1].Direction.Y;
            }
        }

        // find the direction angle of the trailing (out) track 
        private protected static float GetOutboundSectionDirection(TrackVectorNode vectorNode, bool reverse, TrackSections trackSections, int index)
        {
            if (vectorNode.TrackVectorSections.Length < 1)
                throw new System.IO.InvalidDataException($"TrackVectorNode {vectorNode.Index} has no TrackVectorSections attached.");
            if (vectorNode.TrackVectorSections.Length < 1 + index)
                return float.NaN;
            // find the direction angle of the trailing (out) track 
            if (reverse)
            {
                // if the attached track is reverse, we'll need to find the angle at the other end, which is same for straight tracks, but changes for curved tracks
                TrackSection trackSection = trackSections.TryGet(vectorNode.TrackVectorSections[0].SectionIndex);
                return null == trackSection
                    ? throw new System.IO.InvalidDataException($"TrackVectorSection {vectorNode.TrackVectorSections[0].SectionIndex} not found in TSection.dat")
                    : trackSection.Curved
                    ? vectorNode.TrackVectorSections[index].Direction.Y + MathHelper.ToRadians(trackSection.Angle)
                    : vectorNode.TrackVectorSections[index].Direction.Y;
            }
            else
            {
                // else we can take just the angle
                return vectorNode.TrackVectorSections[^(1 + index)].Direction.Y + MathHelper.Pi;
            }
        }

    }

    /// <summary>
    /// Junction segment <seealso cref="JunctionNode"/> which holds a reference to an active <see cref="IJunction"> to allow for interaction/show interactive status 
    /// </summary>
    internal class ActiveJunctionSegment : JunctionNode
    {
        private readonly float[] trackSectionAngles;

        public IJunction Junction { get; }

        public ActiveJunctionSegment(TrackJunctionNode junctionNode, List<TrackVectorNode> vectorNodes, TrackSections trackSections) : base(junctionNode, vectorNodes, trackSections)
        {
            trackSectionAngles = new float[vectorNodes.Count - 1];
            Junction = RuntimeData.Instance.RuntimeReferenceResolver?.SwitchById(junctionNode.TrackCircuitCrossReferences[0].Index);

            int trial = 0;
            while (trial < 3)
            {
                for (int i = 1; i < vectorNodes.Count; i++)
                {
                    float direction = GetOutboundSectionDirection(vectorNodes[i], junctionNode.TrackPins[i].Direction == TrackDirection.Reverse, trackSections, trial);
                    if (float.IsNaN(direction))
                        break;
                    trackSectionAngles[i - 1] = MathHelper.WrapAngle(direction);
                }
                if (trackSectionAngles[0].AlmostEqual(trackSectionAngles[1], 0.001f))
                    trial++;
                else
                    break;
            }

            //if main route is not in OutPin[0] but OutPin[1], swap the both
            if ((int)Junction.State != junctionNode.SelectedRoute)
                (trackSectionAngles[0], trackSectionAngles[1]) = (trackSectionAngles[1], trackSectionAngles[0]);

        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Size = contentArea.Scale switch
            {
                double i when i < 0.3 => 30,
                double i when i < 0.5 => 20,
                double i when i < 0.75 => 15,
                double i when i < 1 => 10,
                double i when i < 3 => 7,
                double i when i < 5 => 5,
                double i when i < 8 => 4,
                _ => 3,
            };

            Color drawColor = this.GetColor<JunctionNode>(Junction.State == SwitchState.MainRoute ? ColorVariation.Complement : ColorVariation.None);
            BasicShapes.DrawTexture(BasicTextureType.PathNormal, contentArea.WorldToScreenCoordinates(in Location), trackSectionAngles[(int)Junction.State], contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }

    }
}
