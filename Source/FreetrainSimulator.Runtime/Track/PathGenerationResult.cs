using System.Collections.Immutable;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Deterministic result of converting a resolved route into a persisted <see cref="PathModel"/>.
    /// </summary>
    public sealed record PathGenerationResult
    {
        /// <summary><see langword="true"/> when generation produced a usable path model.</summary>
        public bool Success { get; init; }

        /// <summary>Human-readable description of the outcome or reason generation was rejected.</summary>
        public string Message { get; init; }

        /// <summary>The generated path model on success; otherwise the original input path model when available.</summary>
        public PathModel PathModel { get; init; }

        /// <summary>Resolver diagnostics that affected generation.</summary>
        public ImmutableArray<PathRouteDiagnostic> Diagnostics { get; init; }

        /// <summary>Authored or generated node indexes included in the generated main path.</summary>
        public ImmutableArray<int> ChangedNodeIndexes { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PathGenerationResult"/> record.
        /// </summary>
        public PathGenerationResult(bool success, string message, PathModel pathModel,
            ImmutableArray<PathRouteDiagnostic> diagnostics, ImmutableArray<int> changedNodeIndexes)
        {
            Success = success;
            Message = message;
            PathModel = pathModel;
            Diagnostics = diagnostics.IsDefault ? ImmutableArray<PathRouteDiagnostic>.Empty : diagnostics;
            ChangedNodeIndexes = changedNodeIndexes.IsDefault ? ImmutableArray<int>.Empty : changedNodeIndexes;
        }

        /// <summary>
        /// Creates a failed generation result with the original path model preserved when available.
        /// </summary>
        public static PathGenerationResult Failed(string message, PathModel pathModel, ImmutableArray<PathRouteDiagnostic> diagnostics)
        {
            return new PathGenerationResult(false, message, pathModel, diagnostics, ImmutableArray<int>.Empty);
        }

        /// <summary>
        /// Creates a successful generation result carrying the generated path model and affected node indexes.
        /// </summary>
        public static PathGenerationResult Succeeded(string message, PathModel pathModel,
            ImmutableArray<PathRouteDiagnostic> diagnostics, ImmutableArray<int> changedNodeIndexes)
        {
            return new PathGenerationResult(true, message, pathModel, diagnostics, changedNodeIndexes);
        }
    }
}
