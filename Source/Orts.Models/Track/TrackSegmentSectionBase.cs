using System;
using System.Collections.Generic;
using System.Diagnostics;

using Orts.Common.Position;

namespace Orts.Models.Track
{
    /// <summary>
    /// A collection of multiple <see cref="TrackSegmentBase"></see> segments along a track, covering all or 
    /// some <see cref="Formats.Msts.TrackVectorSection"/> of a <see cref="Formats.Msts.TrackNode"/>. 
    /// Examples for partial TrackSegmentSections are i.e. Platforms along a track.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class TrackSegmentSectionBase<T> : VectorPrimitive, IIndexedElement where T : TrackSegmentBase
    {
        private PointD midPoint;
        private PointD topLeft;
        private PointD bottomRight;

        private readonly List<T> sectionSegments = new List<T>();
        public IReadOnlyList<T> SectionSegments => sectionSegments;
        public ref readonly PointD TopLeftBound => ref topLeft;
        public ref readonly PointD BottomRightBound => ref bottomRight;
        public ref readonly PointD MidPoint => ref midPoint;

        /// <summary>
        /// Sum if the length of all section segments
        /// </summary>
        public float Length { get; private set; }

        public int TrackNodeIndex { get; }

#pragma warning disable CA1033 // Interface methods should be callable by child types
        int IIndexedElement.Index => TrackNodeIndex;
#pragma warning restore CA1033 // Interface methods should be callable by child types

        private protected TrackSegmentSectionBase(int trackNodeIndex, IEnumerable<T> trackSegments) : base()
        {
            sectionSegments.AddRange(trackSegments);
            sectionSegments.Sort((t1, t2) => t1.TrackVectorSectionIndex.CompareTo(t2.TrackVectorSectionIndex));

            if (SectionSegments.Count == 1)
            {
                SetVector(SectionSegments[0].Location, SectionSegments[0].Vector);
            }
            else
            {
                TrackSegmentBase startSegment = SectionSegments[0];
                TrackSegmentBase endSegment = SectionSegments[^1];

                bool reverse = startSegment.Location.DistanceSquared(SectionSegments[startSegment.TrackVectorSectionIndex + 1].Location) < startSegment.Vector.DistanceSquared(SectionSegments[startSegment.TrackVectorSectionIndex + 1].Location);
                PointD start = reverse ? startSegment.Vector : startSegment.Location;

                //end section
                reverse = endSegment.Location.DistanceSquared(SectionSegments[endSegment.TrackVectorSectionIndex - 1].Location) > endSegment.Vector.DistanceSquared(SectionSegments[endSegment.TrackVectorSectionIndex - 1].Location);
                PointD end = reverse ? endSegment.Location : endSegment.Vector;

                SetVector(start, end);
            }
            foreach (T item in SectionSegments)
            {
                Length += item.Length;
            }
            midPoint = Location + (Vector - Location) / 2.0;
            TrackNodeIndex = trackNodeIndex;
            SetBounds();
        }

#pragma warning disable CA2214 // Do not call overridable methods in constructors
        protected TrackSegmentSectionBase(in PointD start, in PointD end) : base(start, end)
        {
            sectionSegments.Add(CreateItem(start, end));
            Length = SectionSegments[^1].Length;
            SetBounds();
        }

        protected TrackSegmentSectionBase(TrackModel trackModel, int trackNodeIndex) : base()
        {
            ArgumentNullException.ThrowIfNull(trackModel);

            SetVector(trackModel.SegmentSections[trackNodeIndex].Location, trackModel.SegmentSections[trackNodeIndex].Vector);
            foreach (TrackSegmentBase segment in trackModel.SegmentSections[trackNodeIndex].SectionSegments)
            {
                sectionSegments.Add(CreateItem(segment));
                Length += SectionSegments[^1].Length;
            }
            midPoint = Location + (Vector - Location) / 2.0;
            TrackNodeIndex = trackNodeIndex;
            SetBounds();
        }

        protected TrackSegmentSectionBase(TrackModel trackModel, int trackNodeIndex, PointD start, PointD end) : base(start, end)
        {
            ArgumentNullException.ThrowIfNull(trackModel);

            midPoint = Location + (Vector - Location) / 2.0;
            TrackNodeIndex = trackNodeIndex;

            TrackSegmentBase startSegment;
            TrackSegmentBase endSegment;

            startSegment = trackModel.SegmentAt(trackNodeIndex, start);
            endSegment = trackModel.SegmentAt(trackNodeIndex, end);

            if (startSegment == null || endSegment == null)
            {
                Trace.TraceWarning($"Start or End point not on track for Track Vector Node {trackNodeIndex}. This will be shown as straight line in the map view.");
                sectionSegments.Add(CreateItem(start, end));
                SetBounds();
                return;
            }

            IReadOnlyList<TrackSegmentBase> segments;

            if ((segments = trackModel.SegmentSections[trackNodeIndex]?.SectionSegments) == null)
                throw new InvalidOperationException($"Track Segments for TrackNode {trackNodeIndex} not found");

            //find all vector sections in between (understanding which direction to go)
            //build a path between the two
            if (startSegment.TrackVectorSectionIndex < endSegment.TrackVectorSectionIndex)
            {
                //start section
                bool reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location) < startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex + 1].Location);
                sectionSegments.Add(CreateItem(startSegment, start, reverse ? startSegment.Location : startSegment.Vector));
                //interim sections
                for (int i = startSegment.TrackVectorSectionIndex + 1; i <= endSegment.TrackVectorSectionIndex - 1; i++)
                {
                    sectionSegments.Add(CreateItem(segments[i]));
                }
                //end section
                reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location) > endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex - 1].Location);
                sectionSegments.Add(CreateItem(endSegment, reverse ? endSegment.Vector : endSegment.Location, end));
            }
            else if (startSegment.TrackVectorSectionIndex > endSegment.TrackVectorSectionIndex)
            {
                //end section
                bool reverse = endSegment.Location.DistanceSquared(segments[endSegment.TrackVectorSectionIndex + 1].Location) < endSegment.Vector.DistanceSquared(segments[endSegment.TrackVectorSectionIndex + 1].Location);
                sectionSegments.Add(CreateItem(endSegment, end, reverse ? endSegment.Location : endSegment.Vector));
                //interim sections
                for (int i = endSegment.TrackVectorSectionIndex + 1; i <= startSegment.TrackVectorSectionIndex - 1; i++)
                {
                    sectionSegments.Add(CreateItem(segments[i]));
                }
                //start section
                reverse = startSegment.Location.DistanceSquared(segments[startSegment.TrackVectorSectionIndex - 1].Location) > startSegment.Vector.DistanceSquared(segments[startSegment.TrackVectorSectionIndex - 1].Location);
                sectionSegments.Add(CreateItem(startSegment, reverse ? startSegment.Vector : startSegment.Location, start));
            }
            //on a single track vector section
            else
            {
                sectionSegments.Add(CreateItem(startSegment, start, end));
            }
            foreach (T item in SectionSegments)
            {
                Length += item.Length;
            }
            SetBounds();
        }
