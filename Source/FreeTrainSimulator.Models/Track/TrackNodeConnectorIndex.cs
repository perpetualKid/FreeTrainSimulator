using System;
using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackNodeConnectorIndex
    {
        public int NodeIndex { get; init; }
        public int InboundCount { get; init; }
        public ImmutableArray<TrackNodeConnector> TrackNodeConnectors { get; init; }
        [MemoryPackIgnore]
        public ReadOnlySpan<TrackNodeConnector> InConnectors => TrackNodeConnectors[0..InboundCount].AsSpan();
        [MemoryPackIgnore]
        public ReadOnlySpan<TrackNodeConnector> OutConnectors => TrackNodeConnectors[InboundCount..].AsSpan();
    }
}
