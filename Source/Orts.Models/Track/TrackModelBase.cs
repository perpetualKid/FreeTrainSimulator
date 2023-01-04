using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Formats.Msts;

namespace Orts.Models.Track
{
    public abstract class TrackModelBase
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

        public RuntimeData RuntimeData { get; init; }
        public IList<JunctionNodeBase> Junctions { get; }
        public IList<EndNodeBase> EndNodes { get; }
        public IList<TrackSegmentSection> SegmentSections { get; }

        public TileIndexedList<TrackSegmentBase, Tile> TiledSegments { get; private set; }
        public TileIndexedList<JunctionNodeBase, Tile> TiledJunctionNodes { get; private set; }
        public TileIndexedList<EndNodeBase, Tile> TiledEndNodes { get; private set; }
        public TileIndexedList<TrackSegmentSectionBase<TrackSegmentBase>, Tile> TiledSegmentSections { get; private set; }

        protected TrackModelBase()
        {
            Junctions = new PartialTrackNodeList<JunctionNodeBase>(elements);
            EndNodes = new PartialTrackNodeList<EndNodeBase>(elements);
            SegmentSections = new PartialTrackNodeList<TrackSegmentSection>(elements);
        }

        public static T Instance<T>(Game game) where T : TrackModelBase
        {
            return game?.Services.GetService<T>();
        }

        public ITrackNode this[int index] => index > -1 && index < elements.Count ? elements[index] : null;

        public static void Initialize<T>(Game game, RuntimeData runtimeData, IEnumerable<TrackSegmentBase> trackSegments, IEnumerable<JunctionNodeBase> junctionNodes, IEnumerable<EndNodeBase> endNodes) 
            where T: TrackModelBase, new()
        {
            game?.Services.RemoveService(typeof(T));
            T instance = new T() { RuntimeData = runtimeData };
            game.Services.AddService(typeof(T), instance);

            ArgumentNullException.ThrowIfNull(trackSegments);
            ArgumentNullException.ThrowIfNull(junctionNodes);
            ArgumentNullException.ThrowIfNull(endNodes);

            IEnumerable<TrackSegmentSection> trackSegmentSections = trackSegments.GroupBy(t => t.TrackNodeIndex).Select(t => new TrackSegmentSection(t.Key, t));

            instance.elements.AddRange(trackSegmentSections);
            foreach (TrackSegmentSection trackSegment in instance.elements)
                instance.SegmentSections.Add(trackSegment);

            instance.elements.AddRange(junctionNodes);
            instance.elements.AddRange(endNodes);
            instance.elements.Sort((t1, t2) => t1.TrackNodeIndex.CompareTo(t2.TrackNodeIndex));
            instance.elements.Insert(0, null);

            foreach (JunctionNodeBase junctionNode in junctionNodes)
                instance.Junctions.Add(junctionNode);
            foreach (EndNodeBase endNode in endNodes)
                instance.EndNodes.Add(endNode);

            instance.TiledSegments = new TileIndexedList<TrackSegmentBase, Tile>(trackSegments);
            instance.TiledSegmentSections = new TileIndexedList<TrackSegmentSectionBase<TrackSegmentBase>, Tile>(trackSegmentSections);
            instance.TiledJunctionNodes = new TileIndexedList<JunctionNodeBase, Tile>(instance.Junctions);
            instance.TiledEndNodes = new TileIndexedList<EndNodeBase, Tile>(instance.EndNodes);
        }

        public void Reset()
        {
            elements.Clear();
            Junctions.Clear();
            EndNodes.Clear();
            SegmentSections.Clear();
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

    public class TrackModel : TrackModelBase
    { }

    public class RoadTrackModel : TrackModelBase
    { 
    }
}
