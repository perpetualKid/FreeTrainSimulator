using System;
using System.Collections.Generic;
using System.Diagnostics;

using Orts.Common.Position;

namespace Orts.Graphics.MapView.Widgets
{
    /// <summary>
    /// base class for Paths which are formed as set (list) of multiple <see cref="SegmentBase"></see> segments
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal abstract class SegmentPathBase<T>: VectorWidget where T : SegmentBase
    {
        private protected readonly List<T> pathSegments = new List<T>();
        internal protected readonly PointD MidPoint;

#pragma warning disable CA2214 // Do not call overridable methods in constructors
        private protected SegmentPathBase(TrackItemBase start, int startTrackNodeIndex, TrackItemBase end, int endTrackNodeIndex, Dictionary<int, List<SegmentBase>> sourceElements)
        {
            location = start.Location;
            tile = start.Tile;
            vectorEnd = end.Location;
            otherTile = end.Tile;
            MidPoint = Location + (vectorEnd - location) / 2.0;

            ref readonly PointD startLocation = ref start.Location;
            ref readonly PointD endLocation = ref end.Location;

            SegmentBase startSegment;
            SegmentBase endSegment;
            List<SegmentBase> segments;

            // simple case, both are on the same tracknode
            if (startTrackNodeIndex == endTrackNodeIndex)
            {
                if (!sourceElements.TryGetValue(startTrackNodeIndex, out segments))
                    throw new InvalidOperationException($"Track Segments for TrackNode {startTrackNodeIndex} not found");

                (startSegment, endSegment) = EvaluteSegments(startLocation, endLocation, segments);
            }
            //advanced case, most likely it's just on the junction node due to overlap
            else
            {
                //check if the this was close enough on the other tracknode, maybe just a rounding error
                if (!sourceElements.TryGetValue(startTrackNodeIndex, out segments))
                    throw new InvalidOperationException($"Track Segments for TrackNode {startTrackNodeIndex} not found");
                (startSegment, endSegment) = EvaluteSegments(startLocation, endLocation, segments);

                if (startSegment == null || endSegment == null)
                {
                    if (!sourceElements.TryGetValue(endTrackNodeIndex, out segments))
                        throw new InvalidOperationException($"Track Segments for TrackNode {startTrackNodeIndex} not found");

                    (startSegment, endSegment) = EvaluteSegments(startLocation, endLocation, segments);
                }
            }

            if (startSegment == null || endSegment == null)
            {
                Trace.TraceWarning($"Can't connect the both ends for Track Items ID {start.TrackItemId} and ID {end.TrackItemId} on Track Vector Node {startTrackNodeIndex} and {endTrackNodeIndex}.");
                pathSegments.Add(CreateItem(startLocation, endLocation));
                return;
            }

            //find all vector sections in between (understanding which direction to go)
            //build a path between the two
            if (startSegment.TrackVectorSectionIndex < endSegment.TrackVectorSectionIndex)
            {
                //start section
                bool reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location) < startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location);
                pathSegments.Add(CreateItem(startSegment, startLocation, reverse ? startSegment.Location : startSegment.Vector));
                //interim sections
                for (int i = startSegment.TrackVectorSectionIndex + 1; i <= endSegment.TrackVectorSectionIndex - 1; i++)
                {
                    pathSegments.Add(CreateItem(segments[i]));
                }
                //end section
                reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location) > endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location);
                pathSegments.Add(CreateItem(endSegment, reverse ? endSegment.Vector : endSegment.Location, endLocation));
            }
            else if (startSegment.TrackVectorSectionIndex > endSegment.TrackVectorSectionIndex)
            {
                //end section
                bool reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex + 1].Location) < endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex + 1].Location);
                pathSegments.Add(CreateItem(endSegment, endLocation, reverse ? endSegment.Location : endSegment.Vector));
                //interim sections
                for (int i = endSegment.TrackVectorSectionIndex + 1; i <= startSegment.TrackVectorSectionIndex - 1; i++)
                {
                    pathSegments.Add(CreateItem(segments[i]));
                }
                //start section
                reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex - 1].Location) > startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex - 1].Location);
                pathSegments.Add(CreateItem(startSegment, reverse ? startSegment.Vector : startSegment.Location, startLocation));
            }
            //on a single track vector section
            else
            {
                pathSegments.Add(CreateItem(startSegment, startLocation, endLocation));
            }
        }
#pragma warning restore CA2214 // Do not call overridable methods in constructors

        public override double DistanceSquared(in PointD point)
        {
            return double.NaN;
        }

        protected abstract T CreateItem(in PointD start, in PointD end);

        protected abstract T CreateItem(SegmentBase source);

        protected abstract T CreateItem(SegmentBase source, in PointD start, in PointD end);

        private static (SegmentBase startSegment, SegmentBase endSegment) EvaluteSegments(in PointD startLocation, in PointD endLocation, List<SegmentBase> segments)
        {
            SegmentBase startSegment = null;
            SegmentBase endSegment = null;
            foreach (SegmentBase segment in segments)
            {
                //find the start vector section
                if (segment.DistanceSquared(startLocation) < proximityTolerance)
                {
                    startSegment = segment;
                    if (null != endSegment)
                        break;
                }
                //find the end vector section
                if (segment.DistanceSquared(endLocation) < proximityTolerance)
                {
                    endSegment = segment;
                    if (null != startSegment)
                        break;
                }
            }
            return (startSegment, endSegment);
        }
    }
}
