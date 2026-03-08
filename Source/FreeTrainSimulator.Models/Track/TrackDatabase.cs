using System.Collections.Immutable;
using System.Linq;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial class TrackDatabase
    {
        [MemoryPackInclude]
        private ImmutableArray<EndNode> endNodes = ImmutableArray<EndNode>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<VectorNode> vectorNodes = ImmutableArray<VectorNode>.Empty;
        [MemoryPackInclude]
        private ImmutableArray<JunctionNode> junctionNodes = ImmutableArray<JunctionNode>.Empty;

        public TrackDataBaseType TrackDataBaseType { get; init; }
        [MemoryPackIgnore]
        public ImmutableArray<TrackNode> TrackNodes { get; init; } = ImmutableArray<TrackNode>.Empty;
        public ImmutableDictionary<int, int> TrackItems { get; init; } = ImmutableDictionary<int, int>.Empty;
        public ImmutableArray<ImmutableArray<TrackNodeConnector>> TrackNodeConnectors { get; init; } = ImmutableArray<ImmutableArray<TrackNodeConnector>>.Empty;

        [MemoryPackConstructor]
        public TrackDatabase() { }

        [MemoryPackOnSerializing]
        private void OnSerializing()
        {
            endNodes = TrackNodes.Where(tn => tn is EndNode).Cast<EndNode>().ToImmutableArray();
            junctionNodes = TrackNodes.Where(tn => tn is JunctionNode).Cast<JunctionNode>().ToImmutableArray();
            vectorNodes = TrackNodes.Where(tn => tn is VectorNode).Cast<VectorNode>().ToImmutableArray();
        }

        [MemoryPackOnSerialized]
        private void OnSerialized()
        {
            endNodes = ImmutableArray<EndNode>.Empty;
            junctionNodes = ImmutableArray<JunctionNode>.Empty;
            vectorNodes = ImmutableArray<VectorNode>.Empty;
        }

        [MemoryPackOnDeserialized]
        private static void OnDeserialized(ref MemoryPackReader reader, ref TrackDatabase trackDatabase)
        {
            if (trackDatabase == null)
                return;
            trackDatabase = new TrackDatabase()
            {
                TrackDataBaseType = trackDatabase.TrackDataBaseType,
                TrackItems = trackDatabase.TrackItems,
                TrackNodeConnectors = trackDatabase.TrackNodeConnectors,
                TrackNodes = trackDatabase.endNodes.Cast<TrackNode>().
                    Concat(trackDatabase.junctionNodes).
                    Concat(trackDatabase.vectorNodes).ToImmutableArray(),
            };
        }

    }
}
