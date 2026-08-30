using System.Collections.Immutable;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    /// <summary>
    /// Result of validating and materializing a path for commit or persistence.
    /// </summary>
    internal sealed record PathPersistenceValidationResult
    {
        /// <summary>
        /// Initializes a new path persistence validation result.
        /// </summary>
        public PathPersistenceValidationResult(bool persistenceAllowed, PathModel pathModel, PathRouteResolution resolution,
            ImmutableArray<PathRouteDiagnostic> diagnostics, ImmutableArray<int> changedNodeIndexes, string failureMessage,
            PathRouteDiagnostic highestActionableDiagnostic)
        {
            PersistenceAllowed = persistenceAllowed;
            PathModel = pathModel;
            Resolution = resolution;
            Diagnostics = diagnostics.IsDefault ? ImmutableArray<PathRouteDiagnostic>.Empty : diagnostics;
            ChangedNodeIndexes = changedNodeIndexes.IsDefault ? ImmutableArray<int>.Empty : changedNodeIndexes;
            FailureMessage = failureMessage;
            HighestActionableDiagnostic = highestActionableDiagnostic;
        }

        /// <summary>Whether the validated model may be committed or persisted.</summary>
        public bool PersistenceAllowed { get; }

        /// <summary>The normalized model when allowed, otherwise the unchanged source model.</summary>
        public PathModel PathModel { get; }

        /// <summary>The route resolution used for the validation decision.</summary>
        public PathRouteResolution Resolution { get; }

        /// <summary>Resolver diagnostics considered by the validation decision.</summary>
        public ImmutableArray<PathRouteDiagnostic> Diagnostics { get; }

        /// <summary>Authored or generated node indexes included by successful materialization.</summary>
        public ImmutableArray<int> ChangedNodeIndexes { get; }

        /// <summary>User-facing reason persistence or materialization was blocked.</summary>
        public string FailureMessage { get; }

        /// <summary>The highest-severity diagnostic with a suggested action, when available.</summary>
        public PathRouteDiagnostic HighestActionableDiagnostic { get; }
    }
}
