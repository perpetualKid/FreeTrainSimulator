using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace Orts.Models.Track
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
                    this.trackNodes = source;
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

        public RuntimeData RuntimeData { get; }
        public IReadOnlyList<JunctionNodeBase> Junctions { get; }
        public IReadOnlyList<EndNodeBase> EndNodes { get; }
        public IReadOnlyList<TrackSegmentSection> SegmentSections { get; }
        public IReadOnlyList<EndNodeBase> RoadEndNodes { get; }
        public IReadOnlyList<TrackSegmentSection> RoadSegmentSections { get; }

        public EnumArray<ITileIndexedList<ITileCoordinate<Tile>, Tile>, MapContentType> ContentByTile { get; } = new EnumArray<ITileIndexedList<ITileCoordinate<Tile>, Tile>, MapContentType>();

        private TrackModel(RuntimeData runtimeData)
        {
            RuntimeData = runtimeData;
            Junctions = new PartialTrackElementList<JunctionNodeBase>(railTrackElements);
            EndNodes = new PartialTrackElementList<EndNodeBase>(railTrackElements);
            SegmentSections = new PartialTrackElementList<TrackSegmentSection>(railTrackElements);
            RoadEndNodes = new PartialTrackElementList<EndNodeBase>(roadTrackElements);
            RoadSegmentSections = new PartialTrackElementList<TrackSegmentSection>(roadTrackElements);
        }

        public static TrackModel Instance(Game game)
        {
            return game?.Services.GetService<TrackModel>();
        }

        public static TrackModel Reset(Game game, RuntimeData runtimeData)
        {
            game?.Services.RemoveService(typeof(TrackModel));
            TrackModel instance = new TrackModel(runtimeData);
            game.Services.AddService(instance);
            return instance;
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

            (Junctions as PartialTrackElementList<JunctionNodeBase>).AddRange(junctionNodes);
            (EndNodes as PartialTrackElementList<EndNodeBase>).AddRange(endNodes);

            ContentByTile[MapContentType.Tracks] = new TileIndexedList<TrackSegmentBase, Tile>(trackSegments);
            ContentByTile[MapContentType.JunctionNodes] = new TileIndexedList<JunctionNodeBase, Tile>(Junctions);
            ContentByTile[MapContentType.EndNodes] = new TileIndexedList<EndNodeBase, Tile>(EndNodes);
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

            (RoadEndNodes as PartialTrackElementList<EndNodeBase>).AddRange(endNodes);

            ContentByTile[MapContentType.Roads] = new TileIndexedList<TrackSegmentBase, Tile>(trackSegments);
            ContentByTile[MapContentType.RoadEndNodes] = new TileIndexedList<EndNodeBase, Tile>(RoadEndNodes);
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
            (Junctions as PartialTrackElementList<JunctionNodeBase>).Clear();
            (EndNodes as PartialTrackElementList<EndNodeBase>).Clear();
            (SegmentSections as PartialTrackElementList<TrackSegmentSection>).Clear();
        }

        public IIndexedElement TrackNodeByIndex(int index, TrackElementType trackElementType = TrackElementType.RailTrack)
        {
            return trackElementType switch
            {
                TrackElementType.RailTrack => index > -1 && index < railTrackElements.Count ? railTrackElements[index] : null,
                TrackElementType.RoadTrack => index > -1 && index < roadTrackElements.Count ? roadTrackElements[index] : null,
                _ => throw new InvalidOperationException(),
            };
        }

        public IIndexedElement TrackItemByIndex(int index)
        {
            return index > -1 && index < railTrackItems.Count ? railTrackItems[index] : null;
        }

        public IEnumerable<TrackSegmentBase> SegmentsAt(PointD location)
        {
            TrackSegmentBase segment;

            int tileRadius = 0;
            // if closer to a Tile boundary we may want to check neighbour tiles as well
            // just increase the tile radius around (9 tiles covered). If more optimization needed,
            // could rather figure which side of a tile and increase that size only
            if (Math.Abs(location.X) % Tile.TileSizeOver2 > 1000 || Math.Abs(location.Y) % Tile.TileSizeOver2 > 1000)
                tileRadius = 1;

            // get a first track segment at the location (within proximity)
            if ((segment = SegmentAt(location, tileRadius)) != null)
            {
                yield return segment;
                // now we have a segment, so only need to check if we are at the start or end with a junction
                // and return the segments from other connected nodes
                foreach (TrackSegmentBase item in OtherSegmentsAt(location, segment))
                    yield return item;
            }
        }

        public IEnumerable<TrackSegmentBase> OtherSegmentsAt(PointD location, TrackSegmentBase source)
        {
            ArgumentNullException.ThrowIfNull(source);

            TrackVectorNode vectorNode = RuntimeData.TrackDB.TrackNodes[source.TrackNodeIndex] as TrackVectorNode;
            foreach (TrackPin pinLink in vectorNode.TrackPins)
            {
                if (RuntimeData.TrackDB.TrackNodes[pinLink.Link] is TrackJunctionNode junctionNode &&
                    Junctions[junctionNode.Index].JunctionNodeAt(location))
                {
                    foreach (TrackPin pin in junctionNode.TrackPins)
                    {
                        if (pin.Link == vectorNode.Index)
                            continue;
                        yield return SegmentSections[pin.Link].SectionSegments[pin.Direction == Common.TrackDirection.Reverse ? 0 : ^1];
                    }
                }
            }
        }

        /// <summary>
        /// returns the <see cref="TrackSegmentBase" track segment at this location (within Proximity tolerance)
        /// If no segment in this place, returns null.
        /// </summary>
        public TrackSegmentBase SegmentAt(in PointD location, int tileRadius = 0, bool limit = false)
        {
            Tile tile = PointD.ToTile(location);
            double distanceSquared = double.PositiveInfinity;
            TrackSegmentBase result = null;
            foreach (TrackSegmentBase section in ContentByTile[MapContentType.Tracks].BoundingBox(tile, tileRadius))
            {
                double current;
                if ((current = section.DistanceSquared(location)) < distanceSquared)
                {
                    distanceSquared = current;
                    result = section;
                }
            }
            if (result != null && result.TrackSegmentAt(location))
            {
                return result;
            }
            if (!limit)
            {
                foreach (TrackSegmentBase section in ContentByTile[MapContentType.Tracks])
                {
                    double current;
                    if ((current = section.DistanceSquared(location)) < distanceSquared)
                    {
                        distanceSquared = current;
                        result = section;
                    }
                }
            }
            return result != null && result.TrackSegmentAt(location) ? result : null;
        }

        /// <summary>
        /// returns the <see cref="TrackSegmentBase" track segment at this location (within Proximity tolerance) from given track node.
        /// If no segment in this place, returns null.
        /// </summary>
        public TrackSegmentBase SegmentAt(int trackNode, in PointD location)
        {
            foreach (TrackSegmentBase section in SegmentSections[trackNode].SectionSegments)
            {
                if (section.TrackSegmentAt(location))
                    return section;
            }
            return null;
        }

        /// <summary>
        /// returns the <see cref="JunctionNodeBase" junction at this location (within Proximity tolerance)
        /// If no segment in this place, returns null.
        /// </summary>
        public JunctionNodeBase JunctionAt(in PointD location, int tileRadius = 0)
        {
            Tile tile = PointD.ToTile(location);
            double distanceSquared = double.PositiveInfinity;
            JunctionNodeBase result = null;
            foreach (JunctionNodeBase junctionNode in ContentByTile[MapContentType.JunctionNodes].BoundingBox(tile, tileRadius))
            {
                double current;
                if ((current = junctionNode.DistanceSquared(location)) < distanceSquared)
                {
                    distanceSquared = current;
                    result = junctionNode;
                }
            }
            return result != null && result.JunctionNodeAt(location) ? result : null;
        }

        /// <summary>
        /// returns the <see cref="EndNodeBase" end node at this location (within Proximity tolerance)
        /// If no segment in this place, returns null.
        /// </summary>
        public EndNodeBase EndNodeAt(in PointD location, int tileRadius = 0)
        {
            Tile tile = PointD.ToTile(location);
            double distanceSquared = double.PositiveInfinity;
            EndNodeBase result = null;
            foreach (EndNodeBase endNode in ContentByTile[MapContentType.EndNodes].BoundingBox(tile, tileRadius))
            {
                double current;
                if ((current = endNode.DistanceSquared(location)) < distanceSquared)
                {
                    distanceSquared = current;
                    result = endNode;
                }
            }
            return result != null && result.EndNodeAt(location) ? result : null;
        }

        public TrainPathPointBase FindIntermediaryConnection(TrainPathPointBase start, TrainPathPointBase end)
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(end);

            static bool ConnectThroughSameJunction(TrainPathPointBase start, TrainPathPointBase end)
            {
                return (start.JunctionNode != null && start.JunctionNode?.TrackNodeIndex == end.JunctionNode?.TrackNodeIndex);
            }

            //for two path points, try to find if they are connected through same junction on either end of their track node
            //for that, need to test Point1.Start with both Point2.Start and Point2.End, and same for Point1.End test with Point2.Start and Point2.End
            TrackSegmentSection startNode = SegmentSections[start.ConnectedSegments[0].TrackNodeIndex];
            TrackSegmentSection endNode = SegmentSections[end.ConnectedSegments[0].TrackNodeIndex];

            TrainPathPointBase startLocation = new TrainPathPoint(this, startNode.Location);
            TrainPathPointBase endLocation = new TrainPathPoint(this, endNode.Location);

            if (ConnectThroughSameJunction(startLocation, endLocation))
                return endLocation;

            TrainPathPointBase endVector = new TrainPathPoint(this, endNode.Vector);
            if (ConnectThroughSameJunction(startLocation, endVector))
                return endVector;

            TrainPathPointBase startVector = new TrainPathPoint(this, startNode.Vector);
            if (ConnectThroughSameJunction(startVector, endLocation))
                return endLocation;

            if (ConnectThroughSameJunction(startVector, endVector))
                return endVector;

            return null;
        }
    }
}
