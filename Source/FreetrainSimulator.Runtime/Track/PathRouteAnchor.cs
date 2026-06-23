using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Resolved track anchor for an authored path node.
    /// </summary>
    public sealed record PathRouteAnchor
    {
        /// <summary>Authored path node index.</summary>
        public int AuthoredNodeIndex { get; init; }

        /// <summary>Stored path node location.</summary>
        public WorldLocation Location { get; init; }

        /// <summary>Resolved track node index, or -1 when unavailable.</summary>
        public int TrackNodeIndex { get; init; }

        /// <summary>Resolved track vector section index, or -1 when unavailable.</summary>
        public int TrackVectorSectionIndex { get; init; }

        /// <summary>Resolved path node type.</summary>
        public PathNodeType NodeType { get; init; }

        /// <summary>Whether this anchor has a resolved track node.</summary>
        public bool HasTrackAnchor => TrackNodeIndex >= 0;

        /// <summary>Whether this anchor is based on both stored location and resolved track information.</summary>
        public bool IsHybrid => HasTrackAnchor && Location != WorldLocation.None;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathRouteAnchor"/> record.
        /// </summary>
        public PathRouteAnchor(int authoredNodeIndex, in WorldLocation location, PathNodeType nodeType, int trackNodeIndex = -1, int trackVectorSectionIndex = -1)
        {
            if (authoredNodeIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(authoredNodeIndex), authoredNodeIndex, "Authored node index must not be negative.");

            AuthoredNodeIndex = authoredNodeIndex;
            Location = location;
            NodeType = nodeType;
            TrackNodeIndex = trackNodeIndex;
            TrackVectorSectionIndex = trackVectorSectionIndex;
        }
    }
}
