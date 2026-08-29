using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Common
{
    /// <summary>
    /// Shared fixture helpers for building a minimal initialized <see cref="TrackWorld"/> in unit tests,
    /// centralizing the otherwise-duplicated single-vector-node setup and the reflection-based
    /// <see cref="TrackDatabase"/> serialization-callback invocation.
    /// </summary>
    public static class TrackWorldTestFixture
    {
        /// <summary>
        /// Builds a <see cref="TrackWorld"/> containing a single rail vector node (index 1) spanning
        /// (0,0,0) to (100,0,0) in tile (0,0), with no registered section geometry. Sufficient for tests that
        /// only need a valid, initialized world (path-point connectivity, editor construction, snapshots).
        /// </summary>
        public static TrackWorld CreateSingleVectorNodeTrackWorld()
        {
            WorldLocation start = new WorldLocation(new Tile(0, 0), Vector3.Zero);
            WorldLocation end = new WorldLocation(new Tile(0, 0), new Vector3(100, 0, 0));
            VectorNode vectorNode = new VectorNode(start, new Tile(0, 0), end)
            {
                NodeIndex = 1,
                VectorSections = ImmutableArray<VectorSectionNode>.Empty,
            };
            TrackDatabase trackDatabase = new TrackDatabase()
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vectorNode),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex()),
            };
            InitializeTrackDatabase(trackDatabase);
            TrackModel trackModel = new TrackModel()
            {
                TrackDatabase = trackDatabase,
            };

            return TrackWorld.Initialize(null, trackModel, new TrackSectionModel());
        }

        /// <summary>
        /// Builds a closed route topology with two ways around the loop between the anchored endpoint nodes.
        /// </summary>
        public static TrackWorld CreateLoopTrackWorld()
        {
            return CreateTopologyTrackWorld([3, 4],
                [2, 3, 5], [1, 4], [1, 4], [2, 3, 5], [1, 4]);
        }

        /// <summary>
        /// Builds a sequence of switches with alternate crossover rungs between the main endpoint nodes.
        /// </summary>
        public static TrackWorld CreateJunctionLadderTrackWorld()
        {
            return CreateTopologyTrackWorld([3, 5, 7],
                [3], [7], [1, 4, 8], [3, 5], [4, 6, 8, 9], [5, 7], [2, 6, 9], [3, 5], [5, 7]);
        }

        /// <summary>
        /// Builds a main route and a single-level siding that rejoin before the end anchor.
        /// </summary>
        public static TrackWorld CreateSidingTrackWorld()
        {
            return CreateTopologyTrackWorld([3, 6],
                [3], [6], [1, 4, 5], [3, 6], [3, 6], [2, 4, 5]);
        }

        /// <summary>
        /// Builds a balloon-loop topology with a common approach and two loop sides that rejoin before the end anchor.
        /// </summary>
        public static TrackWorld CreateBalloonLoopTrackWorld()
        {
            return CreateTopologyTrackWorld([3, 7],
                [3], [7], [1, 4, 6], [3, 5], [4, 7], [3, 7], [2, 5, 6]);
        }

        /// <summary>
        /// Builds a through route with a switch leading to a terminal dead-end spur.
        /// </summary>
        public static TrackWorld CreateDeadEndTrackWorld()
        {
            return CreateTopologyTrackWorld([3],
                [3], [4], [1, 4, 5], [2, 3], [3]);
        }

        /// <summary>
        /// Builds equal-length parallel tracks between common endpoint switches.
        /// </summary>
        public static TrackWorld CreateParallelTrackWorld()
        {
            return CreateTopologyTrackWorld([3, 6],
                [3], [6], [1, 4, 5], [3, 6], [3, 6], [2, 4, 5]);
        }

        /// <summary>
        /// Builds an equal-cost ambiguous switch topology between the anchored endpoint nodes.
        /// </summary>
        public static TrackWorld CreateAmbiguousSwitchTrackWorld()
        {
            return CreateTopologyTrackWorld([3, 4],
                [3, 4], [3, 4], [1, 2], [1, 2]);
        }

        /// <summary>
        /// Invokes the non-public <c>OnSerializing</c>/<c>OnSerialized</c> callbacks on
        /// <paramref name="trackDatabase"/> so its derived node collections (vector/junction/end nodes) are
        /// populated, mirroring what MemoryPack deserialization performs in production.
        /// </summary>
        public static void InitializeTrackDatabase(TrackDatabase trackDatabase)
        {
            typeof(TrackDatabase).GetMethod("OnSerializing", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);
            typeof(TrackDatabase).GetMethod("OnSerialized", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);
        }

        private static TrackWorld CreateTopologyTrackWorld(int[] junctionNodeIndexes, params int[][] linkedNodeIndexes)
        {
            ImmutableArray<TrackNodeBase>.Builder nodes = ImmutableArray.CreateBuilder<TrackNodeBase>(linkedNodeIndexes.Length + 1);
            ImmutableArray<TrackNodeConnectorIndex>.Builder connectors = ImmutableArray.CreateBuilder<TrackNodeConnectorIndex>(linkedNodeIndexes.Length + 1);
            nodes.Add(null);
            connectors.Add(new TrackNodeConnectorIndex());

            for (int index = 1; index <= linkedNodeIndexes.Length; index++)
            {
                nodes.Add(junctionNodeIndexes.Contains(index) ? CreateJunctionNode(index) : CreateVectorNode(index));
                connectors.Add(new TrackNodeConnectorIndex
                {
                    NodeIndex = index,
                    TrackNodeConnectors = linkedNodeIndexes[index - 1]
                        .Select(link => new TrackNodeConnector { Link = link })
                        .ToImmutableArray(),
                });
            }

            TrackDatabase trackDatabase = new TrackDatabase
            {
                TrackNodes = nodes.MoveToImmutable(),
                TrackNodeConnectors = connectors.MoveToImmutable(),
            };
            InitializeTrackDatabase(trackDatabase);

            return TrackWorld.Initialize(null, new TrackModel { TrackDatabase = trackDatabase }, new TrackSectionModel());
        }

        private static TrackNodeBase CreateVectorNode(int nodeIndex)
        {
            WorldLocation start = new WorldLocation(new Tile(0, 0), new Vector3(nodeIndex * 100, 0, 0));
            WorldLocation end = new WorldLocation(new Tile(0, 0), new Vector3((nodeIndex * 100) + 50, 0, 0));
            return new VectorNode(start, new Tile(0, 0), end)
            {
                NodeIndex = nodeIndex,
                VectorSections = ImmutableArray<VectorSectionNode>.Empty,
            };
        }

        private static TrackNodeBase CreateJunctionNode(int nodeIndex)
        {
            WorldLocation location = new WorldLocation(new Tile(0, 0), new Vector3(nodeIndex * 100, 0, 0));
            return new JunctionNode(location, new Tile(0, 0), Vector3.Zero) { NodeIndex = nodeIndex };
        }
    }
}
