using System;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Options controlling path route resolution and validation behavior.
    /// </summary>
    public sealed record PathRouteResolverOptions
    {
        /// <summary>Default maximum sparse span search distance in metres.</summary>
        public const double DefaultMaximumSparseSearchDistance = 5000.0;

        /// <summary>Default resolver options.</summary>
        public static PathRouteResolverOptions Default { get; } = new PathRouteResolverOptions();

        /// <summary>Maximum sparse span search distance in metres.</summary>
        public double MaximumSparseSearchDistance { get; init; }

        /// <summary>Whether ambiguous spans should be reported as errors instead of warnings.</summary>
        public bool TreatAmbiguityAsError { get; init; }

        /// <summary>Whether main-route-first tie-breaking may choose one route when several candidates exist.</summary>
        public bool AllowMainRouteFirstTieBreaking { get; init; }

        /// <summary>Whether generated intermediary route nodes should be included in the result.</summary>
        public bool IncludeGeneratedIntermediaryNodes { get; init; }

        /// <summary>Whether passing branches should be resolved.</summary>
        public bool ResolvePassingBranches { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PathRouteResolverOptions"/> record.
        /// </summary>
        public PathRouteResolverOptions()
        {
            MaximumSparseSearchDistance = DefaultMaximumSparseSearchDistance;
            TreatAmbiguityAsError = false;
            AllowMainRouteFirstTieBreaking = false;
            IncludeGeneratedIntermediaryNodes = true;
            ResolvePassingBranches = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PathRouteResolverOptions"/> record.
        /// </summary>
        public PathRouteResolverOptions(double maximumSparseSearchDistance, bool treatAmbiguityAsError, 
            bool allowMainRouteFirstTieBreaking, bool includeGeneratedIntermediaryNodes,bool resolvePassingBranches)
        {
            if (maximumSparseSearchDistance <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumSparseSearchDistance), maximumSparseSearchDistance, "Maximum sparse search distance must be greater than zero.");

            MaximumSparseSearchDistance = maximumSparseSearchDistance;
            TreatAmbiguityAsError = treatAmbiguityAsError;
            AllowMainRouteFirstTieBreaking = allowMainRouteFirstTieBreaking;
            IncludeGeneratedIntermediaryNodes = includeGeneratedIntermediaryNodes;
            ResolvePassingBranches = resolvePassingBranches;
        }
    }
}
