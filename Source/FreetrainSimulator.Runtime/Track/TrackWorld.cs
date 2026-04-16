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

        private readonly TrackModel trackModel;

        /// <summary>
        /// Rail track database containing track nodes, items, and connectivity data.
        /// </summary>
        public TrackDatabase TrackDatabase => trackModel.TrackDatabase;

        /// <summary>
        /// Road track database containing road nodes, items, and connectivity data.
        /// </summary>
        public TrackDatabase RoadDatabase => trackModel.RoadDatabase;

        /// <summary>
        /// Indexed by track node index; returns the <see cref="JunctionNodeBase"/> for junction nodes,
        /// or <see langword="null"/> for non-junction node indices.
        /// Populated by the Graphics layer via <see cref="SetJunctions"/>.
        /// </summary>
        public ImmutableArray<JunctionNodeBase> Junctions { get; private set; } = ImmutableArray<JunctionNodeBase>.Empty;

        /// <summary>
        /// Indexed by track node index; returns the <see cref="TrackSegmentSection"/> for rail vector nodes,
        /// or <see langword="null"/> for non-vector node indices.
        /// Populated by the Graphics layer via <see cref="SetSegmentSections"/>.
        /// </summary>
        public ImmutableArray<TrackSegmentSection> SegmentSections { get; private set; } = ImmutableArray<TrackSegmentSection>.Empty;

        /// <summary>
        /// Indexed by track node index; returns the <see cref="TrackSegmentSection"/> for road vector nodes,
        /// or <see langword="null"/> for non-vector node indices.
        /// Populated by the Graphics layer via <see cref="SetRoadSegmentSections"/>.
        /// </summary>
        public ImmutableArray<TrackSegmentSection> RoadSegmentSections { get; private set; } = ImmutableArray<TrackSegmentSection>.Empty;

        public EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType> ContentByTile { get; } = new EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType>();

        public Dictionary<int, int> SwitchStates { get; private set; } = new Dictionary<int, int>();

        /// <summary>
        /// Pre-computed geometric data for every <see cref="VectorSectionNode"/> in both rail and road databases,
        /// keyed by <see cref="VectorSectionNode"/> reference identity.
        /// Built once during <see cref="Initialize"/> before <see cref="TrackTraveller"/> is used.
        /// </summary>
        public FrozenDictionary<VectorSectionNode, SectionGeometry> SectionGeometry { get; private set; } = FrozenDictionary<VectorSectionNode, SectionGeometry>.Empty;

        private TrackWorld(TrackModel trackModel)
        {
            this.trackModel = trackModel;
        }

        public static TrackWorld Instance => GameService<TrackWorld>.Instance;

        public static TrackWorld GameInstance(Game game) => GameService<TrackWorld>.Get(game);

        public static TrackWorld Initialize(Game game, TrackModel trackModel, TrackSectionModel trackSectionModel)
        {
            TrackWorld world = new TrackWorld(trackModel);
            world.Initialize(trackSectionModel);
            return GameService<TrackWorld>.Set(game, world);
        }

        /// <summary>
        /// Builds the 3D spatial index from <paramref name="trackSectionModel"/>'s rail track, road track, and track items.
        /// </summary>
        private void Initialize(TrackSectionModel trackSectionModel)
        {
            /// Builds the rail track 3D spatial index
            if (null != TrackDatabase)
            {
                railTrackNodes = TrackDatabase.TrackNodes;
                ContentByTile[MapContentType.Tracks] = new TileIndexedList<VectorSectionNode>(TrackDatabase.VectorNodes.SelectMany(v => v.VectorSections));
                ContentByTile[MapContentType.JunctionNodes] = new TileIndexedList<JunctionNode>(TrackDatabase.JunctionNodes);
                ContentByTile[MapContentType.EndNodes] = new TileIndexedList<EndNode>(TrackDatabase.EndNodes);

                SwitchStates = TrackDatabase.JunctionNodes.ToDictionary(j => j.NodeIndex, j => j.MainRoute);
            }
            else
            {
                railTrackNodes = ImmutableArray<TrackNodeBase>.Empty;
                ContentByTile[MapContentType.Tracks] = new TileIndexedList<VectorSectionNode>(ImmutableArray<VectorSectionNode>.Empty);
                ContentByTile[MapContentType.JunctionNodes] = new TileIndexedList<JunctionNode>(ImmutableArray<JunctionNode>.Empty);
                ContentByTile[MapContentType.EndNodes] = new TileIndexedList<EndNode>(ImmutableArray<EndNode>.Empty);
            }

            /// Builds the road track 3D spatial index
            if (null != RoadDatabase)
            {
                roadTrackNodes = RoadDatabase.TrackNodes;
                ContentByTile[MapContentType.Roads] = new TileIndexedList<VectorSectionNode>(RoadDatabase.VectorNodes.SelectMany(v => v.VectorSections));
                ContentByTile[MapContentType.RoadEndNodes] = new TileIndexedList<EndNode>(RoadDatabase.EndNodes);
            }
            else
            {
                roadTrackNodes = ImmutableArray<TrackNodeBase>.Empty;
                ContentByTile[MapContentType.Roads] = new TileIndexedList<VectorSectionNode>(ImmutableArray<VectorSectionNode>.Empty);
                ContentByTile[MapContentType.RoadEndNodes] = new TileIndexedList<EndNode>(ImmutableArray<EndNode>.Empty);
            }

            SectionGeometry = BuildSectionGeometry(trackSectionModel);
            InitializeTrackItems();
        }

        /// <summary>
        /// Builds tile-indexed collections for each track item type from the rail and road <see cref="TrackDatabase"/>.
        /// This makes track items queryable by tile through <see cref="ContentByTile"/> without
        /// depending on the 2D rendering layer.
        /// </summary>
        private void InitializeTrackItems()
        {
            ImmutableArray<Models.Track.TrackItemBase> railItems = TrackDatabase?.TrackItems ?? ImmutableArray<Models.Track.TrackItemBase>.Empty;
            ImmutableArray<Models.Track.TrackItemBase> roadItems = RoadDatabase?.TrackItems ?? ImmutableArray<Models.Track.TrackItemBase>.Empty;

            // Combine rail and road items for types that can appear in either database.
            // Materialized once to avoid repeated enumeration of the concatenated sequence (CA1851).
            List<Models.Track.TrackItemBase> allItems = railItems.Concat(roadItems).Where(i => i != null).ToList();

            ContentByTile[MapContentType.Signals] = new TileIndexedList<SignalTrackItem>(
                allItems.OfType<SignalTrackItem>().Where(s => s.NormalSignal));
            ContentByTile[MapContentType.OtherSignals] = new TileIndexedList<SignalTrackItem>(
                allItems.OfType<SignalTrackItem>().Where(s => !s.NormalSignal));
            ContentByTile[MapContentType.SpeedPosts] = new TileIndexedList<SpeedpostTrackItem>(
                allItems.OfType<SpeedpostTrackItem>());
            ContentByTile[MapContentType.MilePosts] = new TileIndexedList<MilepostTrackItem>(
                allItems.OfType<MilepostTrackItem>());
            ContentByTile[MapContentType.Crossovers] = new TileIndexedList<CrossoverTrackItem>(
                allItems.OfType<CrossoverTrackItem>());
            ContentByTile[MapContentType.LevelCrossings] = new TileIndexedList<LevelCrossingTrackItem>(
                allItems.OfType<LevelCrossingTrackItem>());
            ContentByTile[MapContentType.RoadCrossings] = new TileIndexedList<RoadLevelCrossingTrackItem>(
                allItems.OfType<RoadLevelCrossingTrackItem>());
            ContentByTile[MapContentType.Hazards] = new TileIndexedList<HazardTrackItem>(
                allItems.OfType<HazardTrackItem>());
            ContentByTile[MapContentType.Pickups] = new TileIndexedList<PickupTrackItem>(
                allItems.OfType<PickupTrackItem>());
            ContentByTile[MapContentType.SoundRegions] = new TileIndexedList<SoundRegionTrackItem>(
                allItems.OfType<SoundRegionTrackItem>());
            ContentByTile[MapContentType.CarSpawners] = new TileIndexedList<CarSpawnerTrackItem>(
                allItems.OfType<CarSpawnerTrackItem>());
            // EmptyTrackItem instances are index-preserving placeholders with no valid location
            // (WorldLocation.None at tile 0,0). Indexing them would create a spurious tile bucket
            // and corrupt boundary calculations, so they are intentionally excluded.
        }

        /// <summary>
        /// Registers rail track <see cref="TrackSegmentSection"/> instances built by the Graphics layer.
        /// The array is indexed by track node index (sparse — non-vector indices will be <see langword="null"/>).
        /// </summary>
        public void SetSegmentSections(IEnumerable<TrackSegmentSection> sections)
        {
            ArgumentNullException.ThrowIfNull(sections);
            SegmentSections = BuildIndexedArray(sections);
        }

        /// <summary>
        /// Registers road track <see cref="TrackSegmentSection"/> instances built by the Graphics layer.
        /// The array is indexed by track node index (sparse — non-vector indices will be <see langword="null"/>).
        /// </summary>
        public void SetRoadSegmentSections(IEnumerable<TrackSegmentSection> sections)
        {
            ArgumentNullException.ThrowIfNull(sections);
            RoadSegmentSections = BuildIndexedArray(sections);
        }

        /// <summary>
        /// Registers junction widget instances built by the Graphics layer.
        /// The array is indexed by track node index (sparse — non-junction indices will be <see langword="null"/>).
        /// </summary>
        public void SetJunctions(IEnumerable<JunctionNodeBase> junctions)
        {
            ArgumentNullException.ThrowIfNull(junctions);
            List<JunctionNodeBase> items = junctions.ToList();
            int maxIndex = 0;
            foreach (JunctionNodeBase junction in items)
            {
                if (junction.TrackNodeIndex > maxIndex)
                    maxIndex = junction.TrackNodeIndex;
            }
            JunctionNodeBase[] array = new JunctionNodeBase[maxIndex + 1];
            foreach (JunctionNodeBase junction in items)
                array[junction.TrackNodeIndex] = junction;
            Junctions = ImmutableArray.Create(array);
        }

        /// <summary>
        /// Returns the <see cref="JunctionNodeBase"/> widget at <paramref name="location"/>
        /// (within proximity tolerance), or <see langword="null"/> if no junction exists.
        /// </summary>
        public JunctionNodeBase JunctionNodeBaseAt(in PointD location, int tileRadius = 0)
        {
            JunctionNode junction = JunctionAt(PointD.ToWorldLocation(location), tileRadius);
            return junction != null ? Junctions[junction.NodeIndex] : null;
        }

        /// <summary>
        /// Returns all <see cref="TrackSegmentBase"/> widget instances at <paramref name="location"/>:
        /// the primary segment on the nearest track, plus segments reachable through connected junctions.
        /// </summary>
        public IEnumerable<TrackSegmentBase> SegmentBasesAt(PointD location)
        {
            WorldLocation worldLocation = PointD.ToWorldLocation(location);
            foreach (VectorSectionNode section in SectionsAt(worldLocation))
            {
                if (SectionGeometry.TryGetValue(section, out SectionGeometry geo))
                    yield return SegmentSections[geo.Node.NodeIndex].SectionSegments[geo.SectionIndex];
            }
        }

        /// <summary>
        /// Finds a junction node that connects <paramref name="start"/> and <paramref name="end"/> path points
        /// through their track nodes, returning a new <see cref="TrainPathPointBase"/> at the junction if found.
        /// </summary>
        public TrainPathPointBase FindIntermediaryConnection(TrainPathPointBase start, TrainPathPointBase end)
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(end);

            JunctionNode junction = FindIntermediaryJunction(
                start.ConnectedSegments[0].TrackNodeIndex,
                end.ConnectedSegments[0].TrackNodeIndex);
            return junction != null ? new TrainPathPoint(Junctions[junction.NodeIndex].Location, this) : null;
        }

        private static ImmutableArray<TrackSegmentSection> BuildIndexedArray(IEnumerable<TrackSegmentSection> sections)
        {
            List<TrackSegmentSection> items = sections.ToList();
            int maxIndex = 0;
            foreach (TrackSegmentSection section in items)
            {
                if (section.TrackNodeIndex > maxIndex)
                    maxIndex = section.TrackNodeIndex;
            }
            TrackSegmentSection[] array = new TrackSegmentSection[maxIndex + 1];
            foreach (TrackSegmentSection section in items)
            {
                array[section.TrackNodeIndex] = section;
            }
            return ImmutableArray.Create(array);
        }

        private FrozenDictionary<VectorSectionNode, SectionGeometry> BuildSectionGeometry(TrackSectionModel trackSectionModel)
        {
            Dictionary<VectorSectionNode, SectionGeometry> map = new Dictionary<VectorSectionNode, SectionGeometry>(ReferenceEqualityComparer.Instance);
            ImmutableDictionary<int, TrackSection> trackSections = trackSectionModel.TrackSections;

            InitializeFor(TrackDatabase);
            InitializeFor(RoadDatabase);
            return map.ToFrozenDictionary(ReferenceEqualityComparer.Instance);

            void InitializeFor(TrackDatabase trackDatabase)
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
            => ComputeSectionLocation(node?.VectorSections[sectionIndex], sectionOffset);

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
        /// Returns the total length in metres of all sections in <paramref name="node"/>,
        /// or <c>0.0</c> when <paramref name="node"/> is <see langword="null"/> or has no sections.
        /// </summary>
        public double VectorNodeLength(VectorNode node)
        {
            if (node == null)
                return 0.0;
            double length = 0.0;
            for (int i = 0; i < node.VectorSections.Length; i++)
                length += SectionLength(node, i);
            return length;
        }

        /// <summary>
        /// Returns the cumulative length in metres from the start of <paramref name="node"/> to the start
        /// of the section at <paramref name="sectionIndex"/> (i.e. the sum of lengths of sections 0 through
        /// <paramref name="sectionIndex"/>&#x202F;−&#x202F;1).
        /// Returns <c>0.0</c> when <paramref name="node"/> is <see langword="null"/> or <paramref name="sectionIndex"/> is 0.
        /// </summary>
        public double SectionOffset(VectorNode node, int sectionIndex)
        {
            if (node == null || sectionIndex <= 0)
                return 0.0;
            double offset = 0.0;
            for (int i = 0; i < sectionIndex; i++)
                offset += SectionLength(node, i);
            return offset;
        }

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
            ImmutableArray<TrackNodeConnector> nodeConnectors = TrackDatabase.TrackNodeConnectors[trackNodeIndex].TrackNodeConnectors;

            foreach (TrackNodeConnector nodeConnector in nodeConnectors)
            {
                if (TrackDatabase.TrackNodes[nodeConnector.Link] is JunctionNode junctionNode &&
                    WorldLocation.GetDistanceSquared(junctionNode.Location, location) <= ProximityToleranceSquared)
                {
                    foreach (TrackNodeConnector pin in TrackDatabase.TrackNodeConnectors[junctionNode.NodeIndex].TrackNodeConnectors)
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
            ImmutableArray<TrackNodeConnector> nodeConnectors = TrackDatabase.TrackNodeConnectors[trackNodeIndex].TrackNodeConnectors;
            return railTrackNodes[end ? nodeConnectors[1].Link : nodeConnectors[0].Link] as JunctionNode;
        }

        /// <summary>
        /// Returns the <see cref="JunctionNode"/> at the end of the track node selected by <paramref name="trackDirection"/>,
        /// or <see langword="null"/> if that connector links to a non-junction node.
        /// </summary>
        public JunctionNode TrackNodeJunction(int trackNodeIndex, TrackDirection trackDirection)
        {
            ImmutableArray<TrackNodeConnector> nodeConnectors = TrackDatabase.TrackNodeConnectors[trackNodeIndex].TrackNodeConnectors;
            return railTrackNodes[trackDirection == TrackDirection.Reverse ? nodeConnectors[1].Link : nodeConnectors[0].Link] as JunctionNode;
        }

        /// <summary>
        /// Returns the <see cref="JunctionNode"/> at the end of the track node <paramref name="trackNodeIndex"/> that is
        /// within the proximity threshold of <paramref name="location"/>, or <see langword="null"/> if neither end qualifies.
        /// When both ends are within threshold the closer one is returned.
        /// </summary>
        public JunctionNode TrackNodeJunction(in WorldLocation location, int trackNodeIndex)
        {
            ImmutableArray<TrackNodeConnector> nodeConnectors = TrackDatabase.TrackNodeConnectors[trackNodeIndex].TrackNodeConnectors;
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

            ImmutableArray<TrackNodeConnector> nodeConnectors = TrackDatabase.TrackNodeConnectors[trackNodeIndex].TrackNodeConnectors;
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

        #region Gap-filling: Section-level queries

        /// <summary>
        /// Returns the <see cref="VectorSectionNode"/> within <paramref name="node"/> whose geometry
        /// contains <paramref name="location"/> (perpendicular proximity test),
        /// or <see langword="null"/> if no section is within tolerance.
        /// </summary>
        public VectorSectionNode SectionAt(VectorNode node, in WorldLocation location)
        {
            ArgumentNullException.ThrowIfNull(node);

            VectorSectionNode nearest = null;
            double nearestDist = double.PositiveInfinity;

            foreach (VectorSectionNode section in node.VectorSections)
            {
                if (SectionGeometry.TryGetValue(section, out SectionGeometry geo))
                {
                    double d = geo.DistanceSquared(location);
                    if (!double.IsNaN(d) && d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = section;
                    }
                }
            }
            return nearestDist <= ProximityToleranceSquared ? nearest : null;
        }

        /// <summary>
        /// Returns the nearest <see cref="VectorSectionNode"/> to <paramref name="location"/>
        /// using perpendicular distance-to-segment/arc, searching tile-indexed sections.
        /// Returns <see langword="null"/> if no section is within proximity tolerance.
        /// </summary>
        public VectorSectionNode SectionAt(in WorldLocation location, int tileRadius = 0)
        {
            VectorSectionNode nearest = null;
            double nearestDist = double.PositiveInfinity;

            foreach (VectorSectionNode section in ContentByTile[MapContentType.Tracks].BoundingBox(location.Tile, tileRadius).Cast<VectorSectionNode>())
            {
                if (SectionGeometry.TryGetValue(section, out SectionGeometry geo))
                {
                    double d = geo.DistanceSquared(location);
                    if (!double.IsNaN(d) && d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = section;
                    }
                }
            }

            if (nearest != null && nearestDist <= ProximityToleranceSquared)
                return nearest;

            // Fallback: full scan (mirrors trackModel.SegmentAt with limit=false)
            if (tileRadius == 0)
            {
                foreach (VectorSectionNode section in ContentByTile[MapContentType.Tracks].Cast<VectorSectionNode>())
                {
                    if (SectionGeometry.TryGetValue(section, out SectionGeometry geo))
                    {
                        double d = geo.DistanceSquared(location);
                        if (!double.IsNaN(d) && d < nearestDist)
                        {
                            nearestDist = d;
                            nearest = section;
                        }
                    }
                }
                return nearestDist <= ProximityToleranceSquared ? nearest : null;
            }

            return null;
        }

        /// <summary>
        /// Returns all <see cref="VectorSectionNode"/> instances at <paramref name="location"/>:
        /// the primary section on the nearest track, plus any sections reachable through connected junctions.
        /// </summary>
        public IEnumerable<VectorSectionNode> SectionsAt(WorldLocation location)
        {
            int tileRadius = 0;
            // Near tile boundaries, widen the search radius
            if (Math.Abs(location.Location.X) > Tile.TileSizeOver2 - WorldLocation.ProximityTolerance ||
                Math.Abs(location.Location.Z) > Tile.TileSizeOver2 - WorldLocation.ProximityTolerance)
                tileRadius = 1;

            VectorSectionNode section = SectionAt(location, tileRadius);
            if (section == null)
                yield break;

            yield return section;

            // Return junction-connected sections from other track nodes
            int trackNodeIndex = SectionGeometry.TryGetValue(section, out SectionGeometry geo)
                ? geo.Node.NodeIndex : 0;

            if (trackNodeIndex > 0)
            {
                foreach (VectorSectionNode other in OtherVectorSectionNodesAt(location, trackNodeIndex))
                    yield return other;
            }
        }

        /// <summary>
        /// Returns a <see cref="Models.Track.TrackItemBase"/> by its index in the track database,
        /// or <see langword="null"/> for out-of-range indices.
        /// </summary>
        public Models.Track.TrackItemBase TrackItemByIndex(int index)
        {
            ImmutableArray<Models.Track.TrackItemBase> items = TrackDatabase?.TrackItems ?? ImmutableArray<Models.Track.TrackItemBase>.Empty;
            return index > -1 && index < items.Length ? items[index] : null;
        }

        /// <summary>
        /// Checks whether two path endpoints (given as track node indices) share a junction
        /// through in-pin/out-pin crossing, returning the <see cref="JunctionNode"/> if found.
        /// </summary>
        public JunctionNode FindIntermediaryJunction(int startTrackNodeIndex, int endTrackNodeIndex)
        {
            TrackDatabase trackDb = TrackDatabase;
            if (trackDb == null)
                return null;

            ImmutableArray<TrackNodeConnector> startConnectors = trackDb.TrackNodeConnectors[startTrackNodeIndex].TrackNodeConnectors;
            ImmutableArray<TrackNodeConnector> endConnectors = trackDb.TrackNodeConnectors[endTrackNodeIndex].TrackNodeConnectors;

            TrackNodeConnector[] shared = startConnectors.Intersect(endConnectors, TrackNodeConnectorComparer.LinkOnlyComparer).ToArray();
            if (shared.Length == 0)
                return null;

            TrackNodeConnectorIndex junctionConnectors = trackDb.TrackNodeConnectors[shared[0].Link];
            bool startOnIn = false;
            bool endOnIn = false;

            foreach (TrackNodeConnector connector in junctionConnectors.TrackNodeConnectors)
            {
                if (connector.ConnectorType == ConnectorType.OutPin)
                    continue;
                if (connector.Link == startTrackNodeIndex)
                    startOnIn = true;
                else if (connector.Link == endTrackNodeIndex)
                    endOnIn = true;
            }

            // Valid intermediary only when exactly one of the two is on the in-pin side
            return startOnIn ^ endOnIn
                ? railTrackNodes[junctionConnectors.NodeIndex] as JunctionNode
                : null;
        }

        #endregion
    }
}
