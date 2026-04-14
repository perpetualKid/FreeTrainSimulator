using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// Immutable, serializable representation of a track database (TDB file).
    /// Contains all track nodes (endpoints, vectors, junctions) and track items
    /// (signals, platforms, speedposts, crossovers, etc.) that define a route's rail or road network.
    /// </summary>
    /// <remarks>
    /// <para>Abstracts the legacy MSTS <c>.tdb</c> file format into a MemoryPack-serializable model.
    /// Individual node and item subtypes are stored in typed arrays for efficient serialization,
    /// then reassembled into unified <see cref="TrackNodes"/> and <see cref="TrackItems"/> collections
    /// on deserialization.</para>
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackDatabase
    {
        #region Serialized fields
        //these fields are used for type-safe serialization, and will be converted to TrackNodes and TrackItems after deserialization
        [MemoryPackInclude]
        private ImmutableArray<EndNode>? endNodes;
        [MemoryPackInclude]
        private ImmutableArray<VectorNode>? vectorNodes;
        [MemoryPackInclude]
        private ImmutableArray<JunctionNode>? junctionNodes;

        [MemoryPackInclude]
        private ImmutableArray<SidingTrackItem>? sidingTrackItems;
        [MemoryPackInclude]
        private ImmutableArray<PlatformTrackItem>? platformTrackItems;
        [MemoryPackInclude]
        private ImmutableArray<SpeedpostTrackItem>? speedpostTrackItems;
        [MemoryPackInclude]
        private ImmutableArray<MilepostTrackItem>? milepostTrackItems;
        [MemoryPackInclude]
        private ImmutableArray<HazardTrackItem>? hazardTrackItems;
        [MemoryPackInclude]
        private ImmutableArray<PickupTrackItem>? pickupTrackItems;
        [MemoryPackInclude]
        private ImmutableArray<LevelCrossingTrackItem>? levelCrossingTrackItems;
        [MemoryPackInclude]
        private ImmutableArray<RoadLevelCrossingTrackItem>? roadLevelCrossingTrackItems;
        [MemoryPackInclude]
        private ImmutableArray<SoundRegionTrackItem>? soundRegionTrackItems;
        [MemoryPackInclude]
        private ImmutableArray<SignalTrackItem>? signalTrackItems;
        [MemoryPackInclude]
        private ImmutableArray<CrossoverTrackItem>? crossoverTrackItems;
        [MemoryPackInclude]
        private ImmutableArray<CarSpawnerTrackItem>? carSpawnerTrackItems;
        [MemoryPackInclude]
        private ImmutableArray<EmptyTrackItem>? emptyTrackItems;
        #endregion

        #region non-serialized fields
        private ImmutableArray<int> endNodeIndices;
        private ImmutableArray<int> vectorNodeIndices;
        private ImmutableArray<int> junctionNodeIndices;
        #endregion

        /// <summary>Indicates whether this database describes a rail or road network.</summary>
        public TrackDataBaseType TrackDataBaseType { get; init; }

        /// <summary>All track nodes indexed by their <see cref="TrackNodeBase.NodeIndex"/>.
        /// Reconstructed on deserialization from the typed node arrays.</summary>
        [MemoryPackIgnore]
        public ImmutableArray<TrackNodeBase> TrackNodes { get; init; } = ImmutableArray<TrackNodeBase>.Empty;

        /// <summary>Filtered view over <see cref="TrackNodes"/> containing only <see cref="EndNode"/> entries.</summary>
        [MemoryPackIgnore]
        public TrackNodeEnumerable<EndNode> EndNodes => new TrackNodeEnumerable<EndNode>(TrackNodes, endNodeIndices);

        /// <summary>Filtered view over <see cref="TrackNodes"/> containing only <see cref="VectorNode"/> entries.</summary>
        [MemoryPackIgnore]
        public TrackNodeEnumerable<VectorNode> VectorNodes => new TrackNodeEnumerable<VectorNode>(TrackNodes, vectorNodeIndices);

        /// <summary>Filtered view over <see cref="TrackNodes"/> containing only <see cref="JunctionNode"/> entries.</summary>
        [MemoryPackIgnore]
        public TrackNodeEnumerable<JunctionNode> JunctionNodes => new TrackNodeEnumerable<JunctionNode>(TrackNodes, junctionNodeIndices);

        /// <summary>Maps track node indices to the set of track item indices that belong to each node.
        /// Corresponds to the <c>TrItemRefs</c> in the legacy TDB format.</summary>
        public ImmutableDictionary<int, TrackItemIndex> TrackItemSelectors { get; init; } = ImmutableDictionary<int, TrackItemIndex>.Empty;

        /// <summary>Pin (connector) topology for each track node, describing how nodes link together.
        /// Corresponds to the <c>TrPins</c> in the legacy TDB format.</summary>
        public ImmutableArray<TrackNodeConnectorIndex> TrackNodeConnectors { get; init; } = ImmutableArray<TrackNodeConnectorIndex>.Empty;

        /// <summary>All track items indexed by their <see cref="TrackItemBase.TrackItemIndex"/>.
        /// Reconstructed on deserialization from the typed item arrays.</summary>
        [MemoryPackIgnore]
        public ImmutableArray<TrackItemBase> TrackItems { get; init; } = ImmutableArray<TrackItemBase>.Empty;

        [MemoryPackConstructor]
        public TrackDatabase() { }

        [MemoryPackOnSerializing]
        private void OnSerializing()
        {
            endNodes = TrackNodes.OfType<EndNode>().ToImmutableArray();
            junctionNodes = TrackNodes.OfType<JunctionNode>().ToImmutableArray();
            vectorNodes = TrackNodes.OfType<VectorNode>().ToImmutableArray();

            sidingTrackItems = TrackItems.OfType<SidingTrackItem>().ToImmutableArray();
            platformTrackItems = TrackItems.OfType<PlatformTrackItem>().ToImmutableArray();
            speedpostTrackItems = TrackItems.OfType<SpeedpostTrackItem>().ToImmutableArray();
            milepostTrackItems = TrackItems.OfType<MilepostTrackItem>().ToImmutableArray();
            hazardTrackItems = TrackItems.OfType<HazardTrackItem>().ToImmutableArray();
            pickupTrackItems = TrackItems.OfType<PickupTrackItem>().ToImmutableArray();
            levelCrossingTrackItems = TrackItems.OfType<LevelCrossingTrackItem>().ToImmutableArray();
            roadLevelCrossingTrackItems = TrackItems.OfType<RoadLevelCrossingTrackItem>().ToImmutableArray();
            soundRegionTrackItems = TrackItems.OfType<SoundRegionTrackItem>().ToImmutableArray();
            signalTrackItems = TrackItems.OfType<SignalTrackItem>().ToImmutableArray();
            crossoverTrackItems = TrackItems.OfType<CrossoverTrackItem>().ToImmutableArray();
            carSpawnerTrackItems = TrackItems.OfType<CarSpawnerTrackItem>().ToImmutableArray();
            emptyTrackItems = TrackItems.OfType<EmptyTrackItem>().ToImmutableArray();
        }

        [MemoryPackOnSerialized]
        private void OnSerialized()
        {
            ImmutableArray<int>.Builder endNodesBuilder = ImmutableArray.CreateBuilder<int>();
            ImmutableArray<int>.Builder vectorNodesBuilder = ImmutableArray.CreateBuilder<int>();
            ImmutableArray<int>.Builder junctionNodesBuilder = ImmutableArray.CreateBuilder<int>();

            foreach (EndNode node in endNodes)
            {
                endNodesBuilder.Add(node.NodeIndex);
            }
            foreach (JunctionNode node in junctionNodes)
            {
                junctionNodesBuilder.Add(node.NodeIndex);
            }
            foreach (VectorNode node in vectorNodes)
            {
                vectorNodesBuilder.Add(node.NodeIndex);
            }
            endNodeIndices = endNodesBuilder.ToImmutable();
            vectorNodeIndices = vectorNodesBuilder.ToImmutable();
            junctionNodeIndices = junctionNodesBuilder.ToImmutable();

            //clear the node arrays after serialization to reduce memory
            endNodes = null;
            junctionNodes = null;
            vectorNodes = null;

            sidingTrackItems = null;
            platformTrackItems = null;
            speedpostTrackItems = null;
            milepostTrackItems = null;
            hazardTrackItems = null;
            pickupTrackItems = null;
            levelCrossingTrackItems = null;
            roadLevelCrossingTrackItems = null;
            soundRegionTrackItems = null;
            signalTrackItems = null;
            crossoverTrackItems = null;
            carSpawnerTrackItems = null;
            emptyTrackItems = null;
        }

        [MemoryPackOnDeserialized]
        private static void OnDeserialized(ref MemoryPackReader _, ref TrackDatabase trackDatabase)
        {
            if (trackDatabase == null)
                return;

            TrackNodeBase[] trackNodes = new TrackNodeBase[(trackDatabase.endNodes?.Length ?? 0) + (trackDatabase.junctionNodes?.Length ?? 0) + 
                (trackDatabase.vectorNodes?.Length ?? 0) + 1];

            ImmutableArray<int>.Builder endNodesBuilder = ImmutableArray.CreateBuilder<int>();
            ImmutableArray<int>.Builder vectorNodesBuilder = ImmutableArray.CreateBuilder<int>();
            ImmutableArray<int>.Builder junctionNodesBuilder = ImmutableArray.CreateBuilder<int>();

            foreach (EndNode node in trackDatabase.endNodes)
            {
                trackNodes[node.NodeIndex] = node;
                endNodesBuilder.Add(node.NodeIndex);
            }
            foreach (JunctionNode node in trackDatabase.junctionNodes)
            {
                trackNodes[node.NodeIndex] = node;
                junctionNodesBuilder.Add(node.NodeIndex);
            }
            foreach (VectorNode node in trackDatabase.vectorNodes)
            {
                trackNodes[node.NodeIndex] = node;
                vectorNodesBuilder.Add(node.NodeIndex);
            }

            trackDatabase.endNodeIndices = endNodesBuilder.ToImmutable();
            trackDatabase.vectorNodeIndices = vectorNodesBuilder.ToImmutable();
            trackDatabase.junctionNodeIndices = junctionNodesBuilder.ToImmutable();

            TrackItemBase[] trackItems = new TrackItemBase[(trackDatabase.sidingTrackItems?.Length ?? 0) + (trackDatabase.platformTrackItems?.Length ?? 0) +
                (trackDatabase.speedpostTrackItems?.Length ?? 0) + (trackDatabase.milepostTrackItems?.Length ?? 0) + (trackDatabase.hazardTrackItems?.Length ?? 0) +
                (trackDatabase.pickupTrackItems?.Length ?? 0) + (trackDatabase.levelCrossingTrackItems?.Length ?? 0) + (trackDatabase.roadLevelCrossingTrackItems?.Length ?? 0) +
                (trackDatabase.soundRegionTrackItems?.Length ?? 0) + (trackDatabase.signalTrackItems?.Length ?? 0) + (trackDatabase.crossoverTrackItems?.Length ?? 0) +
                (trackDatabase.carSpawnerTrackItems?.Length ?? 0) + (trackDatabase.emptyTrackItems?.Length ?? 0)];
            foreach (SidingTrackItem trackItem in trackDatabase.sidingTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }
            foreach (PlatformTrackItem trackItem in trackDatabase.platformTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }
            foreach (SpeedpostTrackItem trackItem in trackDatabase.speedpostTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }
            foreach (MilepostTrackItem trackItem in trackDatabase.milepostTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }
            foreach (HazardTrackItem trackItem in trackDatabase.hazardTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }
            foreach (PickupTrackItem trackItem in trackDatabase.pickupTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }
            foreach (LevelCrossingTrackItem trackItem in trackDatabase.levelCrossingTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }
            foreach (RoadLevelCrossingTrackItem trackItem in trackDatabase.roadLevelCrossingTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }
            foreach (SoundRegionTrackItem trackItem in trackDatabase.soundRegionTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }
            foreach (SignalTrackItem trackItem in trackDatabase.signalTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }
            foreach (CrossoverTrackItem trackItem in trackDatabase.crossoverTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }
            foreach (CarSpawnerTrackItem trackItem in trackDatabase.carSpawnerTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }
            foreach (EmptyTrackItem trackItem in trackDatabase.emptyTrackItems)
            {
                trackItems[trackItem.TrackItemIndex] = trackItem;
            }

            trackDatabase = trackDatabase with
            {
                TrackNodes = trackNodes.ToImmutableArray(),
                TrackItems = trackItems.ToImmutableArray(),

                carSpawnerTrackItems = null,
                crossoverTrackItems = null,
                emptyTrackItems = null,
                endNodes = null,
                hazardTrackItems = null,
                junctionNodes = null,
                levelCrossingTrackItems = null,
                milepostTrackItems = null,
                pickupTrackItems = null,
                platformTrackItems = null,
                roadLevelCrossingTrackItems = null,
                sidingTrackItems = null,
                signalTrackItems = null,
                soundRegionTrackItems = null,
                speedpostTrackItems = null,
                vectorNodes = null
            };
        }

    }
}
