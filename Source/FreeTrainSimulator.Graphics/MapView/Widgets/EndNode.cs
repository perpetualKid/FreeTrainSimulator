using System.Collections.Generic;
using System.Globalization;

using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Imported.Runtime;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal record EndNode : EndNodeBase, IDrawable<PointPrimitive>, INameValueInformationProvider
    {
        private protected static InformationDictionary debugInformation = new InformationDictionary() { ["Node Type"] = "End Node" };

        private const int width = 3;
        protected const float Length = 0.5f;

        public EndNode(Models.Track.EndNode trackEndNode, TrackDatabase trackDatabase = null) :
            base(trackEndNode, trackDatabase ?? Orts.Formats.Msts.RuntimeData.Instance.TrackModel.TrackDatabase)
        {
            Size = width;
        }

        public virtual InformationDictionary DetailInfo
        {
            get
            {
                debugInformation["Segment Type"] = "Rail Track";
                debugInformation["Node Index"] = TrackNodeIndex.ToString(CultureInfo.InvariantCulture);
                return debugInformation;
            }
        }

        public Dictionary<string, FormatOption> FormattingOptions => null;


        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = WidgetDrawingOptions<EndNode>.Colors[colorVariation];
            contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length * scaleFactor), Direction, contentArea.SpriteBatch);
        }
    }

    internal record RoadEndSegment : EndNode
    {
        public override InformationDictionary DetailInfo
        {
            get
            {
                InformationDictionary result = base.DetailInfo;
                result["Segment Type"] = "Road";
                return result;
            }
        }

        public RoadEndSegment(Models.Track.EndNode trackEndNode) :
            base(trackEndNode, Orts.Formats.Msts.RuntimeData.Instance.TrackModel.RoadDatabase)
        {
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = WidgetDrawingOptions<RoadEndSegment>.Colors[colorVariation];
            contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }
}
