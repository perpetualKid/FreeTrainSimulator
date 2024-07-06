using System;
using System.Collections.Generic;
using System.Globalization;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Models.Track;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    /// <summary>
    /// Graphical representation of a track junction (switch)
    /// </summary>
    internal class JunctionNode : JunctionNodeBase, IDrawable<PointPrimitive>, INameValueInformationProvider
    {
        private const int diameter = 3;
        private protected static InformationDictionary debugInformation = new InformationDictionary() { ["Node Type"] = "Junction" };

        public JunctionNode(TrackJunctionNode junctionNode, int mainRoute, List<TrackVectorNode> vectorNodes, TrackSections trackSections) :
            base(junctionNode, mainRoute, vectorNodes, trackSections)
        {
            Size = diameter;
        }

        public InformationDictionary DetailInfo
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
            Size = Math.Max(2.5f, (float)(4 / contentArea.Scale));

            Color drawColor = this.GetColor<JunctionNode>(colorVariation);
            contentArea.BasicShapes.DrawTexture(BasicTextureType.PathNormal, contentArea.WorldToScreenCoordinates(in Location), Direction, contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }
    }

    /// <summary>
    /// Junction segment <seealso cref="JunctionNode"/> which holds a reference to an active <see cref="IJunction"> to allow for interaction/show interactive status 
    /// </summary>
    internal class ActiveJunctionSegment : JunctionNode
    {
        private readonly float[] trackSectionAngles;

        public IJunction Junction { get; }

        public ActiveJunctionSegment(TrackJunctionNode junctionNode, int mainRoute, List<TrackVectorNode> vectorNodes, TrackSections trackSections) :
            base(junctionNode, mainRoute, vectorNodes, trackSections)
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
            contentArea.BasicShapes.DrawTexture(BasicTextureType.PathNormal, contentArea.WorldToScreenCoordinates(in Location), trackSectionAngles[(int)Junction.State], contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.SpriteBatch);
        }

    }
}
