using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using FreeTrainSimulator.Common.Position;

using Orts.Formats.Msts.Models;

namespace FreeTrainSimulator.Models.Imported.Track
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

#pragma warning disable CA1002 // Do not expose generic lists
        protected List<TrackSegmentSectionBase<T>> PathSections { get; } = new List<TrackSegmentSectionBase<T>>();
#pragma warning restore CA1002 // Do not expose generic lists

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
        protected TrackSegmentPathBase(TrackModel trackModel, in PointD start, int startTrackNodeIndex, in PointD end, int endTrackNodeIndex) :
            base(start, end)
        {
            ArgumentNullException.ThrowIfNull(trackModel);

            midPoint = Location + (Vector - Location) / 2.0;
            TrackVectorNode startVectorNode = trackModel.RuntimeData.TrackDB.TrackNodes[startTrackNodeIndex] as TrackVectorNode;
            TrackVectorNode endVectorNode = trackModel.RuntimeData.TrackDB.TrackNodes[endTrackNodeIndex] as TrackVectorNode;

            (int startJunction, int endJunction, int intermediaryNode)? ConnectAcrossIntermediary()
            {
                for (int i = 0; i < startVectorNode.TrackPins.Length; i++)
                {
                    for (int j = 0; j < endVectorNode.TrackPins.Length; j++)
                    {
                        TrackPin[] trackPins = trackModel.RuntimeData.TrackDB.TrackNodes[startVectorNode.TrackPins[i].Link].TrackPins.
                            Intersect(trackModel.RuntimeData.TrackDB.TrackNodes[endVectorNode.TrackPins[j].Link].TrackPins, TrackPinComparer.LinkOnlyComparer).ToArray();
                        if (trackPins.Length == 1)
                            return (startVectorNode.TrackPins[i].Link, endVectorNode.TrackPins[j].Link, trackPins[0].Link);
                    }
                }
                return null;
            }

            if (startTrackNodeIndex == endTrackNodeIndex)
            {
                PathSections.Add(InitializeSection(trackModel, startTrackNodeIndex, start, end));
            }
            else
            {
                // check the links are connected through (the same) junction node on either end
                TrackPin[] trackPins = startVectorNode.TrackPins.Intersect(endVectorNode.TrackPins, TrackPinComparer.LinkOnlyComparer).ToArray();
                if (trackPins.Length == 1)
                {
                    PointD junctionLocation = PointD.FromWorldLocation((trackModel.RuntimeData.TrackDB.TrackNodes[trackPins[0].Link] as TrackJunctionNode).UiD.Location);
                    PathSections.Add(InitializeSection(trackModel, startTrackNodeIndex, start, junctionLocation));
                    PathSections.Add(InitializeSection(trackModel, endTrackNodeIndex, junctionLocation, end));
                }
                else
                {
                    // check if the links connected through a single intermediary track node across the junction nodes on either end
                    (int startJunction, int endJunction, int intermediaryNode)? intermediary;
                    if ((intermediary = ConnectAcrossIntermediary()) != null)
                    {
                        PathSections.Add(InitializeSection(trackModel, startTrackNodeIndex, start, trackModel.Junctions[intermediary.Value.startJunction].Location));
                        PathSections.Add(InitializeSection(trackModel, intermediary.Value.intermediaryNode));
                        PathSections.Add(InitializeSection(trackModel, endTrackNodeIndex, trackModel.Junctions[intermediary.Value.endJunction].Location, end));
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
