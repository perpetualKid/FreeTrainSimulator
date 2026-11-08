using System;
using System.Collections.Immutable;
using System.Linq;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Resolved route for one path branch.
    /// </summary>
    public sealed record ResolvedPathRoute
    {
        /// <summary>Branch kind.</summary>
        public PathRouteBranchKind BranchKind { get; init; }

        /// <summary>Authored node index where the branch starts.</summary>
        public int StartNodeIndex { get; init; }

        /// <summary>Authored node index where the branch ends, or -1 when unresolved.</summary>
        public int EndNodeIndex { get; init; }

        /// <summary>Resolved spans in branch order.</summary>
        public ImmutableArray<ResolvedPathSpan> Spans { get; init; }

        /// <summary>
        /// Checks the selected physical traversal and final arrival heading independently of authored span
        /// boundaries. Used to verify that branch materialization preserves both the main and siding routes.
        /// </summary>
        public bool HasSamePhysicalTraversal(ResolvedPathRoute other)
        {
            ArgumentNullException.ThrowIfNull(other);
            return BranchKind != other.BranchKind || Spans.IsEmpty || other.Spans.IsEmpty || Spans.Any(span => span.Status != PathRouteSpanStatus.Resolved || span.Candidates.IsEmpty)
                || other.Spans.Any(span => span.Status != PathRouteSpanStatus.Resolved || span.Candidates.IsEmpty)
                ? false
                : Spans.SelectMany(span => span.Candidates[0].PhysicalTraversals) .SequenceEqual(other.Spans.SelectMany(span => span.Candidates[0].PhysicalTraversals))
                && Spans[^1].Candidates[0].Arrival == other.Spans[^1].Candidates[0].Arrival;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvedPathRoute"/> record.
        /// </summary>
        public ResolvedPathRoute(PathRouteBranchKind branchKind, int startNodeIndex, int endNodeIndex)
            : this(branchKind, startNodeIndex, endNodeIndex, ImmutableArray<ResolvedPathSpan>.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvedPathRoute"/> record.
        /// </summary>
        public ResolvedPathRoute(PathRouteBranchKind branchKind, int startNodeIndex, int endNodeIndex, ImmutableArray<ResolvedPathSpan> spans)
        {
            if (startNodeIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(startNodeIndex), startNodeIndex, "Start node index must not be negative.");
            if (endNodeIndex < -1)
                throw new ArgumentOutOfRangeException(nameof(endNodeIndex), endNodeIndex, "End node index must be -1 or greater.");

            BranchKind = branchKind;
            StartNodeIndex = startNodeIndex;
            EndNodeIndex = endNodeIndex;
            Spans = spans.IsDefault ? ImmutableArray<ResolvedPathSpan>.Empty : spans;
        }
    }
}
