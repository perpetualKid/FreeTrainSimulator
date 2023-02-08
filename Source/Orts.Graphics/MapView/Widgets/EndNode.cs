﻿using System.Collections.Generic;
using System.Globalization;

using Microsoft.Xna.Framework;

using Orts.Common.DebugInfo;
using Orts.Common.Position;
using Orts.Formats.Msts.Models;
using Orts.Models.Track;

namespace Orts.Graphics.MapView.Widgets
{
    internal class EndNode: EndNodeBase, IDrawable<PointPrimitive>, INameValueInformationProvider
    {
        private protected static InformationDictionary debugInformation = new InformationDictionary() { ["Node Type"] = "End Node" };

        private const int width = 3;
        protected const int Length = 2;

        public EndNode(TrackEndNode trackEndNode, TrackVectorNode connectedVectorNode, TrackSections trackSections): 
            base(trackEndNode, connectedVectorNode, trackSections)
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
            Color drawColor = this.GetColor<EndNode>(colorVariation);
            contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length * scaleFactor), Direction, contentArea.SpriteBatch);
        }
    }

    internal class RoadEndSegment : EndNode
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

        public RoadEndSegment(TrackEndNode trackEndNode, TrackVectorNode connectedVectorNode, TrackSections sections) : 
            base(trackEndNode, connectedVectorNode, sections)
        {
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<RoadEndSegment>(colorVariation);
            contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }
}
