using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using FreeTrainSimulator.Common;
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
            PathPersistenceValidationResult materialization = ResolveAndMaterialize(pathModel, resolution, trackWorld);
            if (!materialization.PersistenceAllowed)
                return materialization;

            ImmutableArray<PathRouteDiagnostic> eventDiagnostics = GetSimulatorEventDiagnostics(pathModel, materialization);
            if (eventDiagnostics.IsEmpty)
                return materialization;

            PathRouteDiagnostic actionableDiagnostic = eventDiagnostics.FirstOrDefault(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.SuggestedAction))
                ?? eventDiagnostics[0];
            return new PathPersistenceValidationResult(false, pathModel, materialization.Resolution,
                materialization.Diagnostics.AddRange(eventDiagnostics), default,
                DiagnosticMessage(actionableDiagnostic), actionableDiagnostic);
        }

        /// <summary>
        /// Resolves and materializes a draft without imposing Simulator event restrictions on repair operations.
        /// </summary>
        public static PathPersistenceValidationResult ResolveAndMaterialize(PathModel pathModel, TrackWorld trackWorld)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            PathRouteResolution resolution = PathRouteResolver.Resolve(pathModel, trackWorld, PathRouteResolverOptions.Default, CancellationToken.None);
            return ResolveAndMaterialize(pathModel, resolution, trackWorld);
        }

        /// <summary>
        /// Materializes an existing resolution, retaining warning-only ambiguity feedback and authored content.
        /// </summary>
        public static PathPersistenceValidationResult ResolveAndMaterialize(PathModel pathModel, PathRouteResolution resolution, TrackWorld trackWorld)
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

            if (TryGetPreservationFailure(pathModel, resolution, out PathPersistenceValidationResult preservationFailure))
                return preservationFailure;

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

        /// <summary>Checks the exact graph from its explicit Start marker, rather than assuming node zero is the root.</summary>
        internal static ImmutableArray<PathRouteDiagnostic> GetSimulatorEventDiagnostics(PathModel pathModel)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            int startNodeIndex = -1;
            ImmutableArray<PathNode> nodes = pathModel.PathNodes.IsDefault ? ImmutableArray<PathNode>.Empty : pathModel.PathNodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i]?.NodeType.Includes(PathNodeType.Start) == true)
                {
                    startNodeIndex = i;
                    break;
                }
            }

            return PathSimulatorEventCompatibility.Validate(pathModel, startNodeIndex);
        }

        /// <summary>Checks materialized events and maps only unambiguous targets back to the authored draft.</summary>
        internal static ImmutableArray<PathRouteDiagnostic> GetSimulatorEventDiagnostics(PathModel sourceModel, PathPersistenceValidationResult materialization)
        {
            ImmutableArray<PathRouteDiagnostic> diagnostics = GetSimulatorEventDiagnostics(materialization.PathModel);
            return diagnostics.Select(diagnostic => RemapSimulatorEventDiagnostic(sourceModel, materialization, diagnostic)).ToImmutableArray();
        }

        /// <summary>Reindexes a generated refusal only when its authored event and location are uniquely identifiable.</summary>
        internal static PathRouteDiagnostic RemapSimulatorEventDiagnostic(PathModel sourceModel,
            PathPersistenceValidationResult materialization, PathRouteDiagnostic diagnostic)
        {
            PathRouteDiagnostic mapped = diagnostic with
            {
                NodeIndex = SourceNodeIndex(sourceModel, materialization, diagnostic.NodeIndex),
                FromNodeIndex = SourceNodeIndex(sourceModel, materialization, diagnostic.FromNodeIndex),
                ToNodeIndex = SourceNodeIndex(sourceModel, materialization, diagnostic.ToNodeIndex),
            };
            if (mapped.NodeIndex < 0)
                return mapped;

            PathNode node = materialization.PathModel.PathNodes[diagnostic.NodeIndex];
            bool reversed = true;
            PathRouteDiagnostic localized = diagnostic.Code == PathRouteDiagnosticCode.MultipleReversalsInContinuousVector
                ? PathSimulatorEventCompatibility.ValidateTraversalNode(node, mapped.NodeIndex, false, ref reversed)
                : PathSimulatorEventCompatibility.ValidateNode(node, mapped.NodeIndex,
                    diagnostic.Code == PathRouteDiagnosticCode.UnsupportedFirstMainEvent,
                    diagnostic.Code == PathRouteDiagnosticCode.UnsupportedPassingInteriorEvent);
            return localized?.Code == diagnostic.Code ? mapped with { Message = localized.Message } : mapped;
        }

        private static int SourceNodeIndex(PathModel sourceModel, PathPersistenceValidationResult materialization, int generatedIndex)
        {
            ImmutableArray<PathNode> generatedNodes = materialization.PathModel.PathNodes;
            if (generatedIndex < 0 || generatedIndex >= generatedNodes.Length || generatedNodes[generatedIndex] == null)
                return -1;

            PathNode generated = generatedNodes[generatedIndex];
            ImmutableArray<PathNode> sourceNodes = sourceModel.PathNodes;
            int sourceIndex = -1;
            foreach (PathRouteAnchor anchor in materialization.Resolution.AuthoredNodeAnchors)
            {
                int index = anchor.AuthoredNodeIndex;
                if (index < 0 || index >= sourceNodes.Length || sourceNodes[index] == null
                    || anchor.TrackNodeIndex != generated.NodeIndex || !anchor.Location.Equals(generated.Location)
                    || !SameEvent(sourceNodes[index], generated))
                    continue;

                if (sourceIndex >= 0)
                    return -1;
                sourceIndex = index;
            }

            // Revisited locations can produce indistinguishable generated events. Do not select a plausible but
            // unproven authored node merely because its array index happens to match the generated one.
            return sourceIndex >= 0 && generatedNodes.Count(node => node != null && node.NodeIndex == generated.NodeIndex
                && node.Location.Equals(generated.Location) && SameEvent(node, generated)) == 1 ? sourceIndex : -1;
        }

        private static bool SameEvent(PathNode left, PathNode right)
        {
            const PathNodeType events = PathNodeType.Start | PathNodeType.End | PathNodeType.Wait | PathNodeType.Reversal;
            return (left.NodeType & events) == (right.NodeType & events)
                && Equals(left.WaitInfo, right.WaitInfo);
        }

        /// <summary>Combines the localized diagnostic and its localized repair guidance without adding UI text.</summary>
        internal static string DiagnosticMessage(PathRouteDiagnostic diagnostic)
        {
            return string.IsNullOrWhiteSpace(diagnostic.SuggestedAction)
                ? diagnostic.Message : $"{diagnostic.Message} {diagnostic.SuggestedAction}";
        }

        /// <summary>
        /// Materializes explicit main-route choices and compatible unselected spans without inserting detours between choices.
        /// </summary>
        public static PathPersistenceValidationResult MaterializeSelectedRoutes(PathModel pathModel, TrackWorld trackWorld, IReadOnlyDictionary<int, ResolvedRouteCandidate> selectedCandidates)
        {
            PathRouteResolution resolution = PathRouteResolver.ResolveSelectedRoutes(pathModel, trackWorld, PersistenceOptions,
                selectedCandidates, CancellationToken.None);
            return MaterializeResolvedPath(pathModel, resolution, trackWorld, PersistenceOptions);
        }

        /// <summary>
        /// Identifies authored content that materialization would omit, retaining the input model and diagnostics.
        /// </summary>
        public static bool TryGetPreservationFailure(PathModel pathModel, PathRouteResolution resolution, out PathPersistenceValidationResult failure)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(resolution);

            PathRouteDiagnostic unreachableNodeDiagnostic = resolution.Diagnostics.FirstOrDefault(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.UnreachableNode);
            if (unreachableNodeDiagnostic != null)
            {
                string failureMessage = BuildBlockedSaveMessage(unreachableNodeDiagnostic, unreachableNodeDiagnostic);
                failure = new PathPersistenceValidationResult(false, pathModel, resolution, resolution.Diagnostics, default, failureMessage, unreachableNodeDiagnostic);
                return true;
            }

            failure = null;
            return false;
        }

        /// <summary>
        /// Materializes an already resolved path and refuses the operation when authored content would be omitted or generation fails.
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

            if (TryGetPreservationFailure(pathModel, resolution, out PathPersistenceValidationResult preservationFailure))
                return preservationFailure;

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
