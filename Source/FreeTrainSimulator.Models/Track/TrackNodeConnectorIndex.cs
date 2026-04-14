using System;
using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// Groups all connection pins for a single track node, distinguishing inbound from outbound pins.
    /// </summary>
    /// <remarks>
    /// Derived from the <c>TrPins</c> block on a <c>TrackNode</c> in the MSTS <c>.tdb</c> file.
    /// The first <see cref="InboundCount"/> connectors are inbound; the remainder are outbound.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackNodeConnectorIndex
    {
        /// <summary>Index of the track node these connectors belong to.</summary>
        public int NodeIndex { get; init; }

        /// <summary>Number of inbound connectors (the first N entries in <see cref="TrackNodeConnectors"/>).</summary>
        public int InboundCount { get; init; }

        /// <summary>All connectors for this node, with inbound pins listed first.</summary>
        public ImmutableArray<TrackNodeConnector> TrackNodeConnectors { get; init; }

        /// <summary>Slice over the inbound connectors.</summary>
        [MemoryPackIgnore]
        public ReadOnlySpan<TrackNodeConnector> InConnectors => TrackNodeConnectors[0..InboundCount].AsSpan();

        /// <summary>Slice over the outbound connectors.</summary>
        [MemoryPackIgnore]
        public ReadOnlySpan<TrackNodeConnector> OutConnectors => TrackNodeConnectors[InboundCount..].AsSpan();
    }
}
