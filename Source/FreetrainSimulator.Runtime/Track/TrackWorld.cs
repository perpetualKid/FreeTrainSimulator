using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Runtime.Track
{
    public sealed class TrackWorld
    {
        // 1 m² — consistent with PointPrimitive.ProximityTolerance used elsewhere in the runtime
        private const double ProximityToleranceSquared = WorldLocation.ProximityTolerance;

        private ImmutableArray<TrackNodeBase> railTrackNodes = ImmutableArray<TrackNodeBase>.Empty;
        private ImmutableArray<TrackNodeBase> roadTrackNodes = ImmutableArray<TrackNodeBase>.Empty;

        public Models.Track.TrackModel TrackModel { get; }

        public EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType> ContentByTile { get; } = new EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType>();

        public Dictionary<int, int> SwitchStates { get; private set; } = new Dictionary<int, int>();

        /// <summary>
        /// Pre-computed geometric data for every <see cref="VectorSectionNode"/> in both rail and road databases,
        /// keyed by <see cref="VectorSectionNode"/> reference identity.
        /// Built once during <see cref="Initialize"/> before <see cref="TrackTraveller"/> is used.
        /// </summary>
        public FrozenDictionary<VectorSectionNode, SectionGeometry> SectionGeometry { get; private set; }
            = FrozenDictionary<VectorSectionNode, SectionGeometry>.Empty;

        private TrackWorld(Models.Track.TrackModel trackModel)
        {
            TrackModel = trackModel;
        }

        public static TrackWorld Instance => GameService<TrackWorld>.Instance;

        public static TrackWorld GameInstance(Game game) => GameService<TrackWorld>.Get(game);

        public static TrackWorld Initialize(Game game, Models.Track.TrackModel trackModel, TrackSectionModel trackSectionModel)
        {
            TrackWorld world = new TrackWorld(trackModel);
            world.Initialize(trackSectionModel);
            return GameService<TrackWorld>.Set(game, world);
        }

        /// <summary>
        /// Builds the 3D spatial index from <paramref name="trackModel"/>'s rail track, road track, and track items.
        /// <see cref="EmptyTrackItem"/> entries are excluded — they carry no valid world location.
        /// </summary>
        private void Initialize(TrackSectionModel trackSectionModel)
        {
            /// Builds the rail track 3D spatial index
            if (null != TrackModel.TrackDatabase)
            {
                railTrackNodes = TrackModel.TrackDatabase.TrackNodes;
                ContentByTile[MapContentType.Tracks] = new TileIndexedList<VectorSectionNode>(TrackModel.TrackDatabase.VectorNodes.SelectMany(v => v.VectorSections));
                ContentByTile[MapContentType.JunctionNodes] = new TileIndexedList<JunctionNode>(TrackModel.TrackDatabase.JunctionNodes);
                ContentByTile[MapContentType.EndNodes] = new TileIndexedList<EndNode>(TrackModel.TrackDatabase.EndNodes);

                SwitchStates = TrackModel.TrackDatabase.JunctionNodes.ToDictionary(j => j.NodeIndex, j => j.MainRoute);
            }
            else
            {
                railTrackNodes = ImmutableArray<TrackNodeBase>.Empty;
                ContentByTile[MapContentType.Tracks] = new TileIndexedList<VectorSectionNode>(ImmutableArray<VectorSectionNode>.Empty);
                ContentByTile[MapContentType.JunctionNodes] = new TileIndexedList<JunctionNode>(ImmutableArray<JunctionNode>.Empty);
                ContentByTile[MapContentType.EndNodes] = new TileIndexedList<EndNode>(ImmutableArray<EndNode>.Empty);
            }

            /// Builds the road track 3D spatial index
            if (null != TrackModel.RoadDatabase)
            {
                roadTrackNodes = TrackModel.RoadDatabase.TrackNodes;
                ContentByTile[MapContentType.Roads] = new TileIndexedList<VectorSectionNode>(TrackModel.RoadDatabase.VectorNodes.SelectMany(v => v.VectorSections));
                ContentByTile[MapContentType.RoadEndNodes] = new TileIndexedList<EndNode>(TrackModel.RoadDatabase.EndNodes);
            }
            else
            {
                roadTrackNodes = ImmutableArray<TrackNodeBase>.Empty;
                ContentByTile[MapContentType.Roads] = new TileIndexedList<VectorSectionNode>(ImmutableArray<VectorSectionNode>.Empty);
                ContentByTile[MapContentType.RoadEndNodes] = new TileIndexedList<EndNode>(ImmutableArray<EndNode>.Empty);
            }

            SectionGeometry = BuildSectionGeometry(trackSectionModel);
            TrackTraveller.Initialize(this);
        }

        private FrozenDictionary<VectorSectionNode, SectionGeometry> BuildSectionGeometry(TrackSectionModel trackSectionModel)
        {
            Dictionary<VectorSectionNode, SectionGeometry> map = new Dictionary<VectorSectionNode, SectionGeometry>(ReferenceEqualityComparer.Instance);
            ImmutableDictionary<int, TrackSection> trackSections = trackSectionModel.TrackSections;

            BuildFor(TrackModel.TrackDatabase);
            BuildFor(TrackModel.RoadDatabase);
            return map.ToFrozenDictionary(ReferenceEqualityComparer.Instance);

            void BuildFor(TrackDatabase trackDatabase)
            {
                if (trackDatabase == null)
                    return;
                foreach (VectorNode vectorNode in trackDatabase.VectorNodes)
                {
                    foreach ((VectorSectionNode section, int index) in vectorNode.VectorSections.IndexedSelect())
                    {
                        _ = trackSections.TryGetValue(section.NodeIndex, out TrackSection trackSection);
                        map[section] = new SectionGeometry(vectorNode, index, trackSection, section);
                    }
                }
            }
        }

        /// <summary>
        /// Computes the world position at <paramref name="sectionOffset"/> metres from the start of <paramref name="section"/>.
        /// Falls back to <see cref="TrackNodeBase.Location"/> when no geometry template is registered for the section.
        /// </summary>
        public WorldLocation ComputeSectionLocation(VectorSectionNode section, double sectionOffset)
        {
            ArgumentNullException.ThrowIfNull(section);

            if (!SectionGeometry.TryGetValue(section, out SectionGeometry sectionGeometry) || !sectionGeometry.HasGeometry)
                return section.Location;
            if (sectionGeometry.Curved)
            {
                double clampedOffset = Math.Clamp(sectionOffset, 0.0, sectionGeometry.Length);
                return WorldLocation.PointAlongArc(section.Location, section.EndLocation, sectionGeometry.ArcAngle, sectionGeometry.Radius, clampedOffset);
            }
            return WorldLocation.PointAlongDirection(section.Location, section.EndLocation, sectionOffset);
        }

        /// <summary>
        /// Computes the world position at <paramref name="sectionOffset"/> metres from the start of the section at
        /// <paramref name="sectionIndex"/> within <paramref name="node"/>.
        /// </summary>
        public WorldLocation ComputeSectionLocation(VectorNode node, int sectionIndex, double sectionOffset)
            => ComputeSectionLocation(node.VectorSections[sectionIndex], sectionOffset);

        /// <summary>
        /// Returns the pre-computed arc or straight length in metres for <paramref name="section"/>,
        /// or <c>0.0</c> when no geometry template is registered for the section.
        /// </summary>
        public double SectionLength(VectorSectionNode section)
            => SectionGeometry.TryGetValue(section, out SectionGeometry sectionGeometry) && sectionGeometry.HasGeometry
                ? sectionGeometry.Length : 0.0;

        /// <summary>
        /// Returns the pre-computed arc or straight length in metres for the section at
        /// <paramref name="sectionIndex"/> within <paramref name="node"/>,
        /// or <c>0.0</c> when no geometry template is registered.
        /// </summary>
        public double SectionLength(VectorNode node, int sectionIndex)
            => SectionLength(node?.VectorSections[sectionIndex] ?? null);

        /// <summary>
        /// Returns the <see cref="JunctionNode"/> closest to
        /// or <see langword="null"/> if none exists within the threshold.
        /// Pass a positive <paramref name="tileRadius"/> to widen the search area beyond the home tile.
        /// </summary>
        public JunctionNode JunctionAt(in WorldLocation location, int tileRadius = 0)
        {
            return NearestNodeInBucket<JunctionNode>(MapContentType.JunctionNodes, in location, tileRadius);
        }

        /// <summary>
        /// Returns the <see cref="EndNode"/> closest to <paramref name="location"/> within the proximity threshold,
        /// or <see langword="null"/> if none exists within the threshold.
        /// Pass a positive <paramref name="tileRadius"/> to widen the search area beyond the home tile.
        /// </summary>
        public EndNode EndNodeAt(in WorldLocation location, int tileRadius = 0)
        {
            return NearestNodeInBucket<EndNode>(MapContentType.EndNodes, in location, tileRadius);
        }

        /// <summary>
        /// Returns the <see cref="VectorSectionNode"/> whose start location is closest to <paramref name="location"/>
        /// within the proximity threshold, or <see langword="null"/> if none exists within the threshold.
        /// Because <see cref="VectorSectionNode"/> implements <see cref="ITileCoordinateVector"/>, both the start
        /// and end tiles are indexed, so sections spanning a tile boundary are always reachable.
        /// Pass a positive <paramref name="tileRadius"/> to widen the search when the location is far from any section start.
        /// </summary>
        public VectorSectionNode VectorSectionNodeAt(in WorldLocation location, int tileRadius = 0)
        {
            return NearestNodeInBucket<VectorSectionNode>(MapContentType.Tracks, in location, tileRadius);
        }

        /// <summary>
        /// Returns all <see cref="VectorSectionNode"/> instances reachable via a junction at <paramref name="location"/>
        /// from the track node identified by <paramref name="trackNodeIndex"/>, excluding the source node's own sections.
        /// Mirrors <see cref="TrackModel.OtherSegmentsAt"/>.
        /// </summary>
        public IEnumerable<VectorSectionNode> OtherVectorSectionNodesAt(WorldLocation location, int trackNodeIndex)
        {
            ImmutableArray<TrackNodeConnector> nodeConnectors = TrackModel.TrackDatabase.TrackNodeConnectors[trackNodeIndex].TrackNodeConnectors;

            foreach (TrackNodeConnector nodeConnector in nodeConnectors)
            {
                if (TrackModel.TrackDatabase.TrackNodes[nodeConnector.Link] is JunctionNode junctionNode &&
                    WorldLocation.GetDistanceSquared(junctionNode.Location, location) <= ProximityToleranceSquared)
                {
                    foreach (TrackNodeConnector pin in TrackModel.TrackDatabase.TrackNodeConnectors[junctionNode.NodeIndex].TrackNodeConnectors)
                    {
                        if (pin.Link == trackNodeIndex)
                            continue;
                        if (railTrackNodes[pin.Link] is not VectorNode connectedNode)
                            continue;
                        yield return connectedNode.VectorSections[pin.Direction == TrackDirection.Reverse ? 0 : ^1];
                    }
                }
            }
        }

        /// <summary>
        /// Returns the direct node-index lookup for rail or road track nodes.
        /// Returns <see langword="null"/> for out-of-range indices.
        /// </summary>
        public TrackNodeBase TrackNodeByIndex(int index, TrackDataBaseType trackDataBaseType = TrackDataBaseType.Rail)
        {
            return trackDataBaseType switch
            {
                TrackDataBaseType.Rail => index > -1 && index < railTrackNodes.Length ? railTrackNodes[index] : null,
                TrackDataBaseType.Road => index > -1 && index < roadTrackNodes.Length ? roadTrackNodes[index] : null,
                _ => throw new InvalidOperationException(),
            };
        }

        /// <summary>
        /// Returns the <see cref="JunctionNode"/> at the far end (<paramref name="end"/> = <see langword="true"/>)
        /// or near end (<paramref name="end"/> = <see langword="false"/>) of the track node identified by
        /// <paramref name="trackNodeIndex"/>, or <see langword="null"/> if that connector links to a non-junction node.
        /// </summary>
        public JunctionNode TrackNodeJunction(int trackNodeIndex, bool end)
        {
            ImmutableArray<TrackNodeConnector> nodeConnectors = TrackModel.TrackDatabase.TrackNodeConnectors[trackNodeIndex].TrackNodeConnectors;
            return railTrackNodes[end ? nodeConnectors[1].Link : nodeConnectors[0].Link] as JunctionNode;
        }

        /// <summary>
        /// Returns the <see cref="JunctionNode"/> at the end of the track node selected by <paramref name="trackDirection"/>,
        /// or <see langword="null"/> if that connector links to a non-junction node.
        /// </summary>
        public JunctionNode TrackNodeJunction(int trackNodeIndex, TrackDirection trackDirection)
        {
            ImmutableArray<TrackNodeConnector> nodeConnectors = TrackModel.TrackDatabase.TrackNodeConnectors[trackNodeIndex].TrackNodeConnectors;
            return railTrackNodes[trackDirection == TrackDirection.Reverse ? nodeConnectors[1].Link : nodeConnectors[0].Link] as JunctionNode;
        }

        /// <summary>
        /// Returns the <see cref="JunctionNode"/> at the end of the track node <paramref name="trackNodeIndex"/> that is
        /// within the proximity threshold of <paramref name="location"/>, or <see langword="null"/> if neither end qualifies.
        /// When both ends are within threshold the closer one is returned.
        /// </summary>
        public JunctionNode TrackNodeJunction(in WorldLocation location, int trackNodeIndex)
        {
            ImmutableArray<TrackNodeConnector> nodeConnectors = TrackModel.TrackDatabase.TrackNodeConnectors[trackNodeIndex].TrackNodeConnectors;
            JunctionNode startJunction = railTrackNodes[nodeConnectors[0].Link] as JunctionNode;
            JunctionNode endJunction = railTrackNodes[nodeConnectors[1].Link] as JunctionNode;
            double startDistance = startJunction != null ? WorldLocation.GetDistanceSquared(startJunction.Location, location) : double.MaxValue;
            double endDistance = endJunction != null ? WorldLocation.GetDistanceSquared(endJunction.Location, location) : double.MaxValue;
            return startDistance <= ProximityToleranceSquared && startDistance <= endDistance
                ? startJunction
                : endDistance <= ProximityToleranceSquared ? endJunction : null;
        }

        /// <summary>
        /// Returns the world location of the next node boundary beyond <paramref name="trackSectionIndex"/>
        /// within the vector node <paramref name="trackNodeIndex"/>: the start of the following section when
        /// still inside the node, otherwise the location of the connecting junction or end node.
        /// Mirrors <see cref="TrackModel.ResolveEndNodeLocation"/>.
        /// </summary>
        public ref readonly WorldLocation ResolveEndNodeLocation(int trackNodeIndex, int trackSectionIndex)
        {
            if (railTrackNodes[trackNodeIndex] is not VectorNode vectorNode)
                throw new InvalidCastException($"Track node {trackNodeIndex} is not a VectorNode");

            if (trackSectionIndex < vectorNode.VectorSections.Length - 1)
                return ref vectorNode.VectorSections[trackSectionIndex + 1].Location;

            ImmutableArray<TrackNodeConnector> nodeConnectors = TrackModel.TrackDatabase.TrackNodeConnectors[trackNodeIndex].TrackNodeConnectors;
            TrackNodeConnector nodeConnector = nodeConnectors[1];
            if (nodeConnector.Direction != TrackDirection.Reverse)
                nodeConnector = nodeConnectors[0];

            TrackNodeBase node = railTrackNodes[nodeConnector.Link];
            if (node is not EndNode and not JunctionNode)
                throw new InvalidCastException($"Track node {trackNodeIndex} is not connected to a valid end or junction node");
            return ref node.Location;
        }

        /// <summary>
        /// Returns the <see cref="TrackItemBase"/> closest to <paramref name="location"/> within the 1 m proximity threshold,
        /// or <see langword="null"/> if no candidate exists in that tile bucket within the threshold.
        /// Adjacent tile buckets are also searched when <paramref name="location"/> is within the tolerance of a tile border.
        /// </summary>
        public Models.Track.TrackItemBase TrackItemAt(in WorldLocation location, MapContentType contentType)
        {
            Models.Track.TrackItemBase nearest = null;
            double nearestDistance = ProximityToleranceSquared;

            SearchTileBucket(location.Tile, in location, contentType, ref nearest, ref nearestDistance);

            // When the query point is within ProximityTolerance of a tile border, items in the adjacent tile
            // may also be within tolerance — check those buckets too.
            // nearPosX and nearNegX are mutually exclusive (tile is 2048 m wide); same for the Z pair.
            // Corner tiles are nested inside the X branch to avoid redundant conjunction checks.
            bool nearPosZ = location.Location.Z > Tile.TileSizeOver2 - WorldLocation.ProximityTolerance;
            bool nearNegZ = location.Location.Z < -(Tile.TileSizeOver2 - WorldLocation.ProximityTolerance);

            if (location.Location.X > Tile.TileSizeOver2 - WorldLocation.ProximityTolerance)
            {
                SearchTileBucket(new Tile(location.Tile.X + 1, location.Tile.Z), in location, contentType, ref nearest, ref nearestDistance);
                if (nearPosZ)
                    SearchTileBucket(new Tile(location.Tile.X + 1, location.Tile.Z + 1), in location, contentType, ref nearest, ref nearestDistance);
                else if (nearNegZ)
                    SearchTileBucket(new Tile(location.Tile.X + 1, location.Tile.Z - 1), in location, contentType, ref nearest, ref nearestDistance);
            }
            else if (location.Location.X < -(Tile.TileSizeOver2 - WorldLocation.ProximityTolerance))
            {
                SearchTileBucket(new Tile(location.Tile.X - 1, location.Tile.Z), in location, contentType, ref nearest, ref nearestDistance);
                if (nearPosZ)
                    SearchTileBucket(new Tile(location.Tile.X - 1, location.Tile.Z + 1), in location, contentType, ref nearest, ref nearestDistance);
                else if (nearNegZ)
                    SearchTileBucket(new Tile(location.Tile.X - 1, location.Tile.Z - 1), in location, contentType, ref nearest, ref nearestDistance);
            }

            if (nearPosZ)
                SearchTileBucket(new Tile(location.Tile.X, location.Tile.Z + 1), in location, contentType, ref nearest, ref nearestDistance);
            else if (nearNegZ)
                SearchTileBucket(new Tile(location.Tile.X, location.Tile.Z - 1), in location, contentType, ref nearest, ref nearestDistance);

            return nearest;
        }

        /// <summary>
        /// Finds the nearest <typeparamref name="T"/> in the tile bucket(s) defined by <paramref name="contentType"/>
        /// within the proximity threshold. Uses <see cref="ITileIndexedList{TTileCoordinate}.BoundingBox"/> to cover
        /// tile-border cases when <paramref name="tileRadius"/> &gt; 0.
        /// </summary>
        private T NearestNodeInBucket<T>(MapContentType contentType, in WorldLocation location, int tileRadius) where T : TrackNodeBase
        {
            T nearest = null;
            double nearestDistance = ProximityToleranceSquared;
            foreach (T item in ContentByTile[contentType].BoundingBox(location.Tile, tileRadius).Cast<T>())
            {
                double distance = WorldLocation.GetDistanceSquared(item.Location, location);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = item;
                }
            }
            return nearest;
        }

        private void SearchTileBucket(in Tile tile, in WorldLocation location, MapContentType contentType, ref Models.Track.TrackItemBase nearest, ref double nearestDistance)
        {
            foreach (Models.Track.TrackItemBase item in ContentByTile[contentType][tile].Cast<Models.Track.TrackItemBase>())
            {
                double distance = WorldLocation.GetDistanceSquared(item.Location, location);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = item;
                }
            }
        }
    }
}
