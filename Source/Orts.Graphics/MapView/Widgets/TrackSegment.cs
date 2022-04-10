
using System;
using System.Collections.Specialized;
using System.Globalization;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Models;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    internal class TrackSegment : SegmentBase
    {
        private protected static NameValueCollection debugInformation = new NameValueCollection() { ["Node Type"] = "Vector Section" };
        private protected static int debugInfoHash;

        public override NameValueCollection DebugInfo
        {
            get
            {
                int hash = HashCode.Combine(TrackNodeIndex, TrackVectorSectionIndex);
                if (hash != debugInfoHash)
                {
                    debugInformation["Segment Type"] = "Rail Track";
                    debugInformation["Node Index"] = TrackNodeIndex.ToString(CultureInfo.InvariantCulture);
                    debugInformation["Section Index"] = TrackVectorSectionIndex.ToString(CultureInfo.InvariantCulture);
                    debugInformation["Curved"] = Curved.ToString(CultureInfo.InvariantCulture);
                    debugInformation["Length"] = $"{Length:F1}m";
                    debugInformation["Direction"] = $"{MathHelper.ToDegrees(MathHelper.WrapAngle(Direction - MathHelper.PiOver2)):F1}º";
                    debugInformation["Radius"] = Curved ? $"{Radius:F1}m" : "n/a";
                    debugInformation["Angle"] = Curved ? $"{MathHelper.ToDegrees(Angle):F1}º" : "n/a";
                    debugInfoHash = hash;
                }
                return debugInformation;
            }
        }

        public TrackSegment(TrackVectorSection trackVectorSection, TrackSections trackSections, int trackNodeIndex, int trackVectorSectionIndex): 
            base(trackVectorSection, trackSections, trackNodeIndex, trackVectorSectionIndex)
        {
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<TrackSegment>(colorVariation);
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }

    }

    internal class RoadSegment : TrackSegment
    {
        public override NameValueCollection DebugInfo
        {
            get
            {
                NameValueCollection result = base.DebugInfo;
                result["Segment Type"] = "Road";
                return result;
            }
        }

        public RoadSegment(TrackVectorSection trackVectorSection, TrackSections trackSections, int trackNodeIndex, int trackVectorSectionIndex) :
            base(trackVectorSection, trackSections, trackNodeIndex, trackVectorSectionIndex)
        {
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<RoadSegment>(colorVariation);
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }
}
