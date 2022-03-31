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
                if (startSegment.TrackVectorSectionIndex < endSegment.TrackVectorSectionIndex)
                    for (int i = startSegment.TrackVectorSectionIndex; i <= endSegment.TrackVectorSectionIndex; i++)
                    {
                        platformPath.Add(new PlatformSegment(segments[i]));
                    }
                else
                {
                    for (int i = endSegment.TrackVectorSectionIndex; i <= endSegment.TrackVectorSectionIndex; i++)
                    {
                        platformPath.Add(new PlatformSegment(segments[i]));
                    }
                }
                //build a path between the two
            }
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
            foreach(PlatformSegment segment in platformPath)
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

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Color drawColor = GetColor<PlatformTrackItem>(colorVariation);
            drawColor.A = 128;
            if (Curved)
                BasicShapes.DrawArc(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Radius), Direction, Angle, 0, contentArea.SpriteBatch);
            else
                BasicShapes.DrawLine(contentArea.WorldToScreenSize(Size * scaleFactor), drawColor, contentArea.WorldToScreenCoordinates(in Location), contentArea.WorldToScreenSize(Length), Direction, contentArea.SpriteBatch);
        }

    }
}
