using System;

namespace FreeTrainSimulator.Runtime.Track
{
    [Flags]
    public enum PathNodeInvalidReasons
    {
        None = 0,
        NoJunctionNode = 0x1,
        NotOnTrack = 0x2,
        NoConnectionPossible = 0x4,
        Invalid = 0x8,
    }

    public enum PathSectionType
    {
        Invalid,
        MainPath,
        PassingPath,
    }

    /// <summary>
    /// Severity of a path route resolver diagnostic.
    /// </summary>
    public enum PathRouteDiagnosticSeverity
    {
        /// <summary>Informational diagnostic that does not affect route validity.</summary>
        Information,

        /// <summary>Warning diagnostic that may require user review but does not necessarily block use.</summary>
        Warning,

        /// <summary>Error diagnostic that makes at least part of the resolved route invalid.</summary>
        Error,

        /// <summary>Fatal diagnostic that prevents meaningful route resolution.</summary>
        Fatal,
    }

    /// <summary>
    /// Stable diagnostic code emitted by the path route resolver.
    /// </summary>
    public enum PathRouteDiagnosticCode
    {
        /// <summary>No diagnostic code.</summary>
        None,

        /// <summary>The path has no authored nodes.</summary>
        EmptyPath,

        /// <summary>The path has no start node.</summary>
        MissingStartNode,

        /// <summary>The path has no end node.</summary>
        MissingEndNode,

        /// <summary>A main path link points outside the authored node collection.</summary>
        InvalidMainLink,

        /// <summary>A siding path link points outside the authored node collection.</summary>
        InvalidSidingLink,

        /// <summary>An authored node is not reachable from the start node through main or siding links.</summary>
        UnreachableNode,

        /// <summary>The authored path graph contains a cycle that the first resolver slice does not support.</summary>
        UnsupportedGraphCycle,

        /// <summary>The authored main path does not reach the end node.</summary>
        MainRouteDoesNotReachEnd,

        /// <summary>An authored node location could not be resolved to track.</summary>
        AnchorNotOnTrack,

        /// <summary>An authored node location resolves to more than one plausible track anchor.</summary>
        AmbiguousAnchor,

        /// <summary>A populated track anchor does not agree with the stored node location.</summary>
        AnchorLocationMismatch,

        /// <summary>A span between two anchored path nodes could not be resolved by dense routing.</summary>
        UnresolvedDenseSpan,

        /// <summary>A span between two anchored path nodes has multiple plausible routes.</summary>
        AmbiguousRoute,

        /// <summary>A passing branch does not rejoin the main authored path.</summary>
        PassingBranchDoesNotRejoinMain,
    }

    /// <summary>
    /// Identifies the branch type represented by a resolved route.
    /// </summary>
    public enum PathRouteBranchKind
    {
        /// <summary>The route follows the main authored path links.</summary>
        Main,

        /// <summary>The route follows a siding or passing branch.</summary>
        Passing,
    }

    /// <summary>
    /// Resolution status for a span between two authored path nodes.
    /// </summary>
    public enum PathRouteSpanStatus
    {
        /// <summary>The span was not processed.</summary>
        NotResolved,

        /// <summary>The span resolved to a deterministic track route.</summary>
        Resolved,

        /// <summary>The span has multiple plausible routes.</summary>
        Ambiguous,

        /// <summary>The span could not be resolved.</summary>
        Unresolved,
    }

}