#pragma warning restore CA2214 // Do not call overridable methods in constructors

        public override double DistanceSquared(in PointD point)
        {
            return double.NaN;
        }

#pragma warning disable CA1716 // Identifiers should not match keywords
        protected abstract T CreateItem(in PointD start, in PointD end);

        protected abstract T CreateItem(TrackSegmentBase source);

        protected abstract T CreateItem(TrackSegmentBase source, in PointD start, in PointD end);
#pragma warning restore CA1716 // Identifiers should not match keywords

        protected void SetBounds()
        {
            double minX = Math.Min(Location.X, Vector.X);
            double minY = Math.Min(Location.Y, Vector.Y);
            double maxX = Math.Max(Location.X, Vector.X);
            double maxY = Math.Max(Location.Y, Vector.Y);

            foreach (TrackSegmentBase segment in SectionSegments)
            {
                minX = Math.Min(minX, segment.Location.X);
                minY = Math.Min(minY, segment.Location.Y);
                maxX = Math.Max(maxX, segment.Location.X);
                maxY = Math.Max(maxY, segment.Location.Y);
            }

            topLeft = new PointD(minX, maxY);
            bottomRight = new PointD(maxX, minY);
            midPoint = topLeft + (bottomRight - topLeft) / 2.0;
        }
    }
}
