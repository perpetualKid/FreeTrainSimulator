using System;
using System.Linq;
using System.Threading;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    /// <summary>
    /// Central policy for materializing resolved paths and deciding whether normal persistence is safe.
    /// </summary>
    internal static class PathPersistenceValidationPolicy
    {
        private static PathRouteResolverOptions PersistenceOptions { get; } = PathRouteResolverOptions.Default with
        {
            AllowMainRouteFirstTieBreaking = true,
        };

        /// <summary>
        /// Resolves, validates, and materializes a path for normal persistence.
        /// </summary>
        public static PathPersistenceValidationResult ValidateForPersistence(PathModel pathModel, TrackWorld trackWorld)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            PathRouteResolution resolution = PathRouteResolver.Resolve(pathModel, trackWorld, PathRouteResolverOptions.Default, CancellationToken.None);
            return ValidateForPersistence(pathModel, resolution, trackWorld);
        }

        /// <summary>
        /// Validates and materializes a path for normal persistence using an existing route resolution.
        /// </summary>
        public static PathPersistenceValidationResult ValidateForPersistence(PathModel pathModel, PathRouteResolution resolution, TrackWorld trackWorld)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(resolution);

            PathRouteDiagnostic blockingDiagnostic = resolution.Diagnostics
                .Where(diagnostic => diagnostic.Severity >= PathRouteDiagnosticSeverity.Error)
                .OrderByDescending(diagnostic => diagnostic.Severity)
                .FirstOrDefault();
            if (blockingDiagnostic != null)
            {
                PathRouteDiagnostic actionableDiagnostic = HighestActionableDiagnostic(resolution, PathRouteDiagnosticSeverity.Error);
                string failureMessage = BuildBlockedSaveMessage(blockingDiagnostic, actionableDiagnostic);
                return new PathPersistenceValidationResult(false, pathModel, resolution, resolution.Diagnostics,
                    default, failureMessage, actionableDiagnostic ?? blockingDiagnostic);
            }

            if (resolution.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.AmbiguousRoute))
            {
                PathRouteResolution deterministicResolution = PathRouteResolver.Resolve(pathModel, trackWorld, PersistenceOptions, CancellationToken.None);
                PathPersistenceValidationResult verification = MaterializeResolvedPath(pathModel, deterministicResolution, trackWorld, PersistenceOptions);
                return verification.PersistenceAllowed
                    ? new PathPersistenceValidationResult(true, verification.PathModel, resolution, resolution.Diagnostics,
                        verification.ChangedNodeIndexes, null, null)
                    : new PathPersistenceValidationResult(false, pathModel, resolution, resolution.Diagnostics, default,
                        verification.FailureMessage, HighestActionableDiagnostic(resolution, PathRouteDiagnosticSeverity.Information));
            }

            return MaterializeResolvedPath(pathModel, resolution, trackWorld, PathRouteResolverOptions.Default);
        }

        /// <summary>
        /// Materializes an already resolved path and refuses the operation when generation fails.
        /// </summary>
        public static PathPersistenceValidationResult MaterializeResolvedPath(PathModel pathModel, PathRouteResolution resolution, TrackWorld trackWorld)
        {
            return MaterializeResolvedPath(pathModel, resolution, trackWorld, PathRouteResolverOptions.Default);
        }

        private static PathPersistenceValidationResult MaterializeResolvedPath(PathModel pathModel, PathRouteResolution resolution,
            TrackWorld trackWorld, PathRouteResolverOptions options)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(resolution);
            ArgumentNullException.ThrowIfNull(options);

            PathGenerationResult generation = PathModelRouteGenerator.GeneratePath(pathModel, resolution, trackWorld, options);
            if (generation.Success)
            {
                return new PathPersistenceValidationResult(true, generation.PathModel, resolution, resolution.Diagnostics,
                    generation.ChangedNodeIndexes, null, null);
            }

            PathRouteDiagnostic actionableDiagnostic = HighestActionableDiagnostic(resolution, PathRouteDiagnosticSeverity.Information);
            string failureMessage = $"Path cannot be materialized: {generation.Message}";
            return new PathPersistenceValidationResult(false, pathModel, resolution, resolution.Diagnostics,
                default, failureMessage, actionableDiagnostic);
        }

        private static PathRouteDiagnostic HighestActionableDiagnostic(PathRouteResolution resolution, PathRouteDiagnosticSeverity minimumSeverity)
        {
            return resolution.Diagnostics
                .Where(diagnostic => diagnostic.Severity >= minimumSeverity && !string.IsNullOrWhiteSpace(diagnostic.SuggestedAction))
                .OrderByDescending(diagnostic => diagnostic.Severity)
                .FirstOrDefault();
        }

        private static string BuildBlockedSaveMessage(PathRouteDiagnostic blockingDiagnostic, PathRouteDiagnostic actionableDiagnostic)
        {
            PathRouteDiagnostic focusedDiagnostic = actionableDiagnostic ?? blockingDiagnostic;
            string action = string.IsNullOrWhiteSpace(focusedDiagnostic.SuggestedAction)
                ? string.Empty
                : $" {focusedDiagnostic.SuggestedAction}";
            return $"Path cannot be saved because {focusedDiagnostic.Code}: {focusedDiagnostic.Message}{action}";
        }
    }
}
