using System.Collections.Immutable;
using System.Linq;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackDatabase
    {
        #region Serialized fields
        //these fields are used for type-safe serialization, and will be converted to TrackNodes and TrackItems after deserialization
        [MemoryPackInclude]
        private ImmutableArray<EndNode> endNodes = ImmutableArray<EndNode>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<VectorNode> vectorNodes = ImmutableArray<VectorNode>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<JunctionNode> junctionNodes = ImmutableArray<JunctionNode>.Empty;

        [MemoryPackInclude]
        private ImmutableArray<SidingTrackItem> sidingTrackItems = ImmutableArray<SidingTrackItem>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<PlatformTrackItem> platformTrackItems = ImmutableArray<PlatformTrackItem>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<SpeedpostTrackItem> speedpostTrackItems = ImmutableArray<SpeedpostTrackItem>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<MilepostTrackItem> milepostTrackItems = ImmutableArray<MilepostTrackItem>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<HazardTrackItem> hazardTrackItems = ImmutableArray<HazardTrackItem>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<PickupTrackItem> pickupTrackItems = ImmutableArray<PickupTrackItem>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<LevelCrossingTrackItem> levelCrossingTrackItems = ImmutableArray<LevelCrossingTrackItem>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<RoadLevelCrossingTrackItem> roadLevelCrossingTrackItems = ImmutableArray<RoadLevelCrossingTrackItem>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<SoundRegionTrackItem> soundRegionTrackItems = ImmutableArray<SoundRegionTrackItem>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<SignalTrackItem> signalTrackItems = ImmutableArray<SignalTrackItem>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<CrossoverTrackItem> crossoverTrackItems = ImmutableArray<CrossoverTrackItem>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<CarSpawnerTrackItem> carSpawnerTrackItems = ImmutableArray<CarSpawnerTrackItem>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<EmptyTrackItem> emptyTrackItems = ImmutableArray<EmptyTrackItem>.Empty;
        #endregion

        public TrackDataBaseType TrackDataBaseType { get; init; }
        [MemoryPackIgnore]
        public ImmutableArray<TrackNode> TrackNodes { get; init; } = ImmutableArray<TrackNode>.Empty;
        public ImmutableDictionary<int, TrackItemIndex> TrackItemsSelectors { get; init; } = ImmutableDictionary<int, TrackItemIndex>.Empty;
        public ImmutableArray<ImmutableArray<TrackNodeConnector>> TrackNodeConnectors { get; init; } = ImmutableArray<ImmutableArray<TrackNodeConnector>>.Empty;
        [MemoryPackIgnore]
        public ImmutableArray<TrackItemModel> TrackItems { get; init; } = ImmutableArray<TrackItemModel>.Empty;

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
            //clear the node arrays after serialization to reduce memory
            endNodes = ImmutableArray<EndNode>.Empty;
            junctionNodes = ImmutableArray<JunctionNode>.Empty;
            vectorNodes = ImmutableArray<VectorNode>.Empty;

            sidingTrackItems = ImmutableArray<SidingTrackItem>.Empty;
            platformTrackItems = ImmutableArray<PlatformTrackItem>.Empty;
            speedpostTrackItems = ImmutableArray<SpeedpostTrackItem>.Empty;
            milepostTrackItems = ImmutableArray<MilepostTrackItem>.Empty;
            hazardTrackItems = ImmutableArray<HazardTrackItem>.Empty;
            pickupTrackItems = ImmutableArray<PickupTrackItem>.Empty;
            levelCrossingTrackItems = ImmutableArray<LevelCrossingTrackItem>.Empty;
            roadLevelCrossingTrackItems = ImmutableArray<RoadLevelCrossingTrackItem>.Empty;
            soundRegionTrackItems = ImmutableArray<SoundRegionTrackItem>.Empty;
            signalTrackItems = ImmutableArray<SignalTrackItem>.Empty;
            crossoverTrackItems = ImmutableArray<CrossoverTrackItem>.Empty;
            carSpawnerTrackItems = ImmutableArray<CarSpawnerTrackItem>.Empty;
            emptyTrackItems = ImmutableArray<EmptyTrackItem>.Empty;
        }

        [MemoryPackOnDeserialized]
        private static void OnDeserialized(ref MemoryPackReader _, ref TrackDatabase trackDatabase)
        {
            if (trackDatabase == null)
                return;

            TrackNode[] trackNodes = new TrackNode[trackDatabase.endNodes.Length + trackDatabase.junctionNodes.Length + trackDatabase.vectorNodes.Length + 1];
            foreach (EndNode node in trackDatabase.endNodes)
            {
                trackNodes[node.NodeIndex] = node;
            }
            foreach (JunctionNode node in trackDatabase.junctionNodes)
            {
                trackNodes[node.NodeIndex] = node;
            }
            foreach (VectorNode node in trackDatabase.vectorNodes)
            {
                trackNodes[node.NodeIndex] = node;
            }

            TrackItemModel[] trackItems = new TrackItemModel[trackDatabase.sidingTrackItems.Length + trackDatabase.platformTrackItems.Length + 
                trackDatabase.speedpostTrackItems.Length + trackDatabase.milepostTrackItems.Length + trackDatabase.hazardTrackItems.Length + 
                trackDatabase.pickupTrackItems.Length + trackDatabase.levelCrossingTrackItems.Length + trackDatabase.roadLevelCrossingTrackItems.Length + 
                trackDatabase.soundRegionTrackItems.Length + trackDatabase.signalTrackItems.Length + trackDatabase.crossoverTrackItems.Length + 
                trackDatabase.carSpawnerTrackItems.Length + trackDatabase.emptyTrackItems.Length];
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
            };
        }

    }
}
