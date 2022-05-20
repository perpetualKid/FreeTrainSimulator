using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

using Orts.Common.Position;

namespace Orts.Models.Simplified.Track
{
    /// <summary>
    /// base class for Paths which are formed as set (list) of multiple <see cref="TrackSegmentBase"></see> segments
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class SegmentPathBase<T>: VectorPrimitive where T : TrackSegmentBase
    {
        private readonly PointD midPoint;

#pragma warning disable CA1002 // Do not expose generic lists
        protected List<T> PathSegments { get; } = new List<T>();
#pragma warning restore CA1002 // Do not expose generic lists
        public ref readonly PointD MidPoint => ref midPoint;

        private protected SegmentPathBase(PointD start, PointD end): base(start, end)
        {
            midPoint = Location + (Vector - Location) / 2.0;
        }

#pragma warning disable CA2214 // Do not call overridable methods in constructors
        protected SegmentPathBase(in PointD start, int startTrackNodeIndex, in PointD end, int endTrackNodeIndex, Dictionary<int, List<TrackSegmentBase>> sourceElements):
            base(start, end)
        {
            midPoint = Location + (Vector - Location) / 2.0;

            TrackSegmentBase startSegment;
            TrackSegmentBase endSegment;
            List<TrackSegmentBase> segments;

            // simple case, both are on the same tracknode
            if (startTrackNodeIndex == endTrackNodeIndex)
            {
                if (!sourceElements.TryGetValue(startTrackNodeIndex, out segments))
                    throw new InvalidOperationException($"Track Segments for TrackNode {startTrackNodeIndex} not found");

                (startSegment, endSegment) = EvaluteSegments(start, end, segments);
            }
            //advanced case, most likely it's just on the junction node due to overlap
            else
            {
                //check if the this was close enough on the other tracknode, maybe just a rounding error
                if (!sourceElements.TryGetValue(startTrackNodeIndex, out segments))
                    throw new InvalidOperationException($"Track Segments for TrackNode {startTrackNodeIndex} not found");
                (startSegment, endSegment) = EvaluteSegments(start, end, segments);

                if (startSegment == null || endSegment == null)
                {
                    if (!sourceElements.TryGetValue(endTrackNodeIndex, out segments))
                        throw new InvalidOperationException($"Track Segments for TrackNode {startTrackNodeIndex} not found");

                    (startSegment, endSegment) = EvaluteSegments(start, end, segments);
                }
            }

            if (startSegment == null || endSegment == null)
            {
//                Trace.TraceWarning($"Can't connect the both ends for Track Items ID {start.TrackItemId} and ID {end.TrackItemId} on Track Vector Node {startTrackNodeIndex} and {endTrackNodeIndex}.");
                PathSegments.Add(CreateItem(start, end));
                return;
            }

            //find all vector sections in between (understanding which direction to go)
            //build a path between the two
            if (startSegment.TrackVectorSectionIndex < endSegment.TrackVectorSectionIndex)
            {
                //start section
                bool reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location) < startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location);
                PathSegments.Add(CreateItem(startSegment, start, reverse ? startSegment.Location : startSegment.Vector));
                //interim sections
                for (int i = startSegment.TrackVectorSectionIndex + 1; i <= endSegment.TrackVectorSectionIndex - 1; i++)
                {
                    PathSegments.Add(CreateItem(segments[i]));
                }
                //end section
                reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location) > endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location);
                PathSegments.Add(CreateItem(endSegment, reverse ? endSegment.Vector : endSegment.Location, end));
            }
            else if (startSegment.TrackVectorSectionIndex > endSegment.TrackVectorSectionIndex)
            {
                //end section
                bool reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex + 1].Location) < endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex + 1].Location);
                PathSegments.Add(CreateItem(endSegment, end, reverse ? endSegment.Location : endSegment.Vector));
                //interim sections
                for (int i = endSegment.TrackVectorSectionIndex + 1; i <= startSegment.TrackVectorSectionIndex - 1; i++)
                {
                    PathSegments.Add(CreateItem(segments[i]));
                }
                //start section
                reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex - 1].Location) > startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex - 1].Location);
                PathSegments.Add(CreateItem(startSegment, reverse ? startSegment.Vector : startSegment.Location, start));
            }
            //on a single track vector section
            else
            {
                PathSegments.Add(CreateItem(startSegment, start, end));
            }
        }
#pragma warning restore CA2214 // Do not call overridable methods in constructors

        public override double DistanceSquared(in PointD point)
        {
            return double.NaN;
        }

        protected abstract T CreateItem(in PointD start, in PointD end);

        protected abstract T CreateItem(TrackSegmentBase source);

        protected abstract T CreateItem(TrackSegmentBase source, in PointD start, in PointD end);

        private static (TrackSegmentBase startSegment, TrackSegmentBase endSegment) EvaluteSegments(in PointD startLocation, in PointD endLocation, List<TrackSegmentBase> segments)
        {
            TrackSegmentBase startSegment = null;
            TrackSegmentBase endSegment = null;
            foreach (TrackSegmentBase segment in segments)
            {
                //find the start vector section
                if (segment.DistanceSquared(startLocation) < ProximityTolerance)
                {
                    startSegment = segment;
                    if (null != endSegment)
                        break;
                }
                //find the end vector section
                if (segment.DistanceSquared(endLocation) < ProximityTolerance)
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
