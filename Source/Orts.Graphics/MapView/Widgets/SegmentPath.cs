using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Orts.Common.Position;

namespace Orts.Graphics.MapView.Widgets
{
    internal abstract class SegmentPath<T>: VectorWidget where T : SegmentBase
    {
        private protected static Dictionary<int, List<SegmentBase>> sourceElements; //temporary variable to avoid passing the same element around
        private protected readonly List<T> pathSegments = new List<T>();
        private protected readonly PointD midPoint;

        internal SegmentPath(TrackItemBase start, TrackItemBase end)
        {
            location = start.Location;
            tile = start.Tile;
            vectorEnd = end.Location;
            otherTile = end.Tile;
            midPoint = Location + (vectorEnd - location) / 2.0;

            ref readonly PointD startLocation = ref start.Location;
            ref readonly PointD endLocation = ref end.Location;

            SegmentBase startSegment;
            SegmentBase endSegment;
            List<SegmentBase> segments;

            T.Test();

            // simple case, both are on the same tracknode
            if (start.TrackVectorNode.Index == end.TrackVectorNode.Index)
            {
                if (!sourceElements.TryGetValue(start.TrackVectorNode.Index, out segments))
                    throw new InvalidOperationException($"Track Segments for TrackNode {start.TrackVectorNode.Index} not found");

                (startSegment, endSegment) = EvaluteSegments(startLocation, endLocation, segments);
            }
            //advanced case, most likely it's just on the junction node due to overlap
            else
            {
                //check if the this was close enough on the other tracknode, maybe just a rounding error
                if (!sourceElements.TryGetValue(start.TrackVectorNode.Index, out segments))
                    throw new InvalidOperationException($"Track Segments for TrackNode {start.TrackVectorNode.Index} not found");
                (startSegment, endSegment) = EvaluteSegments(startLocation, endLocation, segments);

                if (startSegment == null || endSegment == null)
                {
                    if (!sourceElements.TryGetValue(end.TrackVectorNode.Index, out segments))
                        throw new InvalidOperationException($"Track Segments for TrackNode {start.TrackVectorNode.Index} not found");

                    (startSegment, endSegment) = EvaluteSegments(startLocation, endLocation, segments);
                }
            }
            if (startSegment == null || endSegment == null)
            {
                Trace.TraceWarning($"Can't connect siding ends for Siding '{start.SidingName}'.");
                segments.Add(new SidingSegment(startLocation, endLocation));
                return;
            }

            //startSegment = CheckForSegmentOverrun(startSegment, startLocation, segments);
            //endSegment = CheckForSegmentOverrun(endSegment, endLocation, segments);
            //find all vector sections in between (understanding which direction to go)
            //build a path between the two
            if (startSegment.TrackVectorSectionIndex < endSegment.TrackVectorSectionIndex)
            {
                //start section
                bool reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location) < startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location);
                segments.Add(new SidingSegment(startSegment, startLocation, reverse ? startSegment.Location : startSegment.Vector));
                //interim sections
                for (int i = startSegment.TrackVectorSectionIndex + 1; i <= endSegment.TrackVectorSectionIndex - 1; i++)
                {
                    segments.Add(new SidingSegment(segments[i]));
                    new T(segments[i]);
                }
                //end section
                reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location) > endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location);
                segments.Add(new SidingSegment(endSegment, reverse ? endSegment.Vector : endSegment.Location, endLocation));
            }
            else if (startSegment.TrackVectorSectionIndex > endSegment.TrackVectorSectionIndex)
            {
                //end section
                bool reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex + 1].Location) < endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex + 1].Location);
                segments.Add(new SidingSegment(endSegment, endLocation, reverse ? endSegment.Location : endSegment.Vector));
                //interim sections
                for (int i = endSegment.TrackVectorSectionIndex + 1; i <= startSegment.TrackVectorSectionIndex - 1; i++)
                {
                    sidingPath.Add(new SidingSegment(segments[i]));
                }
                //start section
                reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex - 1].Location) > startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex - 1].Location);
                segments.Add(new SidingSegment(startSegment, reverse ? startSegment.Vector : startSegment.Location, startLocation));
            }
            //on a single track vector section
            else
            {
                segments.Add(new SidingSegment(startSegment, startLocation, endLocation));
            }
        }
    }
}
