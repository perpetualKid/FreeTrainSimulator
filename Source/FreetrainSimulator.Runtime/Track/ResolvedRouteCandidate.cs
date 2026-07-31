using System;
using System.Collections.Immutable;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// A single equal-cost route candidate for an ambiguous <see cref="ResolvedPathSpan"/>. Candidates are
    /// enumerated in a deterministic order so a selected candidate index stays stable across resolutions.
    /// </summary>
    public sealed record ResolvedRouteCandidate
    {
        /// <summary>Track node indexes traversed by the candidate, from the source anchor to the target anchor.</summary>
        public ImmutableArray<int> RouteNodeIndexes { get; init; }

        /// <summary>Track vector node indexes traversed by the candidate.</summary>
        public ImmutableArray<int> TrackVectorNodeIndexes { get; init; }

        /// <summary>Intermediary anchors generated for the candidate, excluding the span endpoints.</summary>
        public ImmutableArray<PathRouteAnchor> GeneratedIntermediaryAnchors { get; init; }

        /// <summary>Accumulated route search cost of the candidate.</summary>
        public double Cost { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvedRouteCandidate"/> record.
        /// </summary>
        public ResolvedRouteCandidate(ImmutableArray<int> routeNodeIndexes, ImmutableArray<int> trackVectorNodeIndexes, ImmutableArray<PathRouteAnchor> generatedIntermediaryAnchors, double cost)
        {
            if (cost < 0)
                throw new ArgumentOutOfRangeException(nameof(cost), cost, "Route candidate cost must not be negative.");

            RouteNodeIndexes = routeNodeIndexes.IsDefault ? ImmutableArray<int>.Empty : routeNodeIndexes;
            TrackVectorNodeIndexes = trackVectorNodeIndexes.IsDefault ? ImmutableArray<int>.Empty : trackVectorNodeIndexes;
            GeneratedIntermediaryAnchors = generatedIntermediaryAnchors.IsDefault ? ImmutableArray<PathRouteAnchor>.Empty : generatedIntermediaryAnchors;
            Cost = cost;
        }
    }
}
