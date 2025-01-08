
using System;
using System.Collections.Generic;
using System.Globalization;

using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Imported.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Models;

namespace FreeTrainSimulator.Graphics.MapView.Widgets
{
    internal class TrackSegment : TrackSegmentBase, IDrawable<VectorPrimitive>, INameValueInformationProvider
    {
        private protected static InformationDictionary debugInformation = new InformationDictionary() { ["Node Type"] = "Vector Section" };
        private protected static int debugInfoHash;

        public Dictionary<string, FormatOption> FormattingOptions { get; }

        public virtual InformationDictionary DetailInfo
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
                    debugInformation["Direction"] = $"{MathHelper.ToDegrees(MathHelper.WrapAngle(Direction + MathHelper.PiOver2)):F1}º ";
                    debugInformation["Radius"] = Curved ? $"{Radius:F1}m" : "n/a";
                    debugInformation["Angle"] = Curved ? $"{MathHelper.ToDegrees(Angle):F1}º" : "n/a";
                    debugInfoHash = hash;
                }
                return debugInformation;
            }
        }

        public TrackSegment(TrackVectorSection trackVectorSection, TrackSections trackSections, int trackNodeIndex, int trackVectorSectionIndex) :
            base(trackVectorSection, trackSections, trackNodeIndex, trackVectorSectionIndex)
        {
        }

        public virtual void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<TrackSegment>(colorVariation);
            if (Curved)
                contentArea.BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }

    }

    internal class RoadSegment : TrackSegment
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

        public RoadSegment(TrackVectorSection trackVectorSection, TrackSections trackSections, int trackNodeIndex, int trackVectorSectionIndex) :
            base(trackVectorSection, trackSections, trackNodeIndex, trackVectorSectionIndex)
        {
        }

        public override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = this.GetColor<RoadSegment>(colorVariation);
            if (Curved)
                contentArea.BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                contentArea.BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }
    }
}
