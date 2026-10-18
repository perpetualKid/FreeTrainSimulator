using FreeTrainSimulator.Common;

namespace FreeTrainSimulator.Runtime.Track
{
    internal readonly record struct TrackRouteArrival
    {
        /// <summary>Node immediately preceding the anchor, or -1 before any physical traversal.</summary>
        internal int PreviousNodeIndex { get; init; }

        /// <summary>Exact connector used at the preceding node.</summary>
        internal int PreviousConnectorIndex { get; init; }

        /// <summary>Exact reciprocal arrival connector at the anchor.</summary>
        internal int IncomingConnectorIndex { get; init; }

        /// <summary>Heading on a geometrically resolved vector, retained across zero-length spans.</summary>
        internal TrackDirection? Direction { get; init; }

        internal TrackRouteArrival(int previousNodeIndex, int previousConnectorIndex, int incomingConnectorIndex,
            TrackDirection? direction)
        {
            PreviousNodeIndex = previousNodeIndex;
            PreviousConnectorIndex = previousConnectorIndex;
            IncomingConnectorIndex = incomingConnectorIndex;
            Direction = direction;
        }
    }
}
