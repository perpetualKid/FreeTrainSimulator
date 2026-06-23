using System.Collections.Immutable;
using System.Linq;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Result of path route resolution.
    /// </summary>
    public sealed record PathRouteResolution
    {
        /// <summary>Resolved main route, or <see langword="null"/> when no main route could be built.</summary>
        public ResolvedPathRoute MainRoute { get; init; }

        /// <summary>Resolved passing routes.</summary>
        public ImmutableArray<ResolvedPathRoute> PassingRoutes { get; init; }

        /// <summary>Resolved authored-node anchors.</summary>
        public ImmutableArray<PathRouteAnchor> AuthoredNodeAnchors { get; init; }

        /// <summary>Diagnostics emitted during resolution.</summary>
        public ImmutableArray<PathRouteDiagnostic> Diagnostics { get; init; }

        /// <summary>Highest diagnostic severity emitted during resolution.</summary>
        public PathRouteDiagnosticSeverity HighestSeverity { get; init; }

        /// <summary>Whether the route has no error or fatal diagnostics.</summary>
        public bool IsValid => HighestSeverity < PathRouteDiagnosticSeverity.Error;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathRouteResolution"/> record.
        /// </summary>
        public PathRouteResolution(ResolvedPathRoute mainRoute, ImmutableArray<ResolvedPathRoute> passingRoutes = default,
            ImmutableArray<PathRouteAnchor> authoredNodeAnchors = default, ImmutableArray<PathRouteDiagnostic> diagnostics = default)
        {
            MainRoute = mainRoute;
            PassingRoutes = passingRoutes.IsDefault ? ImmutableArray<ResolvedPathRoute>.Empty : passingRoutes;
            AuthoredNodeAnchors = authoredNodeAnchors.IsDefault ? ImmutableArray<PathRouteAnchor>.Empty : authoredNodeAnchors;
            Diagnostics = diagnostics.IsDefault ? ImmutableArray<PathRouteDiagnostic>.Empty : diagnostics;
            HighestSeverity = Diagnostics.IsEmpty ? PathRouteDiagnosticSeverity.Information : Diagnostics.Max(diagnostic => diagnostic.Severity);
        }
    }
}
