using System;
using System.Collections.Immutable;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Resolved route span between two authored path nodes.
    /// </summary>
    public sealed record ResolvedPathSpan
    {
        /// <summary>Source authored path node index.</summary>
        public int FromNodeIndex { get; init; }

        /// <summary>Target authored path node index.</summary>
        public int ToNodeIndex { get; init; }

        /// <summary>Resolution status for the span.</summary>
        public PathRouteSpanStatus Status { get; init; }

        /// <summary>Resolved track vector node indexes for the span.</summary>
        public ImmutableArray<int> TrackVectorNodeIndexes { get; init; }

        /// <summary>Generated intermediary anchors for the span.</summary>
        public ImmutableArray<PathRouteAnchor> GeneratedIntermediaryAnchors { get; init; }

        /// <summary>
        /// Equal-cost route candidates for the span. Populated only for an ambiguous span; the first candidate is
        /// the deterministic route reflected by <see cref="TrackVectorNodeIndexes"/>.
        /// </summary>
        public ImmutableArray<ResolvedRouteCandidate> Candidates { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvedPathSpan"/> record.
        /// </summary>
        public ResolvedPathSpan(int fromNodeIndex, int toNodeIndex, PathRouteSpanStatus status)
            : this(fromNodeIndex, toNodeIndex, status, ImmutableArray<int>.Empty, ImmutableArray<PathRouteAnchor>.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvedPathSpan"/> record.
        /// </summary>
        public ResolvedPathSpan(int fromNodeIndex, int toNodeIndex, PathRouteSpanStatus status, ImmutableArray<int> trackVectorNodeIndexes)
            : this(fromNodeIndex, toNodeIndex, status, trackVectorNodeIndexes, ImmutableArray<PathRouteAnchor>.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvedPathSpan"/> record.
        /// </summary>
        public ResolvedPathSpan(int fromNodeIndex, int toNodeIndex, PathRouteSpanStatus status, ImmutableArray<int> trackVectorNodeIndexes, ImmutableArray<PathRouteAnchor> generatedIntermediaryAnchors)
            : this(fromNodeIndex, toNodeIndex, status, trackVectorNodeIndexes, generatedIntermediaryAnchors, ImmutableArray<ResolvedRouteCandidate>.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvedPathSpan"/> record.
        /// </summary>
        public ResolvedPathSpan(int fromNodeIndex, int toNodeIndex, PathRouteSpanStatus status, ImmutableArray<int> trackVectorNodeIndexes, ImmutableArray<PathRouteAnchor> generatedIntermediaryAnchors, ImmutableArray<ResolvedRouteCandidate> candidates)
        {
            if (fromNodeIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(fromNodeIndex), fromNodeIndex, "Source node index must not be negative.");
            if (toNodeIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(toNodeIndex), toNodeIndex, "Target node index must not be negative.");

            FromNodeIndex = fromNodeIndex;
            ToNodeIndex = toNodeIndex;
            Status = status;
            TrackVectorNodeIndexes = trackVectorNodeIndexes.IsDefault ? ImmutableArray<int>.Empty : trackVectorNodeIndexes;
            GeneratedIntermediaryAnchors = generatedIntermediaryAnchors.IsDefault ? ImmutableArray<PathRouteAnchor>.Empty : generatedIntermediaryAnchors;
            Candidates = candidates.IsDefault ? ImmutableArray<ResolvedRouteCandidate>.Empty : candidates;
        }
    }
}
