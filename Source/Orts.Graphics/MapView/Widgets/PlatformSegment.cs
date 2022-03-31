using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    internal class PlatformPath : VectorWidget
    {
        private static Dictionary<int, List<TrackSegment>> trackNodeSegments;

        private readonly List<PlatformSegment> platformPath = new List<PlatformSegment>();

        internal PlatformPath(PlatformTrackItem start, PlatformTrackItem end)
        {
            base.location = start.Location;
            tile = start.Tile;
            vectorEnd = end.Location;
            otherTile = end.Tile;

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
                    double startDistance = segment.DistanceSquared(startLocation);
                    if (startDistance < 0.1)
                    {
                        startSegment = segment;
                        if (null != endSegment)
                            break;
                    }
                    //find the end vector section
                    double endDistance = segment.DistanceSquared(endLocation);
                    if (endDistance < 0.1)
                    {
                        endSegment = segment;
                        if (null != startSegment)
                            break;
                    }

                }
                Debug.Assert(startSegment != null && endSegment != null);
                //find all vector sections in between (understanding which direction to go)
                //build a path between the two
                if (startSegment.TrackVectorSectionIndex < endSegment.TrackVectorSectionIndex)
                {
                    bool reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location) < startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location);
                    platformPath.Add(new PlatformSegment(startSegment, startLocation, reverse ? startSegment.Location : startSegment.Vector));
                    for (int i = startSegment.TrackVectorSectionIndex + 1; i <= endSegment.TrackVectorSectionIndex - 1; i++)
                    {
                        platformPath.Add(new PlatformSegment(segments[i]));
                    }
                    reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location) > endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location);
                    platformPath.Add(new PlatformSegment(endSegment, reverse ? endSegment.Vector: endSegment.Location, endLocation));
                }
                else if (startSegment.TrackVectorSectionIndex > endSegment.TrackVectorSectionIndex)
                {
                    for (int i = endSegment.TrackVectorSectionIndex; i <= endSegment.TrackVectorSectionIndex; i++)
                    {
                        //platformPath.Add(new PlatformSegment(segments[i]));
                    }
                }
                else
                    platformPath.Add(new PlatformSegment(startSegment, startLocation, endLocation));
            }
            //advanced case, need to check junctions along the track
            else
            {
                throw new NotImplementedException();
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
                //if (Angle >= 0)
                //{
                //    PointD deltaStart = location - centerPoint;
                //    float deltaAngle = MathHelper.WrapAngle((float)Math.Atan2(deltaStart.X, deltaStart.Y) - MathHelper.PiOver2);
                //    deltaAngle = centerToStartDirection - deltaAngle;
                //    Direction -= deltaAngle;
                //    Angle += MathHelper.ToDegrees(deltaAngle);
                //    PointD deltaEnd = vectorEnd - centerPoint;
                //    deltaAngle = MathHelper.WrapAngle((float)Math.Atan2(deltaEnd.X, deltaEnd.Y) - MathHelper.PiOver2);
                //    deltaAngle = deltaAngle - centerToEndDirection;
                //    Angle += MathHelper.ToDegrees(deltaAngle);
                //}
                //else
                //{
                //    PointD deltaStart = location - centerPoint;
                //    float deltaAngle = MathHelper.WrapAngle((float)Math.Atan2(deltaStart.X, deltaStart.Y) - MathHelper.PiOver2);
                //    deltaAngle = centerToStartDirection - deltaAngle;
                //    Direction -= deltaAngle;
                //    Angle += MathHelper.ToDegrees(deltaAngle);

                //    PointD deltaEnd = vectorEnd - centerPoint;
                //    deltaAngle = MathHelper.WrapAngle((float)Math.Atan2(deltaEnd.X, deltaEnd.Y) - MathHelper.PiOver2);
                //    deltaAngle = deltaAngle - centerToEndDirection;
                //    Angle += MathHelper.ToDegrees(deltaAngle);

                //}

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
