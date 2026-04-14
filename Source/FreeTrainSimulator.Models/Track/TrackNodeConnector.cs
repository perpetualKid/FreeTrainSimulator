using System.Collections.Generic;

using FreeTrainSimulator.Common;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// A single connection pin linking a track node to an adjacent node.
    /// </summary>
    /// <remarks>
    /// Derived from the <c>TrPin</c> entries in the MSTS <c>.tdb</c> file.
    /// Each pin records the direction of the connection and the linked node index.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackNodeConnector
    {
        /// <summary>Whether this pin is an inbound or outbound connection relative to the owning node.</summary>
        public ConnectorType ConnectorType { get; init; }
        /// <summary>Index of the track node connected to the parent of this pin.</summary>
        public int Link { get; init; }
        /// <summary>For connections to a vector node, indicates which end (start/end) is connected to.</summary>
        public TrackDirection Direction { get; init; }
    }

    /// <summary>
    /// Equality comparer for <see cref="TrackNodeConnector"/> that considers only the <see cref="TrackNodeConnector.Link"/> value.
    /// </summary>
    public class TrackNodeConnectorComparer : IEqualityComparer<TrackNodeConnector>
    {
        private TrackNodeConnectorComparer() { }

        public bool Equals(TrackNodeConnector x, TrackNodeConnector y)
        {
            return x?.Link == y?.Link;
        }

        public int GetHashCode(TrackNodeConnector obj)
        {
            return obj?.Link ?? -1;
        }

        public static TrackNodeConnectorComparer LinkOnlyComparer { get; } = new TrackNodeConnectorComparer();
    }
}
