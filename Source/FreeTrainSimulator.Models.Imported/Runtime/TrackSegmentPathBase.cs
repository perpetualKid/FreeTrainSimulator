using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Models.Imported.Runtime
{
    /// <summary>
    /// A collection of one or more <see cref="TrackSegmentSectionBase{T}"/> forming a train's path.
    /// Unlike a single TrackSegmentSection, a TrackSegmentPath can have path points such as reversals, where a train will pass sections of a track multiple times.
    /// Also at junctions, train could take alternatve paths, following along an alternate TrackSegmentSection
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract record TrackSegmentPathBase<T> : VectorPrimitive where T : TrackSegmentBase
    {
        private PointD midPoint;
        private PointD topLeft;
        private PointD bottomRight;

        protected ImmutableArray<TrackSegmentSectionBase<T>> PathSections { get; private set; } = ImmutableArray<TrackSegmentSectionBase<T>>.Empty;

        public ref readonly PointD TopLeftBound => ref topLeft;
        public ref readonly PointD BottomRightBound => ref bottomRight;
        public ref readonly PointD MidPoint => ref midPoint;

        /// <summary>
        /// Straigth length or Segment length of the arc on a curved track section
        /// </summary>
        public float Length { get; private protected set; }

        protected TrackSegmentPathBase(in PointD start, in PointD end)
            : base(start, end)
        {
            midPoint = Location + (Vector - Location) / 2.0;
            Length = (float)Vector.Distance(Location);
        }

#pragma warning disable CA2214 // Do not call overridable methods in constructors
        protected TrackSegmentPathBase(TrackModel trackModel, in PointD start, int startTrackNodeIndex, in PointD end, int endTrackNodeIndex, TrackDatabase trackDatabase) :
            base(start, end)
        {
            ArgumentNullException.ThrowIfNull(trackDatabase);

            midPoint = Location + (Vector - Location) / 2.0;
            ImmutableArray<TrackNodeConnector> startNodeConnectors = trackDatabase.TrackNodeConnectors[startTrackNodeIndex];
            ImmutableArray<TrackNodeConnector> endNodeConnectors = trackDatabase.TrackNodeConnectors[endTrackNodeIndex];

            (int startJunction, int endJunction, int intermediaryNode)? ConnectAcrossIntermediary()
            {
                foreach(TrackNodeConnector startConnector in startNodeConnectors)
                {
                    foreach(TrackNodeConnector endConnector in endNodeConnectors)
                    {
                        IEnumerable<TrackNodeConnector> connections = trackDatabase.TrackNodeConnectors[startConnector.Link].
                            Intersect(trackDatabase.TrackNodeConnectors[endConnector.Link], TrackNodeConnectorComparer.LinkOnlyComparer);
                        if (connections.Count() == 1)
                            return (startConnector.Link, endConnector.Link, connections.First().Link);
                    }
                }
                return null;
            }

            if (startTrackNodeIndex == endTrackNodeIndex)
            {
                PathSections = PathSections.Add(InitializeSection(trackModel, startTrackNodeIndex, start, end));
            }
            else
            {
                // check the links are connected through (the same) junction node on either end
                IEnumerable<TrackNodeConnector> trackPins = startNodeConnectors.Intersect(endNodeConnectors, TrackNodeConnectorComparer.LinkOnlyComparer);
                if (trackPins.Count() == 1)
                {
                    PointD junctionLocation = PointD.FromWorldLocation((trackDatabase.TrackNodes[trackPins.First().Link] as JunctionNode).Location);
                    PathSections = PathSections.Add(InitializeSection(trackModel, startTrackNodeIndex, start, junctionLocation));
                    PathSections = PathSections.Add(InitializeSection(trackModel, endTrackNodeIndex, junctionLocation, end));
                }
                else
                {
                    // check if the links connected through a single intermediary track node across the junction nodes on either end
                    (int startJunction, int endJunction, int intermediaryNode)? intermediary;
                    if ((intermediary = ConnectAcrossIntermediary()) != null)
                    {
                        PathSections = PathSections.Add(InitializeSection(trackModel, startTrackNodeIndex, start, trackModel.Junctions[intermediary.Value.startJunction].Location));
                        PathSections = PathSections.Add(InitializeSection(trackModel, intermediary.Value.intermediaryNode));
                        PathSections = PathSections.Add(InitializeSection(trackModel, endTrackNodeIndex, trackModel.Junctions[intermediary.Value.endJunction].Location, end));
                    }
                    else
                    {
                        Trace.TraceWarning($"Start and End sections are not connected through the same Junction Node or at most one intermediary Track Node in between on Track Vector Node {startTrackNodeIndex} and {endTrackNodeIndex}.");
                    }
                }
            }
            foreach (TrackSegmentSectionBase<T> section in PathSections)
            {
                Length += section.Length;
            }
        }
#pragma warning restore CA2214 // Do not call overridable methods in constructors

#pragma warning disable CA1716 // Identifiers should not match keywords
        protected abstract TrackSegmentSectionBase<T> InitializeSection(in PointD start, in PointD end);
        protected abstract TrackSegmentSectionBase<T> InitializeSection(TrackModel trackModel, int trackNodeIndex, in PointD start, in PointD end);
        protected abstract TrackSegmentSectionBase<T> InitializeSection(TrackModel trackModel, int trackNodeIndex);
#pragma warning restore CA1716 // Identifiers should not match keywords

        protected void AddSections(IReadOnlyCollection<TrackSegmentSectionBase<T>> sections)
        {
            ArgumentNullException.ThrowIfNull(sections, nameof(sections));
            PathSections = PathSections.AddRange(sections);
            foreach (TrackSegmentSectionBase<T> section in sections)
            {
                Length += section.Length;
            }
        }

        protected void RemoveSections(IReadOnlyCollection<TrackSegmentSectionBase<T>> sections)
        {
            ArgumentNullException.ThrowIfNull(sections, nameof(sections));
            foreach (TrackSegmentSectionBase<T> section in sections)
            {
                Length -= section.Length;
            }
            PathSections = PathSections.RemoveRange(PathSections.Length - sections.Count, sections.Count);
        }

        public (T segment, float remainingDistance) SegmentAt(float distance)
        {
            double distanceCovered = 0;
            foreach (TrackSegmentSectionBase<T> section in PathSections)
            {
                foreach (T segment in section.SectionSegments)
                {
                    if ((distanceCovered += segment.Length) > distance)
                    {
                        return (segment, (float)(distance - (distanceCovered - segment.Length)));
                    }
                }
            }
            return (null, float.NaN);
        }

        public float DirectionAt(float distance)
        {
            (T segment, float remainingDistance) = SegmentAt(distance);
            return segment != null ? segment.DirectionAt(remainingDistance) : float.NaN;
        }

        protected void SetBounds()
        {
            double minX = Math.Min(Location.X, Vector.X);
            double minY = Math.Min(Location.Y, Vector.Y);
            double maxX = Math.Max(Location.X, Vector.X);
            double maxY = Math.Max(Location.Y, Vector.Y);

            foreach (TrackSegmentSectionBase<T> section in PathSections)
            {
                minX = Math.Min(minX, section.TopLeftBound.X);
                minY = Math.Min(minY, section.BottomRightBound.Y);
                maxX = Math.Max(maxX, section.BottomRightBound.X);
                maxY = Math.Max(maxY, section.TopLeftBound.Y);
            }

            topLeft = new PointD(minX, maxY);
            bottomRight = new PointD(maxX, minY);
            midPoint = topLeft + (bottomRight - topLeft) / 2.0;
        }
    }
}
