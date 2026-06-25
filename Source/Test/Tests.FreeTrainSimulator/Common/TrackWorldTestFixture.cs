using System.Collections.Immutable;
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
        /// Invokes the non-public <c>OnSerializing</c>/<c>OnSerialized</c> callbacks on
        /// <paramref name="trackDatabase"/> so its derived node collections (vector/junction/end nodes) are
        /// populated, mirroring what MemoryPack deserialization performs in production.
        /// </summary>
        public static void InitializeTrackDatabase(TrackDatabase trackDatabase)
        {
            typeof(TrackDatabase).GetMethod("OnSerializing", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);
            typeof(TrackDatabase).GetMethod("OnSerialized", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trackDatabase, null);
        }
    }
}
