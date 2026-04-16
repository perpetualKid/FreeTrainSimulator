using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Runtime.Track
{
    public sealed class TrackModel
    {

        private sealed class PartialTrackElementList<T> : IReadOnlyList<T> where T : class, IIndexedElement
        {
            private readonly List<int> elements;
            private readonly List<IIndexedElement> parent;

            internal PartialTrackElementList(List<IIndexedElement> parent)
            {
                this.parent = parent;
                elements = new List<int>();
            }

            public T this[int index] { get => parent[index] as T; set => throw new NotImplementedException(); }

            public int Count => elements.Count;

            public void Add(T item)
            {
                elements.Add(item?.Index ?? throw new ArgumentNullException(nameof(item)));
            }

            public void AddRange(IEnumerable<T> items)
            {
                elements.AddRange(items.Select(item => item?.Index ?? throw new ArgumentNullException(nameof(item))));
            }

            public void Clear()
            {
                elements.Clear();
            }

            public bool Contains(T item)
            {
                return elements.Contains(item?.Index ?? throw new ArgumentNullException(nameof(item)));
            }

            public IEnumerator GetEnumerator()
            {
                return new NodeEnumerator<T>(elements, parent);
            }

            public static int IndexOf(T item) => item?.Index ?? throw new ArgumentNullException(nameof(item));

            IEnumerator<T> IEnumerable<T>.GetEnumerator()
            {
                return new NodeEnumerator<T>(elements, parent);
            }

            private class NodeEnumerator<TModelType> : IEnumerator<TModelType> where TModelType : class
            {
                private readonly List<int> elements;
                private readonly List<IIndexedElement> trackNodes;
                private int current;

                public NodeEnumerator(List<int> elements, List<IIndexedElement> source)
                {
                    this.elements = elements;
                    trackNodes = source;
                    current = -1;
                }

                public TModelType Current => trackNodes[elements[current]] as TModelType;

                object IEnumerator.Current => Current;

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    //Avoids going beyond the end of the collection.
                    return ++current < elements.Count;
                }

                public void Reset()
                {
                    current = -1;
                }
            }
        }

        private readonly List<IIndexedElement> railTrackElements = new List<IIndexedElement>();
        private readonly List<IIndexedElement> roadTrackElements = new List<IIndexedElement>();
        private readonly List<IIndexedElement> railTrackItems = new List<IIndexedElement>();

        public RuntimeDataResolver RuntimeData { get; }
        public IReadOnlyList<JunctionNodeBase> Junctions { get; }
        public IReadOnlyList<EndNodeBase> EndNodes { get; }
        public IReadOnlyList<TrackSegmentSection> SegmentSections { get; }
        public IReadOnlyList<EndNodeBase> RoadEndNodes { get; }
        public IReadOnlyList<TrackSegmentSection> RoadSegmentSections { get; }

        private TrackModel(RuntimeDataResolver runtimeData)
        {
            RuntimeData = runtimeData;
            Junctions = new PartialTrackElementList<JunctionNodeBase>(railTrackElements);
            EndNodes = new PartialTrackElementList<EndNodeBase>(railTrackElements);
            SegmentSections = new PartialTrackElementList<TrackSegmentSection>(railTrackElements);
            RoadEndNodes = new PartialTrackElementList<EndNodeBase>(roadTrackElements);
            RoadSegmentSections = new PartialTrackElementList<TrackSegmentSection>(roadTrackElements);
        }

        public static TrackModel Instance => GameService<TrackModel>.Instance;

        public static TrackModel GameInstance(Game game) => GameService<TrackModel>.Get(game);

        public static TrackModel Reset(Game game, RuntimeDataResolver runtimeData)
        {
            return GameService<TrackModel>.Set(game, new TrackModel(runtimeData));
        }

        public void InitializeRailTrack(IEnumerable<TrackSegmentBase> trackSegments, IEnumerable<JunctionNodeBase> junctionNodes, IEnumerable<EndNodeBase> endNodes)
        {
            ArgumentNullException.ThrowIfNull(trackSegments);
            ArgumentNullException.ThrowIfNull(junctionNodes);
            ArgumentNullException.ThrowIfNull(endNodes);

            IEnumerable<TrackSegmentSection> trackSegmentSections = trackSegments.GroupBy(t => t.TrackNodeIndex).Select(t => new TrackSegmentSection(t.Key, t));

            railTrackElements.AddRange(trackSegmentSections);

            (SegmentSections as PartialTrackElementList<TrackSegmentSection>).AddRange(railTrackElements.Cast<TrackSegmentSection>());

            railTrackElements.AddRange(junctionNodes);
            railTrackElements.AddRange(endNodes);
            railTrackElements.Sort((t1, t2) => t1.Index.CompareTo(t2.Index));
            railTrackElements.Insert(0, null);

            ((Junctions as PartialTrackElementList<JunctionNodeBase>)).AddRange(junctionNodes);
            ((EndNodes as PartialTrackElementList<EndNodeBase>)).AddRange(endNodes);
        }

        public void InitializeRoadTrack(IEnumerable<TrackSegmentBase> trackSegments, IEnumerable<EndNodeBase> endNodes)
        {
            ArgumentNullException.ThrowIfNull(trackSegments);
            ArgumentNullException.ThrowIfNull(endNodes);

            IEnumerable<TrackSegmentSection> trackSegmentSections = trackSegments.GroupBy(t => t.TrackNodeIndex).Select(t => new TrackSegmentSection(t.Key, t));

            roadTrackElements.AddRange(trackSegmentSections);

            (RoadSegmentSections as PartialTrackElementList<TrackSegmentSection>).AddRange(roadTrackElements.Cast<TrackSegmentSection>());

            roadTrackElements.AddRange(endNodes);
            roadTrackElements.Sort((t1, t2) => t1.Index.CompareTo(t2.Index));
            roadTrackElements.Insert(0, null);

            ((RoadEndNodes as PartialTrackElementList<EndNodeBase>)).AddRange(endNodes);
        }

        public void InitializeTrackItems(IEnumerable<TrackItemBase> trackItems)
        {
            ArgumentNullException.ThrowIfNull(trackItems);

            railTrackItems.AddRange(trackItems);
            railTrackItems.Sort((t1, t2) => t1.Index.CompareTo(t2.Index));
        }

        public void Reset()
        {
            railTrackElements.Clear();
            ((Junctions as PartialTrackElementList<JunctionNodeBase>)).Clear();
            ((EndNodes as PartialTrackElementList<EndNodeBase>)).Clear();
            (SegmentSections as PartialTrackElementList<TrackSegmentSection>).Clear();
        }

        public IIndexedElement TrackNodeByIndex(int index, TrackDataBaseType trackDataBaseType = TrackDataBaseType.Rail)
        {
            return trackDataBaseType switch
            {
                TrackDataBaseType.Rail => index > -1 && index < railTrackElements.Count ? railTrackElements[index] : null,
                TrackDataBaseType.Road => index > -1 && index < roadTrackElements.Count ? roadTrackElements[index] : null,
                _ => throw new InvalidOperationException(),
            };
        }

        public IIndexedElement TrackItemByIndex(int index)
        {
            return index > -1 && index < railTrackItems.Count ? railTrackItems[index] : null;
        }

        /// <summary>
        /// Returns all <see cref="TrackSegmentBase"/> instances at <paramref name="location"/>:
        /// the primary segment on the nearest track, plus segments reachable through connected junctions.
        /// Delegates spatial lookup to <see cref="TrackWorld.SectionsAt"/>.
        /// </summary>
        public IEnumerable<TrackSegmentBase> SegmentsAt(PointD location)
        {
            WorldLocation worldLocation = PointD.ToWorldLocation(location);
            foreach (VectorSectionNode section in RuntimeData.TrackWorld.SectionsAt(worldLocation))
            {
                if (RuntimeData.TrackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry geo))
                    yield return SegmentSections[geo.Node.NodeIndex].SectionSegments[geo.SectionIndex];
            }
        }

        /// <summary>
        /// Returns segments from other track nodes connected via junctions at <paramref name="location"/>.
        /// Delegates spatial lookup to <see cref="TrackWorld.OtherVectorSectionNodesAt"/>.
        /// </summary>
        public IEnumerable<TrackSegmentBase> OtherSegmentsAt(PointD location, TrackSegmentBase source)
        {
            ArgumentNullException.ThrowIfNull(source);

            WorldLocation worldLocation = PointD.ToWorldLocation(location);
            foreach (VectorSectionNode section in RuntimeData.TrackWorld.OtherVectorSectionNodesAt(worldLocation, source.TrackNodeIndex))
            {
                if (RuntimeData.TrackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry geo))
                    yield return SegmentSections[geo.Node.NodeIndex].SectionSegments[geo.SectionIndex];
            }
        }

        /// <summary>
        /// Returns the <see cref="TrackSegmentBase"/> at this location (within proximity tolerance),
        /// or <see langword="null"/> if no segment exists.
        /// Delegates spatial lookup to <see cref="TrackWorld.SectionAt(in WorldLocation, int)"/>.
        /// </summary>
        public TrackSegmentBase SegmentAt(in PointD location, int tileRadius = 0, bool limit = false)
        {
            // When limit is true, pass tileRadius+1 to restrict search; TrackWorld.SectionAt only does
            // full scan fallback when tileRadius==0, so passing a non-zero radius is equivalent to limit=true.
            VectorSectionNode section = RuntimeData.TrackWorld.SectionAt(PointD.ToWorldLocation(location), limit ? Math.Max(tileRadius, 1) : tileRadius);
            if (section == null)
                return null;
            if (RuntimeData.TrackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry geo))
                return SegmentSections[geo.Node.NodeIndex].SectionSegments[geo.SectionIndex];
            return null;
        }

        /// <summary>
        /// Returns the <see cref="TrackSegmentBase"/> at this location within a specific track node,
        /// or <see langword="null"/> if no segment exists.
        /// Delegates spatial lookup to <see cref="TrackWorld.SectionAt(VectorNode, in WorldLocation)"/>.
        /// </summary>
        public TrackSegmentBase SegmentAt(int trackNode, in PointD location)
        {
            if (RuntimeData.TrackWorld.TrackNodeByIndex(trackNode) is not VectorNode vectorNode)
                return null;
            VectorSectionNode section = RuntimeData.TrackWorld.SectionAt(vectorNode, PointD.ToWorldLocation(location));
            if (section == null)
                return null;
            if (RuntimeData.TrackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry geo))
                return SegmentSections[geo.Node.NodeIndex].SectionSegments[geo.SectionIndex];
            return null;
        }

        /// <summary>
        /// Returns the <see cref="JunctionNodeBase"/> at this location (within proximity tolerance),
        /// or <see langword="null"/> if no junction exists.
        /// Delegates spatial lookup to <see cref="TrackWorld.JunctionAt"/>.
        /// </summary>
        public JunctionNodeBase JunctionAt(in PointD location, int tileRadius = 0)
        {
            JunctionNode junction = RuntimeData.TrackWorld.JunctionAt(PointD.ToWorldLocation(location), tileRadius);
            return junction != null ? Junctions[junction.NodeIndex] : null;
        }

        /// <summary>
        /// Returns the <see cref="EndNodeBase"/> at this location (within proximity tolerance),
        /// or <see langword="null"/> if no end node exists.
        /// Delegates spatial lookup to <see cref="TrackWorld.EndNodeAt"/>.
        /// </summary>
        public EndNodeBase EndNodeAt(in PointD location, int tileRadius = 0)
        {
            EndNode endNode = RuntimeData.TrackWorld.EndNodeAt(PointD.ToWorldLocation(location), tileRadius);
            return endNode != null ? EndNodes[endNode.NodeIndex] : null;
        }

        public JunctionNodeBase TrackNodeJunction(int trackNodeIndex, bool end)
        {
            return Junctions[RuntimeData.TrackWorld.TrackNodeJunction(trackNodeIndex, end)?.NodeIndex ?? 0];
        }

        public JunctionNodeBase TrackNodeJunction(int trackNodeIndex, TrackDirection trackDirection)
        {
            return Junctions[RuntimeData.TrackWorld.TrackNodeJunction(trackNodeIndex, trackDirection)?.NodeIndex ?? 0];
        }

        public JunctionNodeBase TrackNodeJunction(in PointD location, int trackNodeIndex)
        {
            JunctionNode junction = RuntimeData.TrackWorld.TrackNodeJunction(PointD.ToWorldLocation(location), trackNodeIndex);
            return junction != null ? Junctions[junction.NodeIndex] : null;
        }

        /// <summary>
        /// Finds a junction node that connects <paramref name="start"/> and <paramref name="end"/> path points
        /// through their track nodes, where one is on the in-pin and the other on the out-pin.
        /// Delegates to <see cref="TrackWorld.FindIntermediaryJunction"/>.
        /// </summary>
        public TrainPathPointBase FindIntermediaryConnection(TrainPathPointBase start, TrainPathPointBase end)
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(end);

            JunctionNode junction = RuntimeData.TrackWorld.FindIntermediaryJunction(
                start.ConnectedSegments[0].TrackNodeIndex,
                end.ConnectedSegments[0].TrackNodeIndex);
            return junction != null ? new TrainPathPoint(Junctions[junction.NodeIndex].Location, this) : null;
        }

        /// <summary>
        /// Resolves the <see cref="WorldLocation"/> of the end node at the given track section.
        /// Delegates to <see cref="TrackWorld.ResolveEndNodeLocation"/>.
        /// </summary>
        public ref readonly WorldLocation ResolveEndNodeLocation(int trackNodeIndex, int trackSectionIndex)
        {
            return ref RuntimeData.TrackWorld.ResolveEndNodeLocation(trackNodeIndex, trackSectionIndex);
        }
    }
}
