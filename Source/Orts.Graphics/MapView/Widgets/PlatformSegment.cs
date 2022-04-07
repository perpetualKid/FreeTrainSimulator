using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Position;
using Orts.Graphics.DrawableComponents;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    internal class PlatformPath : VectorWidget
    {
        private static Dictionary<int, List<SegmentBase>> trackNodeSegments;
        private readonly List<PlatformSegment> platformPath = new List<PlatformSegment>();
        private readonly string platformName;
        private readonly string stationName;
        private readonly PointD midPoint;

        internal PlatformPath(PlatformTrackItem start, PlatformTrackItem end)
        {
            base.location = start.Location;
            tile = start.Tile;
            vectorEnd = end.Location;
            otherTile = end.Tile;
            platformName = string.IsNullOrEmpty(start.PlatformName) ? end.PlatformName : start.PlatformName;
            stationName = string.IsNullOrEmpty(start.StationName) ? end.StationName : start.StationName;
            midPoint = base.Location + (vectorEnd - base.location) / 2.0;

            ref readonly PointD startLocation = ref start.Location;
            ref readonly PointD endLocation = ref end.Location;

            SegmentBase startSegment;
            SegmentBase endSegment;
            List<SegmentBase> segments;

            // simple case, both are on the same tracknode
            if (start.TrackVectorNode.Index == end.TrackVectorNode.Index)
            {
                if (!trackNodeSegments.TryGetValue(start.TrackVectorNode.Index, out segments))
                    throw new InvalidOperationException($"Track Segments for TrackNode {start.TrackVectorNode.Index} not found");

                (startSegment, endSegment) = EvaluteSegments(startLocation, endLocation, segments);
            }
            //advanced case, most likely it's just on the junction node due to overlap
            else
            {
                //check if the this was close enough on the other tracknode, maybe just a rounding error
                if (!trackNodeSegments.TryGetValue(start.TrackVectorNode.Index, out segments))
                    throw new InvalidOperationException($"Track Segments for TrackNode {start.TrackVectorNode.Index} not found");
                (startSegment, endSegment) = EvaluteSegments(startLocation, endLocation, segments);

                if (startSegment == null || endSegment == null)
                {
                    if (!trackNodeSegments.TryGetValue(end.TrackVectorNode.Index, out segments))
                        throw new InvalidOperationException($"Track Segments for TrackNode {start.TrackVectorNode.Index} not found");

                    (startSegment, endSegment) = EvaluteSegments(startLocation, endLocation, segments);
                }
                if (startSegment == null || endSegment == null)
                    Trace.TraceWarning($"Can't connect platform ends for Platform '{start.PlatformName}' in station '{start.StationName}'.");
            }

            startSegment = CheckForSegmentOverrun(startSegment, startLocation, segments);
            endSegment = CheckForSegmentOverrun(endSegment, endLocation, segments);
            //find all vector sections in between (understanding which direction to go)
            //build a path between the two
            if (startSegment.TrackVectorSectionIndex < endSegment.TrackVectorSectionIndex)
            {
                //start section
                bool reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location) < startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location);
                platformPath.Add(new PlatformSegment(startSegment, startLocation, reverse ?  startSegment.Location : startSegment.Vector));
                //interim sections
                for (int i = startSegment.TrackVectorSectionIndex + 1; i <= endSegment.TrackVectorSectionIndex - 1; i++)
                {
                    platformPath.Add(new PlatformSegment(segments[i]));
                }
                //end section
                reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location) > endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location);
                platformPath.Add(new PlatformSegment(endSegment, reverse ? endSegment.Vector : endSegment.Location, endLocation));
            }
            else if (startSegment.TrackVectorSectionIndex > endSegment.TrackVectorSectionIndex)
            {
                //end section
                bool reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex + 1].Location) < endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex + 1].Location);
                platformPath.Add(new PlatformSegment(endSegment, endLocation, reverse ? endSegment.Location : endSegment.Vector));
                //interim sections
                for (int i = endSegment.TrackVectorSectionIndex + 1; i <= startSegment.TrackVectorSectionIndex - 1; i++)
                {
                    platformPath.Add(new PlatformSegment(segments[i]));
                }
                //start section
                reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex - 1].Location) > startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex - 1].Location);
                platformPath.Add(new PlatformSegment(startSegment, reverse ? startSegment.Vector : startSegment.Location, startLocation));
            }
            //on a single track vector section
            else
            {
                platformPath.Add(new PlatformSegment(startSegment, startLocation, endLocation));
            }
        }

        public static List<PlatformPath> CreatePlatforms(IEnumerable<PlatformTrackItem> platformItems, Dictionary<int, List<SegmentBase>> trackNodeSegments)
        {
            PlatformPath.trackNodeSegments = trackNodeSegments;
            List<PlatformPath> platforms = new List<PlatformPath>();

            Dictionary<int, PlatformTrackItem> platformItemMappings = platformItems.ToDictionary(p => p.Id);
            while (platformItemMappings.Count > 0)
            {
                int sourceId = platformItemMappings.Keys.First();
                PlatformTrackItem start = platformItemMappings[sourceId];
                platformItemMappings.Remove(sourceId);
                if (platformItemMappings.TryGetValue(start.LinkedId, out PlatformTrackItem end))
                {
                    if (end.LinkedId != start.Id)
                    {
                        Trace.TraceWarning($"Platform Item Pair has inconsistent linking from Source Id {start.Id} to target {start.LinkedId} vs Target id {end.Id} to source {end.LinkedId}.");
                    }
                    platformItemMappings.Remove(end.Id);
                    platforms.Add(new PlatformPath(start, end));
                }
                else
                {
                    Trace.TraceWarning($"Linked Platform Item {start.LinkedId} for Platform Item {start.Id} not found.");
                }
            }
            PlatformPath.trackNodeSegments = null;
            return platforms;

        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            foreach (PlatformSegment segment in platformPath)
            {
                segment.Draw(contentArea, colorVariation, scaleFactor);
            }

            Color fontColor = GetColor<PlatformPath>(colorVariation);
            TextShape.DrawString(contentArea.WorldToScreenCoordinates(in midPoint), fontColor, platformName, contentArea.CurrentFont, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Top, SpriteEffects.None, contentArea.SpriteBatch);
            TextShape.DrawString(contentArea.WorldToScreenCoordinates(in midPoint), fontColor, stationName, contentArea.CurrentFont, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Bottom, SpriteEffects.None, contentArea.SpriteBatch);
        }

        public override double DistanceSquared(in PointD point)
        {
            return double.NaN;
        }

        private static SegmentBase CheckForSegmentOverrun(SegmentBase expectedSegment, in PointD targetLocation, List<SegmentBase> trackNodeSegments)
        {
            // seems the platform marker is placed beyond the tracknode
            // so we need to figure which end is closer to the unset marker and run the platform until there
            if (expectedSegment == null)
            {
                expectedSegment = trackNodeSegments[0];
                double distanceFromStart = Math.Min(trackNodeSegments[0].Location.DistanceSquared(targetLocation), trackNodeSegments[0].Vector.DistanceSquared(targetLocation));
                if (trackNodeSegments[^1].Location.DistanceSquared(targetLocation) < distanceFromStart || trackNodeSegments[^1].Vector.DistanceSquared(targetLocation) < distanceFromStart)
                    expectedSegment = trackNodeSegments[^1];
            }
            return expectedSegment;
        }

        private static (SegmentBase startSegment, SegmentBase endSegment) EvaluteSegments(in PointD startLocation, in PointD endLocation, List<SegmentBase> segments)
        {
            SegmentBase startSegment = null;
            SegmentBase endSegment = null;
            foreach (SegmentBase segment in segments)
            {
                //find the start vector section
                if (segment.DistanceSquared(startLocation) < 1)
                {
                    startSegment = segment;
                    if (null != endSegment)
                        break;
                }
                //find the end vector section
                if (segment.DistanceSquared(endLocation) < 1)
                {
                    endSegment = segment;
                    if (null != startSegment)
                        break;
                }
            }
            return (startSegment, endSegment);
        }
    }

    internal class PlatformSegment : SegmentBase
    {
        public PlatformSegment(SegmentBase source) : base(source)
        {
            Size = 3;
        }

        public PlatformSegment(SegmentBase source, in PointD start, in PointD end) : base(source, start, end)
        {
            Size = 3;
        }

        public PlatformSegment(in PointD start, in PointD end) : base(start, end)
        {
            Size = 3;
        }


        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<PlatformSegment>(colorVariation);
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }

        public override NameValueCollection DebugInfo => null;
    }
}
