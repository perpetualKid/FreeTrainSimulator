using System.Collections.Immutable;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Outcome of resolving the span(s) affected by an authored anchor edit.
    /// </summary>
    internal enum PathSpanCommitStatus
    {
        /// <summary>The authored edit itself was rejected; nothing was resolved.</summary>
        Failed,
        /// <summary>Every affected span resolved to a single route; the result can be committed.</summary>
        Resolved,
        /// <summary>At least one affected span has several equal-cost routes; the user must choose.</summary>
        Ambiguous,
        /// <summary>At least one affected span could not be routed; the edit must not be committed.</summary>
        Unresolved,
    }

    /// <summary>
    /// Result of the unified span-commit routine: the tentative or materialized path model together with the
    /// resolution outcome for the spans adjacent to the edited node(s).
    /// </summary>
    internal sealed record PathSpanCommitResult
    {
        /// <summary>Resolution outcome for the affected spans.</summary>
        public PathSpanCommitStatus Status { get; init; }

        /// <summary>Human-readable description of the outcome or the reason the edit was not committed.</summary>
        public string Message { get; init; }

        /// <summary>
        /// The materialized path model when <see cref="Status"/> is <see cref="PathSpanCommitStatus.Resolved"/>,
        /// the tentative (uncommitted) model when the outcome is ambiguous, otherwise the unchanged source model.
        /// </summary>
        public PathModel PathModel { get; init; }

        /// <summary>Affected spans carrying several equal-cost route candidates; empty unless ambiguous.</summary>
        public ImmutableArray<ResolvedPathSpan> AmbiguousSpans { get; init; }

        /// <summary>Node indexes changed by the authored edit and its materialization.</summary>
        public ImmutableArray<int> ChangedNodeIndexes { get; init; }

        /// <summary><see langword="true"/> when the affected spans resolved and the result can be committed.</summary>
        public bool Success => Status == PathSpanCommitStatus.Resolved;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathSpanCommitResult"/> record.
        /// </summary>
        public PathSpanCommitResult(PathSpanCommitStatus status, string message, PathModel pathModel,
            ImmutableArray<ResolvedPathSpan> ambiguousSpans, ImmutableArray<int> changedNodeIndexes)
        {
            Status = status;
            Message = message;
            PathModel = pathModel;
            AmbiguousSpans = ambiguousSpans.IsDefault ? ImmutableArray<ResolvedPathSpan>.Empty : ambiguousSpans;
            ChangedNodeIndexes = changedNodeIndexes.IsDefault ? ImmutableArray<int>.Empty : changedNodeIndexes;
        }

        /// <summary>Creates a result for an authored edit that was rejected before resolution.</summary>
        public static PathSpanCommitResult Failed(string message, PathModel pathModel)
        {
            return new PathSpanCommitResult(PathSpanCommitStatus.Failed, message, pathModel,
                ImmutableArray<ResolvedPathSpan>.Empty, ImmutableArray<int>.Empty);
        }

        /// <summary>Creates a result carrying the materialized path model of a fully resolved edit.</summary>
        public static PathSpanCommitResult Resolved(string message, PathModel pathModel, ImmutableArray<int> changedNodeIndexes)
        {
            return new PathSpanCommitResult(PathSpanCommitStatus.Resolved, message, pathModel,
                ImmutableArray<ResolvedPathSpan>.Empty, changedNodeIndexes);
        }

        /// <summary>Creates a result exposing the equal-cost candidates of an ambiguous affected span.</summary>
        public static PathSpanCommitResult Ambiguous(string message, PathModel pathModel, ImmutableArray<ResolvedPathSpan> ambiguousSpans)
        {
            return new PathSpanCommitResult(PathSpanCommitStatus.Ambiguous, message, pathModel,
                ambiguousSpans, ImmutableArray<int>.Empty);
        }

        /// <summary>Creates a result for an edit whose affected span could not be routed.</summary>
        public static PathSpanCommitResult Unresolved(string message, PathModel pathModel)
        {
            return new PathSpanCommitResult(PathSpanCommitStatus.Unresolved, message, pathModel,
                ImmutableArray<ResolvedPathSpan>.Empty, ImmutableArray<int>.Empty);
        }
    }
}
