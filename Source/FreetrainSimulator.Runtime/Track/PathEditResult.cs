using System.Collections.Immutable;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Deterministic result of a <see cref="PathModelEditor"/> mutation. The operation never mutates its
    /// input; on success <see cref="PathModel"/> is the new authored path and <see cref="ChangedNodeIndexes"/>
    /// lists the node indexes that were added, removed, or modified. On failure the original model is returned
    /// unchanged and <see cref="Message"/> explains why.
    /// </summary>
    public sealed record PathEditResult
    {
        /// <summary><see langword="true"/> when the mutation was applied; <see langword="false"/> when it was a no-op or rejected.</summary>
        public bool Success { get; init; }

        /// <summary>Human-readable description of the outcome (applied change or reason for rejection).</summary>
        public string Message { get; init; }

        /// <summary>
        /// The resulting authored path. On success this is a new instance; on failure it is the original
        /// model returned unchanged.
        /// </summary>
        public PathModel PathModel { get; init; }

        /// <summary>Authored node indexes that were added, removed, or modified by the mutation. Empty on failure.</summary>
        public ImmutableArray<int> ChangedNodeIndexes { get; init; } = ImmutableArray<int>.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathEditResult"/> record.
        /// </summary>
        public PathEditResult(bool success, string message, PathModel pathModel, ImmutableArray<int> changedNodeIndexes)
        {
            Success = success;
            Message = message;
            PathModel = pathModel;
            ChangedNodeIndexes = changedNodeIndexes.IsDefault ? ImmutableArray<int>.Empty : changedNodeIndexes;
        }

        /// <summary>
        /// Creates a failed result that returns <paramref name="pathModel"/> unchanged with the given <paramref name="message"/>.
        /// </summary>
        public static PathEditResult Failed(string message, PathModel pathModel)
        {
            return new PathEditResult(false, message, pathModel, ImmutableArray<int>.Empty);
        }

        /// <summary>
        /// Creates a successful result carrying the new <paramref name="pathModel"/> and the affected node indexes.
        /// </summary>
        public static PathEditResult Succeeded(string message, PathModel pathModel, ImmutableArray<int> changedNodeIndexes)
        {
            return new PathEditResult(true, message, pathModel, changedNodeIndexes);
        }
    }
}
