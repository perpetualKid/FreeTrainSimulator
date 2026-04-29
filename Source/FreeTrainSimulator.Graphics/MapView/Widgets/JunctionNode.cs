using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    /// <summary>
    /// Graphical representation of a track junction (switch)
    /// </summary>
    internal record JunctionNode : JunctionNodeBase, IDrawable<PointPrimitive>, INameValueInformationProvider
    {
        private const int diameter = 4;
        private protected static InformationDictionary debugInformation = new InformationDictionary() { ["Node Type"] = "Junction" };

        public JunctionNode(Models.Track.JunctionNode junctionNode, int mainRouteNode) :
            base(junctionNode, mainRouteNode)
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

        public virtual void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Size = renderer.Scale switch
            {
                double i when i < 0.5 => 30,
                double i when i < 0.75 => 15,
                double i when i < 1 => 12,
                double i when i < 5 => 8,
                double i when i < 10 => 6,
                double i when i < 20 => 4,
                _ => 2f,
            };


            scaleFactor *= WidgetDrawingOptions<JunctionNode>.ScaleFactor;

            Color drawColor = WidgetDrawingOptions<JunctionNode>.Colors[colorVariation];
            renderer.DrawTexture(renderer.Scale > 4 ? BasicTextureType.Ring : BasicTextureType.RingBold, renderer.WorldToScreenCoordinates(in Location), Direction, renderer.WorldToScreenSize(Size * scaleFactor), drawColor);
        }

        public static void UpdateTrackWidthRatio(bool downscale)
        {
            WidgetDrawingOptions<JunctionNode>.ScaleFactor = downscale ? 2.0 / 3 : 1;
        }
    }

    /// <summary>
    /// Junction segment <seealso cref="JunctionNode"/> which holds a reference to an active <see cref="IJunction"> to allow for interaction/show interactive status 
    /// </summary>
    internal record ActiveJunctionSegment : JunctionNode
    {
        private readonly float[] trackSectionAngles;

        public IJunction Junction { get; }

        public ActiveJunctionSegment(Models.Track.JunctionNode junctionNode, int mainRouteNode) :
            base(junctionNode, mainRouteNode)
        {

            Junction = RuntimeDataResolver.Instance.RuntimeReferenceResolver?.SwitchByNodeIndex(junctionNode.NodeIndex);
            ImmutableArray<TrackNodeConnector> connectors = RuntimeDataResolver.Instance.TrackWorld.TrackDatabase.TrackNodeConnectors[TrackNodeIndex].TrackNodeConnectors;
            trackSectionAngles = new float[connectors.Length - 1];

            int trial = 0;
            while (trial < 3)
            {
                for (int i = 1; i < connectors.Length; i++)
                {
                    TrackNodeConnector connector = connectors[i];
                    VectorNode vectorNode = RuntimeDataResolver.Instance.TrackWorld.TrackDatabase.TrackNodes[connector.Link] as VectorNode;
                    float direction = GetOutboundSectionDirection(vectorNode, connectors[i].Direction == TrackDirection.Reverse, trial);
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
            if ((int)Junction.State != RuntimeDataResolver.Instance.TrackWorld.SwitchStates[junctionNode.NodeIndex])
                (trackSectionAngles[0], trackSectionAngles[1]) = (trackSectionAngles[1], trackSectionAngles[0]);

        }

        public override void Draw(IMapRenderer renderer, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Size = renderer.Scale switch
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

            Color drawColor = WidgetDrawingOptions<JunctionNode>.Colors[Junction.State == SwitchState.MainRoute ? ColorVariation.Complement : ColorVariation.None];
            renderer.DrawTexture(BasicTextureType.PathNormal, renderer.WorldToScreenCoordinates(in Location), trackSectionAngles[(int)Junction.State], renderer.WorldToScreenSize(Size * scaleFactor), drawColor);
        }

    }
}
