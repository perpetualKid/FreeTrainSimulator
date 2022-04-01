using System;
using System.Collections.Generic;
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
        private static Dictionary<int, List<TrackSegment>> trackNodeSegments;
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

            // simple case, both are on the same tracknode
            if (start.TrackVectorNode.Index == end.TrackVectorNode.Index)
            {
                if (!trackNodeSegments.TryGetValue(start.TrackVectorNode.Index, out List<TrackSegment> segments))
                    throw new InvalidOperationException($"Track Segments for TrackNode {start.TrackVectorNode.Index} not found");
                TrackSegment startSegment = null;
                TrackSegment endSegment = null;
                ref readonly PointD startLocation = ref start.Location;
                ref readonly PointD endLocation = ref end.Location;
                foreach (TrackSegment segment in segments)
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
                startSegment = CheckForSegmentOverrun(startSegment, startLocation, segments);
                endSegment = CheckForSegmentOverrun(endSegment, endLocation, segments);
                //find all vector sections in between (understanding which direction to go)
                //build a path between the two
                if (startSegment.TrackVectorSectionIndex < endSegment.TrackVectorSectionIndex)
                {
                    //start section
                    bool reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location) < startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location);
                    platformPath.Add(new PlatformSegment(startSegment, startLocation, reverse ? startSegment.Location : startSegment.Vector));
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
                    bool reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location) > endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location);
                    platformPath.Add(new PlatformSegment(endSegment, reverse ? endSegment.Location : endSegment.Vector, endLocation));
                    //interim sections
                    for (int i = endSegment.TrackVectorSectionIndex + 1; i <= startSegment.TrackVectorSectionIndex - 1; i++)
                    {
                        platformPath.Add(new PlatformSegment(segments[i]));
                    }
                    //start section
                    reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location) < startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location);
                    platformPath.Add(new PlatformSegment(startSegment, startLocation, reverse ? startSegment.Vector : startSegment.Location));
                }
                //on a single track vector section
                else
                {
                    platformPath.Add(new PlatformSegment(startSegment, startLocation, endLocation));
                }
            }
            //advanced case, need to check junctions along the track
            else
            {
                //                throw new NotImplementedException();
            }
        }

        public static List<PlatformPath> CreatePlatforms(Dictionary<int, PlatformTrackItem> platformItems, Dictionary<int, List<TrackSegment>> trackNodeSegments)
        {
            PlatformPath.trackNodeSegments = trackNodeSegments;
            List<PlatformPath> platforms = new List<PlatformPath>();

            while (platformItems.Count > 0)
            {
                int sourceId = platformItems.Keys.First();
                PlatformTrackItem start = platformItems[sourceId];
                platformItems.Remove(sourceId);
                if (platformItems.TryGetValue(start.LinkedId, out PlatformTrackItem end))
                {
                    if (end.LinkedId != start.Id)
                    {
                        Trace.TraceWarning($"Platform Item Pair has inconsistent linking from Source Id {start.Id} to target {start.LinkedId} vs Target id {end.Id} to source {end.LinkedId}.");
                    }
                    platformItems.Remove(end.Id);
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

            Color fontColor = GetColor<PlatformTrackItem>(colorVariation);
            TextShape.DrawString(contentArea.WorldToScreenCoordinates(in midPoint), fontColor, platformName, contentArea.CurrentFont, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Top, SpriteEffects.None, contentArea.SpriteBatch);
            TextShape.DrawString(contentArea.WorldToScreenCoordinates(in midPoint), fontColor, stationName, contentArea.CurrentFont, Vector2.One, HorizontalAlignment.Center, VerticalAlignment.Bottom, SpriteEffects.None, contentArea.SpriteBatch);
        }

        private static TrackSegment CheckForSegmentOverrun(TrackSegment segment, in PointD targetLocation, List<TrackSegment> trackNodeSegments)
        {
            // seems the platform marker is placed beyond the tracknode
            // so we need to figure which end is closer to the unset marker and run the platform until there
            if (segment == null)
            {
                segment = trackNodeSegments[0];
                double distanceFromStart = Math.Min(trackNodeSegments[0].Location.DistanceSquared(targetLocation), trackNodeSegments[0].Vector.DistanceSquared(targetLocation));
                if (trackNodeSegments[^1].Location.DistanceSquared(targetLocation) < distanceFromStart || trackNodeSegments[^1].Vector.DistanceSquared(targetLocation) < distanceFromStart)
                    segment = trackNodeSegments[^1];
            }
            return segment;
        }
    }

    internal class PlatformSegment : TrackSegment
    {
        public PlatformSegment(TrackSegment source) : base(source)
        {
            Size = 3;
        }

        public PlatformSegment(TrackSegment source, in PointD start, in PointD end) : base(source)
        {
            bool reverse = false;

            //figure which end is closer to start vs end
            if (start.DistanceSquared(location) > start.DistanceSquared(vectorEnd) && end.DistanceSquared(location) < end.DistanceSquared(vectorEnd))
                reverse = true;

            if (reverse)
            {
                location = end;
                vectorEnd = start;
            }
            else
            {
                location = start;
                vectorEnd = end;
            }

            Size = 3;
            if (Curved)
            {
                PointD deltaStart = location - centerPoint;
                float deltaAngle = (float)Math.Atan2(deltaStart.X, deltaStart.Y) - MathHelper.PiOver2;
                deltaAngle = MathHelper.WrapAngle(centerToStartDirection - deltaAngle);
                Direction -= deltaAngle;
                Angle += MathHelper.ToDegrees(deltaAngle);
                PointD deltaEnd = vectorEnd - centerPoint;
                deltaAngle = (float)Math.Atan2(deltaEnd.X, deltaEnd.Y) - MathHelper.PiOver2;
                deltaAngle = MathHelper.WrapAngle(deltaAngle - centerToEndDirection);
                Angle += MathHelper.ToDegrees(deltaAngle);
            }
            else
            {
                Length = (float)end.Distance(start);
            }
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<PlatformTrackItem>(colorVariation);
            drawColor.A = 160;
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, 0, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }

    }
}
