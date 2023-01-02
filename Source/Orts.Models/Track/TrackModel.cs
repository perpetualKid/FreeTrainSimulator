using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Orts.Common.Position;

namespace Orts.Models.Track
{
    public class TrackModel
    {
        private sealed class PartialTrackNodeList<T> : IList<T> where T : class, ITrackNode
        {
            private readonly List<int> elements;
            private readonly List<ITrackNode> parent;

            internal PartialTrackNodeList(List<ITrackNode> parent)
            {
                this.parent = parent;
                elements = new List<int>();
            }

            public T this[int index] { get => parent[index] as T; set => throw new NotImplementedException(); }

            public bool IsReadOnly => true;

            public int Count => elements.Count;

            public void Add(T item)
            {
                elements.Add(item?.TrackNodeIndex ?? throw new ArgumentNullException(nameof(item)));
            }

            public void Clear()
            {
                elements.Clear();
            }

            public bool Contains(T item)
            {
                return elements.Contains(item?.TrackNodeIndex ?? throw new ArgumentNullException(nameof(item)));
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public IEnumerator GetEnumerator()
            {
                return new NodeEnumerator<T>(elements, parent);
            }

            public int IndexOf(T item) => item?.TrackNodeIndex ?? throw new ArgumentNullException(nameof(item));

            public void Insert(int index, T item)
            {
                throw new NotImplementedException();
            }

            public bool Remove(T item)
            {
                throw new NotImplementedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            IEnumerator<T> IEnumerable<T>.GetEnumerator()
            {
                return new NodeEnumerator<T>(elements, parent);
            }

            private class NodeEnumerator<TModelType> : IEnumerator<TModelType> where TModelType : class
            {
                private readonly List<int> junctions;
                private readonly List<ITrackNode> trackNodes;
                private int current;

                public NodeEnumerator(List<int> elements, List<ITrackNode> source)
                {
                    this.junctions = elements;
                    this.trackNodes = source;
                    current = -1;
                }

                public TModelType Current => trackNodes[junctions[current]] as TModelType;

                object IEnumerator.Current => Current;

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    //Avoids going beyond the end of the collection.
                    return ++current < junctions.Count;
                }

                public void Reset()
                {
                    current = -1;
                }
            }
        }

        private readonly List<ITrackNode> elements = new List<ITrackNode>();

        public IList<JunctionNodeBase> Junctions { get; }
        public IList<EndNodeBase> EndNodes { get; }
        public IList<TrackSegmentSection> SegmentSections { get; }

        public TileIndexedList<TrackSegmentBase, Tile> TiledSegments { get; private set; }
        public TileIndexedList<JunctionNodeBase, Tile> TiledJunctionNodes { get; private set; }
        public TileIndexedList<EndNodeBase, Tile> TiledEndNodes { get; private set; }
        public TileIndexedList<TrackSegmentSectionBase<TrackSegmentBase>, Tile> TiledSegmentSections { get; private set; }

        private TrackModel()
        {
            Junctions = new PartialTrackNodeList<JunctionNodeBase>(elements);
            EndNodes = new PartialTrackNodeList<EndNodeBase>(elements);
            SegmentSections = new PartialTrackNodeList<TrackSegmentSection>(elements);
        }

        public static TrackModel Instance { get; } = new TrackModel();

        public void SetTrackSegments(IEnumerable<TrackSegmentBase> trackSegments, IEnumerable<JunctionNodeBase> junctionNodes, IEnumerable<EndNodeBase> endNodes)
        {
            ArgumentNullException.ThrowIfNull(trackSegments);
            ArgumentNullException.ThrowIfNull(junctionNodes);
            ArgumentNullException.ThrowIfNull(endNodes);

            IEnumerable<TrackSegmentSection> trackSegmentSections = trackSegments.GroupBy(t => t.TrackNodeIndex).Select(t => new TrackSegmentSection(t.Key, t));

            elements.AddRange(trackSegmentSections);
            foreach (TrackSegmentSection trackSegment in elements)
                SegmentSections.Add(trackSegment);

            elements.AddRange(junctionNodes);
            elements.AddRange(endNodes);
            elements.Sort((t1, t2) => t1.TrackNodeIndex.CompareTo(t2.TrackNodeIndex));
            elements.Insert(0, null);

            foreach (JunctionNodeBase junctionNode in junctionNodes)
                Junctions.Add(junctionNode);
            foreach (EndNodeBase endNode in endNodes)
                EndNodes.Add(endNode);

            TiledSegments = new TileIndexedList<TrackSegmentBase, Tile>(trackSegments);
            TiledSegmentSections = new TileIndexedList<TrackSegmentSectionBase<TrackSegmentBase>, Tile>(trackSegmentSections);
            TiledJunctionNodes = new TileIndexedList<JunctionNodeBase, Tile>(Junctions);
            TiledEndNodes = new TileIndexedList<EndNodeBase, Tile>(EndNodes);
        }

        public static void Reset()
        {
            Instance.elements.Clear();
            Instance.Junctions.Clear();
            Instance.EndNodes.Clear();
            Instance.SegmentSections.Clear();
        }

        public TrackSegmentBase SegmentBaseAt(in PointD location)
        {
            Tile tile = PointD.ToTile(location);
            TrackSegmentBase result;
            foreach (TrackSegmentSection section in TiledSegmentSections.BoundingBox(tile, 1))
            {
                if ((result = TrackSegmentBase.SegmentBaseAt(location, section.SectionSegments)) != null)
                    return result;
            }
            return null;
        }

        public TrackSegmentBase SegmentBaseAt(int nodeIndex, in PointD location)
        {
            TrackSegmentBase result;
            if ((result = TrackSegmentBase.SegmentBaseAt(location, SegmentSections[nodeIndex].SectionSegments)) != null)
                return result;
            return null;
        }

        public JunctionNodeBase JunctionBaseAt(in PointD location)
        {
            Tile tile = PointD.ToTile(location);
            foreach (JunctionNodeBase junctionNode in TiledJunctionNodes.BoundingBox(tile, 1))
            {
                if (junctionNode.JunctionNodeAt(location))
                    return junctionNode;
            }
            return null;
        }

        public EndNodeBase EndNodeBaseAt(in PointD location)
        {
            Tile tile = PointD.ToTile(location);
            foreach (EndNodeBase endNode in TiledEndNodes.BoundingBox(tile, 1))
            {
                if (endNode.EndNodeAt(location))
                    return endNode;
            }
            return null;
        }

    }
}
