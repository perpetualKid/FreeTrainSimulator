using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// UI-neutral, stateless mutation operations on an authored <see cref="PathModel"/>. Each operation is a
    /// pure function: it never mutates its input and returns a <see cref="PathEditResult"/> carrying a new
    /// model on success or the original model unchanged on failure. Node links
    /// (<see cref="PathNode.NextMainNode"/> / <see cref="PathNode.NextSidingNode"/>) are absolute indexes into
    /// <see cref="PathModel.PathNodes"/>; operations that change node order re-index those links so they stay
    /// consistent. Intended to back the toolbox path editor's core editing commands and its path-level
    /// (immutable <see cref="PathModel"/>) undo/redo.
    /// </summary>
    public static class PathModelEditor
    {
        private const double NearbyJunctionRepairDistanceMeters = 10.0;
        // Train.TestAbsDelay interprets 30000..39999 as 3HHMM; higher values also include legacy AI operations.
        private const int MaximumBasicWaitTimeSeconds = 29999;
        private const string basicWaitTimeOutOfRangeMessage = "Basic wait time must be between 1 and 29999 seconds. Values of 30000 or greater may encode legacy AI operations instead of a duration.";

        private const string reverseChainRequiredMessage = "Reverse Path requires a complete, reachable main chain with exactly one Start and End and at least two nodes.";
        private const string reverseUnsupportedDataMessage = "Reverse Path supports only event-free paths without siding links, invalid nodes, or unknown flags.";
        private const string pathReversedMessage = "Path reversed.";
        private const string appendChainRequiredMessage = "Path composition requires complete, reachable, event-free main paths with exactly one Start and End and no siding links or wait payloads.";
        private const string appendRouteRequiredMessage = "Path composition requires unambiguous matching track anchors and continuous routes without repeated track nodes or duplicate connectors.";
        private const string appendEndpointRequiredMessage = "The current end and extension start must have the same track node, section, and stored location. Move the endpoints into alignment before composing paths.";
        private const string appendContinuityRequiredMessage = "Path composition would change a selected track route or require a reversal at the join. Choose an extension with a compatible departure direction.";
        private const string combineChooseDifferentPathsMessage = "Choose two different saved paths to combine.";
        private const string combineRouteMismatchMessage = "Both paths must belong to the same route.";
        private const string combineChainRequiredMessage = "Path combination requires complete, reachable, event-free main paths with exactly one Start and End and no siding links or wait payloads.";
        private const string combineUnconnectableMessage = "No supported orientation connects these two paths. Add via points to bridge them manually, or choose different paths.";
        private const string combineOrientationAmbiguousMessage = "More than one orientation connects these two paths. Choosing among multiple valid orientations is not yet supported; choose different paths or combine them manually.";
        private const string combinedMessage = "Paths combined into a new unsaved path.";

        /// <summary>
        /// Reverses a complete, event-free main chain without resolving or relocating its anchors. Traversal order
        /// becomes array order with Start at zero; endpoint roles and labels swap, and all links are rebuilt.
        /// Siding links, any wait payload (including zero), events, invalid or unknown flags, and incomplete graphs
        /// are refused without changing the input. Track-route equivalence must be checked by the caller.
        /// </summary>
        public static PathEditResult ReversePath(PathModel pathModel)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (nodes.Length < 2 || nodes.Any(node => node == null))
                return PathEditResult.Failed(reverseChainRequiredMessage, PathEditorMessageKind.ReverseChainRequired, pathModel);

            const PathNodeType allowedFlags = PathNodeType.Start | PathNodeType.End | PathNodeType.Via | PathNodeType.Junction;
            if (nodes.Any(node => node.NextSidingNode != -1 || node.WaitInfo != null || (node.NodeType & ~allowedFlags) != 0))
                return PathEditResult.Failed(reverseUnsupportedDataMessage, PathEditorMessageKind.ReverseUnsupportedData, pathModel);

            int startIndex = IndexOfNodeType(nodes, PathNodeType.Start);
            int endIndex = IndexOfNodeType(nodes, PathNodeType.End);
            if (startIndex < 0 || endIndex < 0 || startIndex == endIndex
                || nodes.Count(node => node.NodeType.Includes(PathNodeType.Start)) != 1
                || nodes.Count(node => node.NodeType.Includes(PathNodeType.End)) != 1)
                return PathEditResult.Failed(reverseChainRequiredMessage, PathEditorMessageKind.ReverseChainRequired, pathModel);

            List<int> traversal = new List<int>(nodes.Length);
            HashSet<int> visited = new HashSet<int>();
            int index = startIndex;

            while (index >= 0 && index < nodes.Length && visited.Add(index))
            {
                traversal.Add(index);
                
                if (index == endIndex)
                    break;
                
                index = nodes[index].NextMainNode;
            }
            if (traversal.Count != nodes.Length || traversal[^1] != endIndex || nodes[endIndex].NextMainNode != -1)
                return PathEditResult.Failed(reverseChainRequiredMessage, PathEditorMessageKind.ReverseChainRequired, pathModel);

            ImmutableArray<PathNode>.Builder reversed = ImmutableArray.CreateBuilder<PathNode>(nodes.Length);
            for (int i = 0; i < nodes.Length; i++)
            {
                PathNode node = nodes[traversal[nodes.Length - 1 - i]];
                PathNodeType role = i == 0 ? PathNodeType.Start : i == nodes.Length - 1 ? PathNodeType.End : PathNodeType.None;
                reversed.Add(node with
                {
                    NodeType = (node.NodeType & ~(PathNodeType.Start | PathNodeType.End)) | role,
                    NextMainNode = i == nodes.Length - 1 ? -1 : i + 1,
                    NextSidingNode = -1,
                });
            }

            return PathEditResult.Succeeded(pathReversedMessage, PathEditorMessageKind.PathReversed,
                pathModel with { Start = pathModel.End, End = pathModel.Start, PathNodes = reversed.MoveToImmutable() },
                Enumerable.Range(0, nodes.Length).ToImmutableArray());
        }

        /// <summary>
        /// Appends a different event-free main path at an exactly matching hybrid endpoint, preserving both
        /// inputs and the current identity. Linked traversal becomes array order; the shared endpoint becomes
        /// a Via and the extension supplies the destination label. Refuses rerouting, implicit reversal,
        /// disconnected or repeated routes, stale bindings, unsupported data and unsafe materialization.
        /// Both paths must belong to the supplied track world; attached route identities must agree.
        /// This operation performs no persistence or history mutation; callers own those workflows.
        /// </summary>
        public static PathEditResult AppendPathAtMatchingEndpoint(PathModel pathModel, PathModel extension, TrackWorld trackWorld,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(extension);
            cancellationToken.ThrowIfCancellationRequested();

            if (trackWorld?.TrackDatabase == null)
                return PathEditResult.Failed("Path composition requires a loaded track world.", pathModel);
            if (ReferenceEquals(pathModel, extension) || (!string.IsNullOrEmpty(pathModel.Id) && string.Equals(pathModel.Id, extension.Id, StringComparison.OrdinalIgnoreCase)))
                return PathEditResult.Failed("Choose a different saved path as the extension.", PathEditorMessageKind.CompositionChooseDifferentExtension, pathModel);
            if (!string.Equals(pathModel.Parent?.Hierarchy(), extension.Parent?.Hierarchy(), StringComparison.OrdinalIgnoreCase))
                return PathEditResult.Failed("Both paths must belong to the same route.", PathEditorMessageKind.CompositionRouteMismatch, pathModel);
            if (!TryGetAppendableMainRoute(pathModel, out ImmutableArray<int> currentRoute) || !TryGetAppendableMainRoute(extension, out ImmutableArray<int> extensionRoute))
                return PathEditResult.Failed(appendChainRequiredMessage, PathEditorMessageKind.CompositionChainRequired, pathModel);

            PathRouteResolution current = PathRouteResolver.Resolve(pathModel, trackWorld, cancellationToken);
            PathRouteResolution tail = PathRouteResolver.Resolve(extension, trackWorld, cancellationToken);
            if (!HasAppendableTrackRoute(pathModel, current, trackWorld) || !HasAppendableTrackRoute(extension, tail, trackWorld))
                return PathEditResult.Failed(appendRouteRequiredMessage, PathEditorMessageKind.CompositionRouteRequired, pathModel);

            PathRouteAnchor end = current.AuthoredNodeAnchors[currentRoute[^1]];
            PathRouteAnchor start = tail.AuthoredNodeAnchors[extensionRoute[0]];
            if (end.TrackNodeIndex != start.TrackNodeIndex || end.TrackVectorSectionIndex != start.TrackVectorSectionIndex
                || end.Location != start.Location)
                return PathEditResult.Failed(appendEndpointRequiredMessage, PathEditorMessageKind.CompositionEndpointRequired, pathModel);

            int nodeCount = currentRoute.Length + extensionRoute.Length - 1;
            ImmutableArray<PathNode>.Builder builder = ImmutableArray.CreateBuilder<PathNode>(nodeCount);
            foreach (int nodeIndex in currentRoute)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PathNode node = pathModel.PathNodes[nodeIndex];
                builder.Add(node with
                {
                    NodeType = nodeIndex == currentRoute[^1] ? (node.NodeType & ~PathNodeType.End) | PathNodeType.Via : node.NodeType,
                    NextMainNode = builder.Count + 1,
                });
            }
            foreach (int nodeIndex in extensionRoute.Skip(1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Add(extension.PathNodes[nodeIndex] with { NextMainNode = builder.Count + 1 == nodeCount ? -1 : builder.Count + 1 });
            }
            PathModel composed = pathModel with
            {
                End = extension.End,
                PathNodes = builder.MoveToImmutable(),
                ValidationState = PathValidationState.NotValidated,
            };
            PathRouteResolution joined = PathRouteResolver.Resolve(composed, trackWorld, cancellationToken);
            if (!HasAppendableTrackRoute(composed, joined, trackWorld))
                return PathEditResult.Failed(appendContinuityRequiredMessage, PathEditorMessageKind.CompositionContinuityRequired, pathModel);

            ResolvedPathSpan[] expectedSpans = current.MainRoute.Spans.Concat(tail.MainRoute.Spans).ToArray();
            if (expectedSpans.Length != joined.MainRoute.Spans.Length || expectedSpans.Where((span, index) =>
                !span.Candidates[0].RouteNodeIndexes.SequenceEqual(joined.MainRoute.Spans[index].Candidates[0].RouteNodeIndexes)
                || !span.Candidates[0].PhysicalTraversals.SequenceEqual(joined.MainRoute.Spans[index].Candidates[0].PhysicalTraversals)
                || span.Candidates[0].Arrival?.Direction != joined.MainRoute.Spans[index].Candidates[0].Arrival?.Direction).Any())
                return PathEditResult.Failed(appendContinuityRequiredMessage, PathEditorMessageKind.CompositionContinuityRequired, pathModel);

            PathGenerationResult materialized = PathModelRouteGenerator.GeneratePath(composed, joined, trackWorld, PathRouteResolverOptions.Default);
            if (!materialized.Success)
                return PathEditResult.Failed(materialized.Message, pathModel);
            PathRouteResolution savedRoute = PathRouteResolver.Resolve(materialized.PathModel, trackWorld, cancellationToken);
            return !HasAppendableTrackRoute(materialized.PathModel, savedRoute, trackWorld)
                || !joined.MainRoute.Spans.SelectMany(span => span.Candidates[0].PhysicalTraversals)
                    .SequenceEqual(savedRoute.MainRoute.Spans.SelectMany(span => span.Candidates[0].PhysicalTraversals))
                || joined.MainRoute.Spans[^1].Candidates[0].Arrival?.Direction != savedRoute.MainRoute.Spans[^1].Candidates[0].Arrival?.Direction
                ? PathEditResult.Failed(appendContinuityRequiredMessage, PathEditorMessageKind.CompositionContinuityRequired, pathModel)
                : PathEditResult.Succeeded("Path extension composed at the matching endpoint.", PathEditorMessageKind.CompositionComposedAtMatchingEndpoint, composed,
                Enumerable.Range(0, nodeCount).ToImmutableArray());
        }

        private static bool TryGetAppendableMainRoute(PathModel model, out ImmutableArray<int> route)
        {
            route = ImmutableArray<int>.Empty;
            ImmutableArray<PathNode> nodes = Nodes(model);
            const PathNodeType allowedFlags = PathNodeType.Start | PathNodeType.End | PathNodeType.Via | PathNodeType.Junction;
            return nodes.Length >= 2 && nodes.All(node => node != null && node.NextSidingNode == -1
                    && node.WaitInfo == null && (node.NodeType & ~allowedFlags) == 0)
                && nodes.Count(node => node.NodeType.Includes(PathNodeType.Start)) == 1
                && nodes.Count(node => node.NodeType.Includes(PathNodeType.End)) == 1
                && TryGetMainRouteNodes(nodes, out route, out _)
                && route.Length == nodes.Length && nodes[route[^1]].NodeType.Includes(PathNodeType.End);
        }

        private static bool HasAppendableTrackRoute(PathModel model, PathRouteResolution resolution, TrackWorld world)
        {
            if (!resolution.IsValid || resolution.MainRoute == null || !resolution.PassingRoutes.IsEmpty
                || resolution.AuthoredNodeAnchors.Length != model.PathNodes.Length
                || resolution.MainRoute.Spans.Length != model.PathNodes.Length - 1
                || resolution.MainRoute.Spans.Any(span => span.Status != PathRouteSpanStatus.Resolved || span.Candidates.Length != 1)
                || resolution.Diagnostics.Any(diagnostic => diagnostic.Code is PathRouteDiagnosticCode.AnchorLocationMismatch or PathRouteDiagnosticCode.AmbiguousRoute))
                return false;

            foreach (PathRouteAnchor anchor in resolution.AuthoredNodeAnchors)
            {
                if (!anchor.HasTrackAnchor || anchor.AuthoredNodeIndex < 0 || anchor.AuthoredNodeIndex >= model.PathNodes.Length
                    || (model.PathNodes[anchor.AuthoredNodeIndex].NodeIndex > 0 && model.PathNodes[anchor.AuthoredNodeIndex].NodeIndex != anchor.TrackNodeIndex)
                    || (model.PathNodes[anchor.AuthoredNodeIndex].NodeIndex <= 0 && resolution.Diagnostics.Any(diagnostic =>
                        diagnostic.Code == PathRouteDiagnosticCode.AmbiguousAnchor && diagnostic.NodeIndex == anchor.AuthoredNodeIndex)))
                    return false;
            }

            if (!HasUniqueRouteNodes(resolution))
                return false;
            
            foreach (ResolvedPathSpan span in resolution.MainRoute.Spans)
            {
                foreach (TrackRouteTraversal traversal in span.Candidates[0].PhysicalTraversals)
                {
                    if (traversal.FromNodeIndex < 0 || traversal.FromNodeIndex >= world.TrackDatabase.TrackNodeConnectors.Length
                        || traversal.ToNodeIndex < 0 || traversal.ToNodeIndex >= world.TrackDatabase.TrackNodeConnectors.Length
                        || world.TrackDatabase.TrackNodeConnectors[traversal.FromNodeIndex] == null
                        || world.TrackDatabase.TrackNodeConnectors[traversal.ToNodeIndex] == null)
                        return false;
            
                    ImmutableArray<TrackNodeConnector> from = world.TrackDatabase.TrackNodeConnectors[traversal.FromNodeIndex].TrackNodeConnectors;
                    ImmutableArray<TrackNodeConnector> to = world.TrackDatabase.TrackNodeConnectors[traversal.ToNodeIndex].TrackNodeConnectors;
                    
                    if (from.IsDefaultOrEmpty || to.IsDefaultOrEmpty
                        || from.Count(connector => connector?.Link == traversal.ToNodeIndex) != 1
                        || to.Count(connector => connector?.Link == traversal.FromNodeIndex) != 1)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Rejects repeated track nodes, allowing consecutive spans to share their one boundary node.
        /// Append requires unique spans; Combine may retain alternatives only at its connecting span.
        /// </summary>
        private static bool HasUniqueRouteNodes(PathRouteResolution resolution)
            => HasUniqueRouteNodes(resolution, -1);

        private static bool HasUniqueRouteNodes(PathRouteResolution resolution, int connectingFromNodeIndex)
        {
            ResolvedPathSpan connectingSpan = resolution.MainRoute.Spans.FirstOrDefault(span => span.FromNodeIndex == connectingFromNodeIndex);
            int candidateCount = connectingSpan?.Candidates.Length ?? 1;
            for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
            {
                // Each alternative must be safe through the whole fixed prefix and suffix, not just at the join.
                HashSet<int> visited = new HashSet<int>();
                int previousNode = -1;
                foreach (ResolvedPathSpan span in resolution.MainRoute.Spans)
                {
                    bool isConnectingSpan = span.FromNodeIndex == connectingFromNodeIndex;
                    if ((span.Status != PathRouteSpanStatus.Resolved && !(isConnectingSpan && span.Status == PathRouteSpanStatus.Ambiguous))
                        || span.Candidates.IsEmpty || (!isConnectingSpan && span.Candidates.Length != 1))
                        return false;
                    ResolvedRouteCandidate candidate = span.Candidates[isConnectingSpan ? candidateIndex : 0];
                    if (candidate.RouteNodeIndexes.IsEmpty || (candidate.RouteNodeIndexes.Length == 1 && candidate.Arrival?.Direction == null))
                        return false;
                    for (int i = 0; i < candidate.RouteNodeIndexes.Length; i++)
                    {
                        int node = candidate.RouteNodeIndexes[i];
                        if (i == 0 && node == previousNode)
                            continue;
                        if (!visited.Add(node))
                            return false;
                        previousNode = node;
                    }
                }
            }
            return candidateCount > 0;
        }

        /// <summary>
        /// Combines two different, complete, reachable, event-free main-only saved paths into a single new
        /// authored path. Tries all four ways to join one path's endpoint to the other's (optionally reversing
        /// either side first via <see cref="ReversePath"/>) and resolves a connecting route for each orientation
        /// with <see cref="PathRouteResolver"/>. Succeeds only when exactly one orientation yields a resolvable,
        /// non-overlapping route; refuses when zero orientations connect or more than one remains viable, since
        /// choosing among several valid orientations is not yet supported. The join itself may still be an
        /// ambiguous span in the result: that is left for the normal route-candidate picker to resolve, the same
        /// way it resolves any other via point. This operation performs no persistence or history mutation;
        /// callers own those workflows.
        /// </summary>
        public static PathEditResult CombinePaths(PathModel first, PathModel second, TrackWorld trackWorld, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(first);
            ArgumentNullException.ThrowIfNull(second);
            cancellationToken.ThrowIfCancellationRequested();

            if (trackWorld?.TrackDatabase == null)
                return PathEditResult.Failed("Path combination requires a loaded track world.", first);
            if (ReferenceEquals(first, second) || (!string.IsNullOrEmpty(first.Id) && string.Equals(first.Id, second.Id, StringComparison.OrdinalIgnoreCase)))
                return PathEditResult.Failed(combineChooseDifferentPathsMessage, PathEditorMessageKind.CombineChooseDifferentPaths, first);
            if (!string.Equals(first.Parent?.Hierarchy(), second.Parent?.Hierarchy(), StringComparison.OrdinalIgnoreCase))
                return PathEditResult.Failed(combineRouteMismatchMessage, PathEditorMessageKind.CombineRouteMismatch, first);
            if (!TryGetAppendableMainRoute(first, out ImmutableArray<int> firstRoute) || !TryGetAppendableMainRoute(second, out ImmutableArray<int> secondRoute))
                return PathEditResult.Failed(combineChainRequiredMessage, PathEditorMessageKind.CombineChainRequired, first);

            PathEditResult reversedFirst = ReversePath(first);
            PathEditResult reversedSecond = ReversePath(second);
            if (!reversedFirst.Success || !reversedSecond.Success)
                return PathEditResult.Failed(combineChainRequiredMessage, PathEditorMessageKind.CombineChainRequired, first);

            (PathModel Model, ImmutableArray<int> Route)[] firstOrientations =
            {
                (first, firstRoute),
                (reversedFirst.PathModel, Enumerable.Range(0, reversedFirst.PathModel.PathNodes.Length).ToImmutableArray()),
            };
            (PathModel Model, ImmutableArray<int> Route)[] secondOrientations =
            {
                (second, secondRoute),
                (reversedSecond.PathModel, Enumerable.Range(0, reversedSecond.PathModel.PathNodes.Length).ToImmutableArray()),
            };

            PathModel combined = null;
            int viableCount = 0;
            foreach ((PathModel Model, ImmutableArray<int> Route) in firstOrientations)
            {
                foreach ((PathModel Model, ImmutableArray<int> Route) secondSide in secondOrientations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PathModel candidate = SpliceForCombine(Model, Route, secondSide.Model, secondSide.Route);
                    PathRouteResolution resolution = PathRouteResolver.Resolve(candidate, trackWorld, cancellationToken);
                    if (!resolution.IsValid || resolution.MainRoute == null || !HasUniqueRouteNodes(resolution, Route.Length - 1))
                        continue;

                    viableCount++;
                    combined = candidate;
                }
            }

            return viableCount switch
            {
                1 => PathEditResult.Succeeded(combinedMessage, PathEditorMessageKind.CombineCombined, combined,
                    Enumerable.Range(0, combined.PathNodes.Length).ToImmutableArray()),
                0 => PathEditResult.Failed(combineUnconnectableMessage, PathEditorMessageKind.CombineUnconnectable, first),
                _ => PathEditResult.Failed(combineOrientationAmbiguousMessage, PathEditorMessageKind.CombineOrientationAmbiguous, first),
            };
        }

        // Joins firstRoute's last node to secondRoute's first node by converting both into Via nodes and linking
        // them, leaving the join span for the resolver (ambiguous or not) exactly like any other via point.
        private static PathModel SpliceForCombine(PathModel first, ImmutableArray<int> firstRoute, PathModel second, ImmutableArray<int> secondRoute)
        {
            int nodeCount = firstRoute.Length + secondRoute.Length;
            ImmutableArray<PathNode>.Builder builder = ImmutableArray.CreateBuilder<PathNode>(nodeCount);
            foreach (int nodeIndex in firstRoute)
            {
                PathNode node = first.PathNodes[nodeIndex];
                builder.Add(node with
                {
                    NodeType = nodeIndex == firstRoute[^1] ? (node.NodeType & ~PathNodeType.End) | PathNodeType.Via : node.NodeType,
                    NextMainNode = builder.Count + 1,
                });
            }
            foreach (int nodeIndex in secondRoute)
            {
                PathNode node = second.PathNodes[nodeIndex];
                builder.Add(node with
                {
                    NodeType = nodeIndex == secondRoute[0] ? (node.NodeType & ~PathNodeType.Start) | PathNodeType.Via : node.NodeType,
                    NextMainNode = builder.Count + 1 == nodeCount ? -1 : builder.Count + 1,
                });
            }
            return first with
            {
                End = second.End,
                PathNodes = builder.MoveToImmutable(),
                ValidationState = PathValidationState.NotValidated,
            };
        }

        /// <summary>Updates editable path metadata without changing route nodes or path identity.</summary>
        public static PathEditResult SetMetadata(PathModel pathModel, string name, string start, string end, bool playerPath)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            string normalizedName = name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedName))
                return PathEditResult.Failed("Path name must not be empty.", pathModel);

            string normalizedStart = start?.Trim() ?? string.Empty;
            string normalizedEnd = end?.Trim() ?? string.Empty;
            if (string.Equals(pathModel.Name ?? string.Empty, normalizedName, StringComparison.Ordinal)
                && string.Equals(pathModel.Start ?? string.Empty, normalizedStart, StringComparison.Ordinal)
                && string.Equals(pathModel.End ?? string.Empty, normalizedEnd, StringComparison.Ordinal)
                && pathModel.PlayerPath == playerPath)
            {
                return PathEditResult.Succeeded("Path metadata is unchanged.", pathModel, ImmutableArray<int>.Empty);
            }

            PathModel updated = pathModel with
            {
                Name = normalizedName,
                Start = normalizedStart,
                End = normalizedEnd,
                PlayerPath = playerPath,
            };
            return PathEditResult.Succeeded("Path metadata updated.", updated, ImmutableArray<int>.Empty);
        }

        /// <summary>
        /// Sets the authored start anchor. An existing start is replaced in place; otherwise the new start is
        /// prepended and all absolute links are re-indexed.
        /// </summary>
        public static PathEditResult SetStartAnchor(PathModel pathModel, PathNode anchor, bool isJunction)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(anchor);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            int startIndex = IndexOfNodeType(nodes, PathNodeType.Start);
            if (startIndex >= 0)
            {
                PathNode replacement = CreateEndpointNode(anchor, nodes[startIndex], PathNodeType.Start, isJunction,
                    nodes[startIndex].NextMainNode, nodes[startIndex].NextSidingNode);
                return PathEditResult.Succeeded($"Replaced start anchor at node {startIndex}.",
                    pathModel with { PathNodes = nodes.SetItem(startIndex, replacement) },
                    ImmutableArray.Create(startIndex));
            }

            if (nodes.IsEmpty)
            {
                PathNode start = CreateEndpointNode(anchor, null, PathNodeType.Start, isJunction, -1, -1);
                return PathEditResult.Succeeded("Set start anchor at node 0.",
                    pathModel with { PathNodes = ImmutableArray.Create(start) },
                    ImmutableArray.Create(0));
            }

            ImmutableArray<PathNode>.Builder builder = ImmutableArray.CreateBuilder<PathNode>(nodes.Length + 1);
            builder.Add(CreateEndpointNode(anchor, null, PathNodeType.Start, isJunction, 1, -1));
            foreach (PathNode node in nodes)
            {
                builder.Add(node with
                {
                    NextMainNode = ShiftLink(node.NextMainNode, 0),
                    NextSidingNode = ShiftLink(node.NextSidingNode, 0),
                });
            }

            return PathEditResult.Succeeded("Prepended start anchor at node 0.",
                pathModel with { PathNodes = builder.ToImmutable() },
                Enumerable.Range(0, nodes.Length + 1).ToImmutableArray());
        }

        /// <summary>
        /// Sets the authored end anchor. An existing terminal end is replaced in place; otherwise a new end is
        /// appended to the safely reachable tail of the main chain.
        /// </summary>
        public static PathEditResult SetEndAnchor(PathModel pathModel, PathNode anchor, bool isJunction)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(anchor);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            int endIndex = IndexOfNodeType(nodes, PathNodeType.End);
            if (endIndex >= 0)
            {
                PathNode existingEnd = nodes[endIndex];
                if (existingEnd.NextSidingNode >= 0)
                {
                    return PathEditResult.Failed($"Cannot replace end node {endIndex} because it carries a siding branch to node {existingEnd.NextSidingNode}.", pathModel);
                }
                if (existingEnd.NextSidingNode < -1)
                {
                    return PathEditResult.Failed($"Cannot replace end node {endIndex} because its siding link {existingEnd.NextSidingNode} is invalid.", pathModel);
                }

                PathNode replacement = CreateEndpointNode(anchor, existingEnd, PathNodeType.End, isJunction, -1, -1);
                return PathEditResult.Succeeded($"Replaced end anchor at node {endIndex}.",
                    pathModel with { PathNodes = nodes.SetItem(endIndex, replacement) },
                    ImmutableArray.Create(endIndex));
            }

            int startIndex = IndexOfNodeType(nodes, PathNodeType.Start);
            if (startIndex < 0)
                return PathEditResult.Failed("Cannot set an end anchor before a start anchor exists.", pathModel);

            if (!TryFindMainTail(nodes, startIndex, out int tailIndex, out string failure))
                return PathEditResult.Failed(failure, pathModel);

            int appendedIndex = nodes.Length;
            PathNode tail = nodes[tailIndex] with { NextMainNode = appendedIndex };
            PathNode end = CreateEndpointNode(anchor, null, PathNodeType.End, isJunction, -1, -1);
            ImmutableArray<PathNode> updatedNodes = nodes.SetItem(tailIndex, tail).Add(end);

            return PathEditResult.Succeeded($"Appended end anchor at node {appendedIndex} after main-path tail {tailIndex}.",
                pathModel with { PathNodes = updatedNodes },
                ImmutableArray.Create(tailIndex, appendedIndex));
        }

        /// <summary>
        /// Marks the last node of a linear main path as the end node. Fails when a node is already the end,
        /// when the path is empty, or when there is no start node.
        /// </summary>
        public static PathEditResult AddEnd(PathModel pathModel)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (nodes.IsEmpty)
                return PathEditResult.Failed("Cannot add an end node to an empty path.", pathModel);
            if (IndexOfNodeType(nodes, PathNodeType.Start) < 0)
                return PathEditResult.Failed("Cannot add an end node before a start node exists.", pathModel);

            int lastIndex = nodes.Length - 1;
            if (nodes[lastIndex].NodeType.Includes(PathNodeType.End))
                return PathEditResult.Failed("The path already ends with an end node.", pathModel);

            // Swap Via for End (keeping any Junction/Wait/Reversal flags) and break the trailing link.
            PathNodeType nodeType = (nodes[lastIndex].NodeType & ~PathNodeType.Via) | PathNodeType.End;
            PathNode endNode = nodes[lastIndex] with { NodeType = nodeType, NextMainNode = -1 };

            return PathEditResult.Succeeded("Added end node.",
                pathModel with { PathNodes = nodes.SetItem(lastIndex, endNode) },
                ImmutableArray.Create(lastIndex));
        }

        /// <summary>
        /// Clears the end flag from the end node, leaving it as a regular via node. Fails when the
        /// path has no end node.
        /// </summary>
        public static PathEditResult RemoveEnd(PathModel pathModel)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            int endIndex = IndexOfNodeType(nodes, PathNodeType.End);
            if (endIndex < 0)
                return PathEditResult.Failed("The path has no end node to remove.", pathModel);

            // Drop the End flag; if nothing else remains, the node becomes a plain via node.
            PathNodeType nodeType = nodes[endIndex].NodeType & ~PathNodeType.End;
            if (nodeType == PathNodeType.None)
                nodeType = PathNodeType.Via;

            return PathEditResult.Succeeded("Removed end node.",
                pathModel with { PathNodes = nodes.SetItem(endIndex, nodes[endIndex] with { NodeType = nodeType }) },
                ImmutableArray.Create(endIndex));
        }

        /// <summary>
        /// Marks the first node of the path as the start node. Fails when a start node already exists or the
        /// path is empty.
        /// </summary>
        public static PathEditResult AddStart(PathModel pathModel)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (nodes.IsEmpty)
                return PathEditResult.Failed("Cannot add a start node to an empty path.", pathModel);
            if (IndexOfNodeType(nodes, PathNodeType.Start) >= 0)
                return PathEditResult.Failed("The path already has a start node.", pathModel);

            // Swap Via for Start (keeping any Junction flag); links are unchanged because the first
            // node keeps its position.
            PathNodeType nodeType = (nodes[0].NodeType & ~PathNodeType.Via) | PathNodeType.Start;

            return PathEditResult.Succeeded("Added start node.",
                pathModel with { PathNodes = nodes.SetItem(0, nodes[0] with { NodeType = nodeType }) },
                ImmutableArray.Create(0));
        }

        /// <summary>
        /// Removes the start node and re-indexes the remaining nodes and their links. Fails when the path has
        /// no start node.
        /// </summary>
        public static PathEditResult RemoveStart(PathModel pathModel)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            int startIndex = IndexOfNodeType(nodes, PathNodeType.Start);
            if (startIndex < 0)
                return PathEditResult.Failed("The path has no start node to remove.", pathModel);

            ImmutableArray<PathNode> reindexed = RemoveNodeAt(nodes, startIndex);

            return PathEditResult.Succeeded("Removed start node.",
                pathModel with { PathNodes = reindexed },
                ImmutableArray.Create(startIndex));
        }

        /// <summary>
        /// Truncates the main route after <paramref name="nodeIndex"/>, removing all later route nodes and marking
        /// the selected node as the new end. Passing branches that rejoin after the cutoff are removed atomically;
        /// branches that rejoin at or before it are preserved.
        /// </summary>
        public static PathEditResult RemoveRestOfPath(PathModel pathModel, int nodeIndex)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {nodeIndex} is out of range.", pathModel);

            if (!TryGetMainRouteNodes(nodes, out ImmutableArray<int> mainRoute, out string failure))
                return PathEditResult.Failed(failure, pathModel);

            if (nodes.Any(node => node.NextSidingNode >= 0))
                return RemoveRestOfBranchedPath(pathModel, nodes, mainRoute, nodeIndex);

            if (nodes.Length != mainRoute.Length)
                return PathEditResult.Failed("The path contains disconnected nodes outside the main route.", pathModel);

            int selectedMainPosition = mainRoute.IndexOf(nodeIndex);

            if (selectedMainPosition < 0)
                return PathEditResult.Failed($"Node {nodeIndex} is not on the main route.", pathModel);

            if (selectedMainPosition == mainRoute.Length - 1)
                return PathEditResult.Failed("The selected node is already the last main-route node; there is nothing to remove.", pathModel);

            return BuildTruncatedPath(pathModel, nodes, mainRoute.Take(selectedMainPosition + 1).ToHashSet(), nodeIndex, false, -1);
        }

        private static PathEditResult RemoveRestOfBranchedPath(PathModel pathModel, ImmutableArray<PathNode> nodes, ImmutableArray<int> mainRoute, int nodeIndex)
        {
            int selectedMainPosition = mainRoute.IndexOf(nodeIndex);

            if (selectedMainPosition < 0)
                return PathEditResult.Failed($"Node {nodeIndex} is a passing-branch interior and cannot be used to truncate the main route.", pathModel);

            if (selectedMainPosition == mainRoute.Length - 1)
                return PathEditResult.Failed("The selected node is already the last main-route node; there is nothing to remove.", pathModel);

            ImmutableArray<int> branchStarts = Enumerable.Range(0, nodes.Length).Where(index => nodes[index].NextMainNode >= 0 && nodes[index].NextSidingNode >= 0).ToImmutableArray();
            string failure = null;

            if (branchStarts.Length != 1 || !TryGetSinglePassingBranch(nodes, branchStarts[0], out int rejoinNodeIndex, out ImmutableArray<int> interiorNodes, out failure))
            {
                return PathEditResult.Failed(failure ?? "The path does not contain exactly one supported passing branch.", pathModel);
            }

            int rejoinPosition = mainRoute.IndexOf(rejoinNodeIndex);
            HashSet<int> retained = mainRoute.Take(selectedMainPosition + 1).ToHashSet();
            bool preserveBranch = selectedMainPosition >= rejoinPosition;
            if (preserveBranch)
                retained.UnionWith(interiorNodes);

            return BuildTruncatedPath(pathModel, nodes, retained, nodeIndex, preserveBranch, branchStarts[0]);
        }

        private static PathEditResult BuildTruncatedPath(PathModel pathModel, ImmutableArray<PathNode> nodes, HashSet<int> retained, int nodeIndex, bool preserveBranch, int branchStartIndex)
        {
            ImmutableArray<int> retainedIndexes = Enumerable.Range(0, nodes.Length).Where(retained.Contains).ToImmutableArray();
            Dictionary<int, int> indexMap = retainedIndexes.Select((oldIndex, newIndex) => (oldIndex, newIndex)).ToDictionary(item => item.oldIndex, item => item.newIndex);
            int updatedNodeIndex = indexMap[nodeIndex];
            ImmutableArray<PathNode>.Builder builder = ImmutableArray.CreateBuilder<PathNode>(retainedIndexes.Length);

            foreach (int oldIndex in retainedIndexes)
            {
                PathNode node = nodes[oldIndex];
                int nextMainNode = indexMap.TryGetValue(node.NextMainNode, out int remappedMain) ? remappedMain : -1;
                int nextSidingNode = indexMap.TryGetValue(node.NextSidingNode, out int remappedSiding) ? remappedSiding : -1;
                PathNodeType nodeType = oldIndex == nodeIndex
                    ? (node.NodeType & ~PathNodeType.Via) | PathNodeType.End
                    : node.NodeType & ~PathNodeType.End;
                builder.Add(node with
                {
                    NodeType = nodeType,
                    NextMainNode = oldIndex == nodeIndex ? -1 : nextMainNode,
                    NextSidingNode = oldIndex == nodeIndex ? -1 : nextSidingNode,
                });
            }

            ImmutableArray<PathNode> updatedNodes = builder.ToImmutable();
            if (preserveBranch)
            {
                int updatedStartIndex = indexMap[branchStartIndex];
                if (!TryGetSinglePassingBranch(updatedNodes, updatedStartIndex, out _, out _, out string failure))
                    return PathEditResult.Failed($"Truncating after node {nodeIndex} would invalidate the supported passing branch: {failure}", pathModel);
            }

            return PathEditResult.Succeeded($"Removed {nodes.Length - updatedNodes.Length} node(s) after node {nodeIndex}.", pathModel with { PathNodes = updatedNodes }, ImmutableArray.Create(updatedNodeIndex));
        }

        /// <summary>
        /// Makes an ambiguous span unambiguous by inserting the chosen candidate's intermediary anchors as
        /// authored via points after the node at <paramref name="nodeIndex"/>. Fails for an out-of-range
        /// index or when the candidate carries no intermediary anchors to author.
        /// </summary>
        public static PathEditResult ApplyRouteCandidate(PathModel pathModel, int nodeIndex, ResolvedRouteCandidate candidate)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(candidate);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);

            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {nodeIndex} is out of range.", pathModel);

            if (candidate.GeneratedIntermediaryAnchors.IsDefaultOrEmpty)
                return PathEditResult.Failed("The selected route candidate has no intermediary anchors to author.", pathModel);

            PathModel current = pathModel;
            ImmutableArray<int>.Builder changedNodeIndexes = ImmutableArray.CreateBuilder<int>();
            int insertAfterIndex = nodeIndex;

            foreach (PathRouteAnchor anchor in candidate.GeneratedIntermediaryAnchors)
            {
                PathNode viaAnchor = new PathNode(anchor.Location) { NodeIndex = anchor.TrackNodeIndex };
                PathEditResult result = InsertViaPoint(current, insertAfterIndex, viaAnchor, anchor.NodeType.Includes(PathNodeType.Junction));

                if (!result.Success)
                    return PathEditResult.Failed(result.Message, pathModel);

                current = result.PathModel;
                insertAfterIndex++;
                changedNodeIndexes.Add(insertAfterIndex);
            }

            return PathEditResult.Succeeded($"Applied route candidate with {candidate.GeneratedIntermediaryAnchors.Length} via point(s) after node {nodeIndex}.",
                current, changedNodeIndexes.ToImmutable());
        }

        /// <summary>
        /// Inserts a via point directly after the node at <paramref name="nodeIndex"/>, linking it into the
        /// main chain and re-indexing all following links. Fails for an out-of-range index or when the preceding node
        /// is the end node.
        /// </summary>
        public static PathEditResult InsertViaPoint(PathModel pathModel, int nodeIndex, PathNode anchor, bool isJunction)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(anchor);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);

            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {nodeIndex} is out of range.", pathModel);

            if (nodes[nodeIndex].NodeType.Includes(PathNodeType.End))
                return PathEditResult.Failed("Cannot insert a via point after the end node.", pathModel);

            int insertIndex = nodeIndex + 1;
            ImmutableArray<PathNode>.Builder builder = ImmutableArray.CreateBuilder<PathNode>(nodes.Length + 1);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (i == insertIndex)
                    builder.Add(CreateViaNode(anchor, isJunction, ShiftLink(nodes[nodeIndex].NextMainNode, insertIndex)));

                builder.Add(nodes[i] with
                {
                    NextMainNode = i == nodeIndex ? insertIndex : ShiftLink(nodes[i].NextMainNode, insertIndex),
                    NextSidingNode = ShiftLink(nodes[i].NextSidingNode, insertIndex),
                });
            }

            if (insertIndex == nodes.Length)
                builder.Add(CreateViaNode(anchor, isJunction, ShiftLink(nodes[nodeIndex].NextMainNode, insertIndex)));

            return PathEditResult.Succeeded($"Inserted via point at node {insertIndex}.", pathModel with { PathNodes = builder.ToImmutable() }, ImmutableArray.Create(insertIndex));
        }

        /// <summary>
        /// Inserts a via point directly after the node at <paramref name="nodeIndex"/> into the siding chain of the
        /// one supported passing branch, steering its interior route through the new anchor instead of the
        /// resolver's default pick. Unlike <see cref="InsertViaPoint"/>, which always targets the main chain, this
        /// always targets the branch's siding links, and only accepts the branch's start node or one of its
        /// existing interior anchors, so the insertion always lands inside the branch and before its rejoin node.
        /// </summary>
        public static PathEditResult InsertPassingBranchViaPoint(PathModel pathModel, int nodeIndex, PathNode anchor, bool isJunction)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(anchor);

            if (!TryGetPassingBranchNodeRole(pathModel, nodeIndex, out PassingBranchNodeRole role, out string failure))
                return PathEditResult.Failed(failure, pathModel);

            if (role != PassingBranchNodeRole.BranchStart && role != PassingBranchNodeRole.BranchInterior)
                return PathEditResult.Failed($"Node {nodeIndex} is not the start or an interior anchor of the supported passing branch.", pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            int nextSidingNode = nodes[nodeIndex].NextSidingNode;
            int insertIndex = nodeIndex + 1;

            ImmutableArray<PathNode>.Builder builder = ImmutableArray.CreateBuilder<PathNode>(nodes.Length + 1);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (i == insertIndex)
                    builder.Add(CreateSidingViaNode(anchor, isJunction, ShiftLink(nextSidingNode, insertIndex)));

                builder.Add(nodes[i] with
                {
                    NextMainNode = ShiftLink(nodes[i].NextMainNode, insertIndex),
                    NextSidingNode = i == nodeIndex ? insertIndex : ShiftLink(nodes[i].NextSidingNode, insertIndex),
                });
            }

            if (insertIndex == nodes.Length)
                builder.Add(CreateSidingViaNode(anchor, isJunction, ShiftLink(nextSidingNode, insertIndex)));

            return PathEditResult.Succeeded($"Inserted passing-branch via point at node {insertIndex}.", pathModel with { PathNodes = builder.ToImmutable() }, ImmutableArray.Create(insertIndex));
        }

        /// <summary>
        /// Removes the via point at <paramref name="nodeIndex"/>, relinking its predecessors to its successor and
        /// re-indexing the remaining links. Passing-branch endpoints and edits that invalidate a supported passing
        /// branch are rejected.
        /// </summary>
        public static PathEditResult RemoveViaPoint(PathModel pathModel, int nodeIndex)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);

            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {nodeIndex} is out of range.", pathModel);

            PathNode removed = nodes[nodeIndex];
            if ((removed.NodeType & (PathNodeType.Start | PathNodeType.End)) != PathNodeType.None)
                return PathEditResult.Failed($"Node {nodeIndex} is a start or end node and is not a via point.", pathModel);

            bool hasPassingBranchLinks = nodes.Any(node => node.NextSidingNode >= 0);
            int passingBranchStartIndex = -1;

            if (hasPassingBranchLinks)
            {
                if (!TryGetPassingBranchNodeRole(pathModel, nodeIndex, out PassingBranchNodeRole role, out string failure))
                    return PathEditResult.Failed(failure, pathModel);

                if (role == PassingBranchNodeRole.BranchStart || role == PassingBranchNodeRole.BranchRejoin)
                    return PathEditResult.Failed($"Node {nodeIndex} is a passing-branch endpoint and cannot be removed as a via point.", pathModel);

                passingBranchStartIndex = Enumerable.Range(0, nodes.Length).Single(index => nodes[index].NextMainNode >= 0 && nodes[index].NextSidingNode >= 0);
            }

            // Bridge the gap first (still using pre-removal indexes), then let RemoveNodeAt re-index everything.
            ImmutableArray<PathNode> relinked = ImmutableArray.CreateRange(nodes, node => node with
            {
                NextMainNode = node.NextMainNode == nodeIndex ? removed.NextMainNode : node.NextMainNode,
                NextSidingNode = node.NextSidingNode == nodeIndex ? removed.NextSidingNode : node.NextSidingNode,
            });

            ImmutableArray<PathNode> updatedNodes = RemoveNodeAt(relinked, nodeIndex);
            if (hasPassingBranchLinks)
            {
                int updatedStartIndex = passingBranchStartIndex > nodeIndex ? passingBranchStartIndex - 1 : passingBranchStartIndex;

                if (!TryGetSinglePassingBranch(updatedNodes, updatedStartIndex, out _, out _, out string failure))
                    return PathEditResult.Failed($"Removing node {nodeIndex} would invalidate the supported passing branch: {failure}", pathModel);
            }

            return PathEditResult.Succeeded($"Removed via point {nodeIndex}.", pathModel with { PathNodes = updatedNodes }, ImmutableArray.Create(nodeIndex));
        }

        /// <summary>
        /// Creates one single-level passing branch from a main-route node to a later main-route node. The branch
        /// initially has no authored interior anchors; callers can materialize resolver-selected candidates with
        /// <see cref="AddPassingBranchAnchors"/>. Existing, nested, and overlapping branches are refused.
        /// </summary>
        public static PathEditResult CreatePassingBranch(PathModel pathModel, int startNodeIndex, int rejoinNodeIndex)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);

            if (!TryGetMainRouteNodes(nodes, out ImmutableArray<int> mainRoute, out string failure))
                return PathEditResult.Failed(failure, pathModel);
            
            if (!IsEligiblePassingBranch(nodes, mainRoute, startNodeIndex, rejoinNodeIndex, out failure))
                return PathEditResult.Failed(failure, pathModel);

            ImmutableArray<PathNode> updatedNodes = nodes.SetItem(startNodeIndex, nodes[startNodeIndex] with { NextSidingNode = rejoinNodeIndex });
            
            return PathEditResult.Succeeded($"Created passing branch from node {startNodeIndex} to node {rejoinNodeIndex}.", pathModel with { PathNodes = updatedNodes }, ImmutableArray.Create(startNodeIndex, rejoinNodeIndex));
        }

        /// <summary>Materializes resolver-selected intermediary anchors on an existing supported passing branch.</summary>
        public static PathEditResult AddPassingBranchAnchors(PathModel pathModel, int startNodeIndex, ImmutableArray<PathRouteAnchor> anchors)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            if (anchors.IsDefaultOrEmpty)
                return PathEditResult.Failed("The selected passing route candidate has no intermediary anchors to author.", pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);

            if (!TryGetSinglePassingBranch(nodes, startNodeIndex, out int rejoinNodeIndex, out _, out string failure))
                return PathEditResult.Failed(failure, pathModel);

            ImmutableArray<PathNode>.Builder builder = nodes.ToBuilder();
            ImmutableArray<int>.Builder changed = ImmutableArray.CreateBuilder<int>(anchors.Length + 2);
            int previousIndex = startNodeIndex;

            foreach (PathRouteAnchor anchor in anchors)
            {
                int newIndex = builder.Count;
                builder.Add(new PathNode(anchor.Location)
                {
                    NodeType = anchor.NodeType.Includes(PathNodeType.Junction) ? PathNodeType.Junction : PathNodeType.Via,
                    NodeIndex = anchor.TrackNodeIndex,
                    NextMainNode = -1,
                    NextSidingNode = rejoinNodeIndex,
                });
                builder[previousIndex] = builder[previousIndex] with { NextSidingNode = newIndex };
                previousIndex = newIndex;
                changed.Add(newIndex);
            }

            changed.Add(startNodeIndex);
            changed.Add(rejoinNodeIndex);

            return PathEditResult.Succeeded($"Added {anchors.Length} passing-branch anchor(s) after node {startNodeIndex}.",
                pathModel with { PathNodes = builder.ToImmutable() }, changed.ToImmutable());
        }

        /// <summary>
        /// Moves an interior anchor of a supported single-level passing branch. Event markers and nonzero wait
        /// payloads are refused because the Simulator does not support them on passing interiors.
        /// </summary>
        public static PathEditResult MovePassingBranchAnchor(PathModel pathModel, int nodeIndex, PathNode anchor, bool isJunction)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(anchor);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);

            if (!TryGetPassingBranchInteriorNodes(nodes, out ImmutableArray<int> interiorNodes, out string failure))
                return PathEditResult.Failed(failure, pathModel);
            
            if (!interiorNodes.Contains(nodeIndex))
                return PathEditResult.Failed($"Node {nodeIndex} is not an interior anchor of the supported passing branch.", pathModel);

            return MoveNode(pathModel, nodeIndex, anchor, isJunction);
        }

        /// <summary>Returns the node's role after validating the complete supported passing-branch topology.</summary>
        public static bool TryGetPassingBranchNodeRole(PathModel pathModel, int nodeIndex, out PassingBranchNodeRole role, out string failure)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            role = PassingBranchNodeRole.None;
            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
            {
                failure = $"Node index {nodeIndex} is out of range.";
                return false;
            }

            ImmutableArray<int> starts = Enumerable.Range(0, nodes.Length).Where(index => nodes[index].NextMainNode >= 0 && nodes[index].NextSidingNode >= 0).ToImmutableArray();

            if (starts.Length != 1)
            {
                failure = "Exactly one supported passing branch is required.";
                return false;
            }

            if (!TryGetSinglePassingBranch(nodes, starts[0], out int rejoinNodeIndex, out ImmutableArray<int> interiorNodes, out failure))
                return false;

            role = nodeIndex == starts[0]
                ? PassingBranchNodeRole.BranchStart
                : nodeIndex == rejoinNodeIndex
                    ? PassingBranchNodeRole.BranchRejoin
                    : interiorNodes.Contains(nodeIndex)
                        ? PassingBranchNodeRole.BranchInterior
                        : PassingBranchNodeRole.MainRoute;
            failure = null;

            return true;
        }

        /// <summary>Removes the supported single-level passing branch and its interior anchors.</summary>
        public static PathEditResult RemovePassingBranch(PathModel pathModel, int startNodeIndex)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (!TryGetSinglePassingBranch(nodes, startNodeIndex, out _, out ImmutableArray<int> interiorNodes, out string failure))
                return PathEditResult.Failed(failure, pathModel);

            ImmutableArray<PathNode> updatedNodes = nodes.SetItem(startNodeIndex, nodes[startNodeIndex] with { NextSidingNode = -1 });

            foreach (int interiorIndex in interiorNodes.OrderByDescending(index => index))
                updatedNodes = RemoveNodeAt(updatedNodes, interiorIndex);

            return PathEditResult.Succeeded($"Removed passing branch from node {startNodeIndex}.", pathModel with { PathNodes = updatedNodes }, interiorNodes.Add(startNodeIndex));
        }

        private static PathNode CreateViaNode(PathNode anchor, bool junction, int nextMainNode)
        {
            return new PathNode(anchor.Location)
            {
                NodeType = junction ? PathNodeType.Junction : PathNodeType.Via,
                NodeIndex = anchor.NodeIndex,
                NextMainNode = nextMainNode,
                NextSidingNode = -1,
            };
        }

        private static PathNode CreateSidingViaNode(PathNode anchor, bool junction, int nextSidingNode)
        {
            return new PathNode(anchor.Location)
            {
                NodeType = junction ? PathNodeType.Junction : PathNodeType.Via,
                NodeIndex = anchor.NodeIndex,
                NextMainNode = -1,
                NextSidingNode = nextSidingNode,
            };
        }

        private static bool IsEligiblePassingBranch(ImmutableArray<PathNode> nodes, ImmutableArray<int> mainRoute, int startNodeIndex, int rejoinNodeIndex, out string failure)
        {
            int startPosition = mainRoute.IndexOf(startNodeIndex);
            int rejoinPosition = mainRoute.IndexOf(rejoinNodeIndex);

            if (startPosition < 0 || rejoinPosition < 0)
            {
                failure = "A passing branch must start and rejoin on authored main-route nodes.";
                return false;
            }

            if (rejoinPosition <= startPosition)
            {
                failure = "A passing branch must rejoin a later main-route node.";
                return false;
            }

            if (nodes.Any(node => node.NextSidingNode >= 0))
            {
                failure = "Nested or overlapping passing branches are not supported.";
                return false;
            }

            if (nodes.Length != mainRoute.Length)
            {
                failure = "A passing branch cannot be added while disconnected authored nodes exist.";
                return false;
            }

            failure = null;
            
            return true;
        }

        private static bool TryGetSinglePassingBranch(ImmutableArray<PathNode> nodes, int startNodeIndex, out int rejoinNodeIndex, out ImmutableArray<int> interiorNodes, out string failure)
        {
            rejoinNodeIndex = -1;
            interiorNodes = ImmutableArray<int>.Empty;

            if (!TryGetMainRouteNodes(nodes, out ImmutableArray<int> mainRoute, out failure))
                return false;
            
            if (startNodeIndex < 0 || startNodeIndex >= nodes.Length || nodes[startNodeIndex].NextSidingNode < 0 || !mainRoute.Contains(startNodeIndex))
            {
                failure = $"Node {startNodeIndex} does not start a passing branch.";
                return false;
            }
            
            ImmutableArray<int> branchStarts = Enumerable.Range(0, nodes.Length).Where(index => nodes[index].NextMainNode >= 0 && nodes[index].NextSidingNode >= 0).ToImmutableArray();
            
            if (branchStarts.Length != 1 || branchStarts[0] != startNodeIndex)
            {
                failure = "The path does not contain exactly one supported passing-branch start.";
                return false;
            }

            ImmutableArray<int>.Builder interior = ImmutableArray.CreateBuilder<int>();
            HashSet<int> visited = new HashSet<int>();
            int current = nodes[startNodeIndex].NextSidingNode;
            
            while (!mainRoute.Contains(current))
            {
                if (current < 0 || current >= nodes.Length || !visited.Add(current) || nodes[current].NextMainNode >= 0 || nodes[current].NextSidingNode < 0)
                {
                    failure = "The passing branch is unresolved, nested, or does not rejoin the main route.";
                    return false;
                }
                interior.Add(current);
                current = nodes[current].NextSidingNode;
            }
            
            if (mainRoute.IndexOf(current) <= mainRoute.IndexOf(startNodeIndex))
            {
                failure = "The passing branch does not rejoin a later main-route node.";
                return false;
            }
            
            rejoinNodeIndex = current;
            interiorNodes = interior.ToImmutable();
            ImmutableHashSet<int> allowedNodes = mainRoute.ToImmutableHashSet().Union(interiorNodes);
            
            for (int index = 0; index < nodes.Length; index++)
            {
                if (!allowedNodes.Contains(index))
                {
                    failure = "The path contains disconnected nodes outside the passing branch.";
                    return false;
                }
                if (nodes[index].NextSidingNode >= 0 && index != startNodeIndex && !interiorNodes.Contains(index))
                {
                    failure = "The path contains an overlapping or nested passing-branch link.";
                    return false;
                }
            }
            
            failure = null;
            return true;
        }

        private static bool TryGetPassingBranchInteriorNodes(ImmutableArray<PathNode> nodes, out ImmutableArray<int> interiorNodes, out string failure)
        {
            ImmutableArray<int> starts = Enumerable.Range(0, nodes.Length)
                .Where(index => nodes[index].NextMainNode >= 0 && nodes[index].NextSidingNode >= 0).ToImmutableArray();

            if (starts.Length != 1)
            {
                interiorNodes = ImmutableArray<int>.Empty;
                failure = "Exactly one supported passing branch is required.";
                return false;
            }

            return TryGetSinglePassingBranch(nodes, starts[0], out _, out interiorNodes, out failure);
        }

        private static bool TryGetMainRouteNodes(ImmutableArray<PathNode> nodes, out ImmutableArray<int> mainRoute, out string failure)
        {
            for (int index = 0; index < nodes.Length; index++)
            {
                if (nodes[index].NextMainNode < -1 || nodes[index].NextMainNode >= nodes.Length)
                {
                    mainRoute = ImmutableArray<int>.Empty;
                    failure = $"Node {index} has invalid main link {nodes[index].NextMainNode}.";
                    return false;
                }

                if (nodes[index].NextSidingNode < -1 || nodes[index].NextSidingNode >= nodes.Length)
                {
                    mainRoute = ImmutableArray<int>.Empty;
                    failure = $"Node {index} has invalid siding link {nodes[index].NextSidingNode}.";
                    return false;
                }
            }

            int startIndex = IndexOfNodeType(nodes, PathNodeType.Start);
            
            if (startIndex < 0)
            {
                mainRoute = ImmutableArray<int>.Empty;
                failure = "A passing branch requires a main-route start node.";
                return false;
            }
            
            ImmutableArray<int>.Builder route = ImmutableArray.CreateBuilder<int>();
            HashSet<int> visited = new HashSet<int>();
            int current = startIndex;
            
            while (current >= 0)
            {
                if (current >= nodes.Length || !visited.Add(current))
                {
                    mainRoute = ImmutableArray<int>.Empty;
                    failure = "The main route contains an invalid link or cycle.";
                    return false;
                }
            
                route.Add(current);
                current = nodes[current].NextMainNode;
            }

            mainRoute = route.ToImmutable();
            failure = null;
            return true;
        }

        // Re-targets a single absolute link index after a node is inserted at insertIndex.
        private static int ShiftLink(int link, int insertIndex)
        {
            return link >= insertIndex ? link + 1 : link;
        }

        /// <summary>
        /// Marks the node at <paramref name="nodeIndex"/> as a wait point and stores the given wait time in
        /// seconds. Basic durations must be between 1 and 29999; larger values may encode legacy AI operations.
        /// Fails for an out-of-range index or duration, a junction node, or a start/end node.
        /// Also refuses combined wait/reversal events, passing-branch interiors, and unclassifiable passing topology.
        /// Other nodes are not checked for Simulator event compatibility.
        /// </summary>
        public static PathEditResult SetWaitPoint(PathModel pathModel, int nodeIndex, int waitTimeSeconds)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            if (waitTimeSeconds <= 0 || waitTimeSeconds > MaximumBasicWaitTimeSeconds)
                return PathEditResult.Failed(basicWaitTimeOutOfRangeMessage, PathEditorMessageKind.BasicWaitTimeOutOfRange, pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (!CanAnnotateNode(nodes, nodeIndex, "wait point", out string failure))
                return PathEditResult.Failed(failure, pathModel);

            PathNode node = nodes[nodeIndex] with
            {
                NodeType = nodes[nodeIndex].NodeType | PathNodeType.Wait,
                WaitInfo = new PathNodeWaitInfo { WaitTime = waitTimeSeconds },
            };

            return !TryValidateNodeEvents(pathModel, nodeIndex, node, out failure)
                ? PathEditResult.Failed(failure, pathModel)
                : PathEditResult.Succeeded($"Set wait point of {waitTimeSeconds}s on node {nodeIndex}.", pathModel with { PathNodes = nodes.SetItem(nodeIndex, node) }, ImmutableArray.Create(nodeIndex));
        }

        /// <summary>
        /// Clears the wait point marker and wait information from the node at <paramref name="nodeIndex"/>. Fails
        /// for an out-of-range index or when neither the marker nor a wait information object exists. Explicit
        /// removal also clears zero-valued wait information and does not require annotation eligibility.
        /// </summary>
        public static PathEditResult ClearWaitPoint(PathModel pathModel, int nodeIndex)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {nodeIndex} is out of range.", pathModel);

            if (!nodes[nodeIndex].NodeType.Includes(PathNodeType.Wait) && nodes[nodeIndex].WaitInfo == null)
                return PathEditResult.Failed($"Node {nodeIndex} is not a wait point.", pathModel);

            PathNode node = nodes[nodeIndex] with
            {
                NodeType = WithoutMarker(nodes[nodeIndex].NodeType, PathNodeType.Wait),
                WaitInfo = null,
            };

            return PathEditResult.Succeeded($"Cleared wait point on node {nodeIndex}.", pathModel with { PathNodes = nodes.SetItem(nodeIndex, node) }, ImmutableArray.Create(nodeIndex));
        }

        /// <summary>
        /// Marks the node at <paramref name="nodeIndex"/> as a reversal point. Fails for an out-of-range index, a
        /// junction node, or a start/end node. Also refuses combined wait/reversal events, passing-branch interiors,
        /// and unclassifiable passing topology. Other nodes are not checked for Simulator event compatibility.
        /// </summary>
        public static PathEditResult SetReversalPoint(PathModel pathModel, int nodeIndex)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (!CanAnnotateNode(nodes, nodeIndex, "reversal point", out string failure))
                return PathEditResult.Failed(failure, pathModel);

            PathNode node = nodes[nodeIndex] with { NodeType = nodes[nodeIndex].NodeType | PathNodeType.Reversal };

            return !TryValidateNodeEvents(pathModel, nodeIndex, node, out failure)
                ? PathEditResult.Failed(failure, pathModel)
                : PathEditResult.Succeeded($"Set reversal point on node {nodeIndex}.", pathModel with { PathNodes = nodes.SetItem(nodeIndex, node) }, ImmutableArray.Create(nodeIndex));
        }

        /// <summary>
        /// Clears the reversal point marker from the node at <paramref name="nodeIndex"/>. Fails for an
        /// out-of-range index or when the node is not a reversal point. Does not require annotation eligibility
        /// or Simulator event compatibility, so imported annotations remain removable.
        /// </summary>
        public static PathEditResult ClearReversalPoint(PathModel pathModel, int nodeIndex)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {nodeIndex} is out of range.", pathModel);

            if ((nodes[nodeIndex].NodeType & PathNodeType.Reversal) != PathNodeType.Reversal)
                return PathEditResult.Failed($"Node {nodeIndex} is not a reversal point.", pathModel);

            PathNode node = nodes[nodeIndex] with { NodeType = WithoutMarker(nodes[nodeIndex].NodeType, PathNodeType.Reversal) };

            return PathEditResult.Succeeded($"Cleared reversal point on node {nodeIndex}.", pathModel with { PathNodes = nodes.SetItem(nodeIndex, node) }, ImmutableArray.Create(nodeIndex));
        }

        // Wait and reversal markers describe what the train does while running along a track point. They are not
        // meaningful on the route termini or on a junction, where the resolver owns the node semantics.
        private static bool CanAnnotateNode(ImmutableArray<PathNode> nodes, int nodeIndex, string markerName, out string failure)
        {
            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
            {
                failure = $"Node index {nodeIndex} is out of range.";
                return false;
            }

            PathNodeType nodeType = nodes[nodeIndex].NodeType;
            if ((nodeType & (PathNodeType.Start | PathNodeType.End)) != PathNodeType.None)
            {
                failure = $"Node {nodeIndex} is a start or end node and cannot be a {markerName}.";
                return false;
            }
            if (nodeType.Includes(PathNodeType.Junction))
            {
                failure = $"Node {nodeIndex} is a junction node and cannot be a {markerName}.";
                return false;
            }

            failure = null;
            return true;
        }

        private static bool TryValidateNodeEvents(PathModel pathModel, int nodeIndex, PathNode node, out string failure)
        {
            failure = null;
            if ((node.NodeType & (PathNodeType.Wait | PathNodeType.Reversal)) == PathNodeType.None
                && (node.WaitInfo?.WaitTime ?? 0) == 0)
                return true;

            bool isPassingInterior = false;
            if (Nodes(pathModel).Any(pathNode => pathNode.NextSidingNode != -1))
            {
                if (!TryGetPassingBranchNodeRole(pathModel, nodeIndex, out PassingBranchNodeRole role, out failure))
                    return false;

                isPassingInterior = role == PassingBranchNodeRole.BranchInterior;
            }

            // Setters guard endpoints separately; moving an imported first-node event must remain a local repair.
            PathRouteDiagnostic diagnostic = PathSimulatorEventCompatibility.ValidateNode(node, nodeIndex, false, isPassingInterior);
            failure = diagnostic?.Message;
            return diagnostic == null;
        }

        // Drops a marker flag, falling back to a plain via node when nothing else remains.
        private static PathNodeType WithoutMarker(PathNodeType nodeType, PathNodeType marker)
        {
            PathNodeType remaining = nodeType & ~marker;
            return remaining == PathNodeType.None ? PathNodeType.Via : remaining;
        }

        /// <summary>
        /// Moves the node at <paramref name="nodeIndex"/> to a new track anchor, preserving its path links and
        /// wait metadata. Start/end/wait/reversal intent is preserved, while junction/via classification
        /// is recalculated from <paramref name="isJunction"/>. Refuses combined events, events on the destination
        /// junction, and passing-interior events or nonzero wait payloads. Event-bearing moves require classifiable
        /// passing topology, but other nodes and traversal-wide event restrictions do not gate local repairs.
        /// </summary>
        public static PathEditResult MoveNode(PathModel pathModel, int nodeIndex, PathNode replacementAnchor, bool isJunction)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(replacementAnchor);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {nodeIndex} is out of range.", pathModel);

            PathNode original = nodes[nodeIndex];
            PathNodeType nodeType = original.NodeType & (PathNodeType.Start | PathNodeType.End | PathNodeType.Wait | PathNodeType.Reversal | PathNodeType.Invalid);
            nodeType &= ~PathNodeType.Invalid;
            nodeType |= isJunction ? PathNodeType.Junction : PathNodeType.Via;

            PathNode movedNode = new PathNode(replacementAnchor.Location)
            {
                NodeType = nodeType,
                NodeIndex = replacementAnchor.NodeIndex,
                NextMainNode = original.NextMainNode,
                NextSidingNode = original.NextSidingNode,
                WaitInfo = original.WaitInfo,
            };

            return !TryValidateNodeEvents(pathModel, nodeIndex, movedNode, out string failure)
                ? PathEditResult.Failed(failure, pathModel)
                : PathEditResult.Succeeded($"Moved node {nodeIndex}.",
                pathModel with { PathNodes = nodes.SetItem(nodeIndex, movedNode) },
                ImmutableArray.Create(nodeIndex));
        }

        /// <summary>
        /// Safely repairs the node at <paramref name="nodeIndex"/> when the repair is unambiguous. The first
        /// supported repair converts a node incorrectly marked as a junction into a track point when its stored
        /// location lies on exactly one vector section and no actual junction exists there. Event compatibility
        /// is checked at the prospective destination without requiring compatibility of the complete imported path.
        /// </summary>
        public static PathEditResult RepairNode(PathModel pathModel, int nodeIndex, TrackWorld trackWorld)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            if (trackWorld == null)
                return PathEditResult.Failed("Cannot repair node because no track world is available.", pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {nodeIndex} is out of range.", pathModel);

            PathNode node = nodes[nodeIndex];
            JunctionNode exactJunction = trackWorld.JunctionAt(node.Location);
            if (exactJunction != null)
                return PathEditResult.Failed($"Node {nodeIndex} is already located on a junction.", pathModel);

            JunctionNode[] nearbyJunctions = FindNearbyJunctions(trackWorld, node.Location);
            if (nearbyJunctions.Length == 1)
                return MoveNodeToJunction(pathModel, nodeIndex, nearbyJunctions[0]);
            
            if (nearbyJunctions.Length > 1)
                return PathEditResult.Failed($"Node {nodeIndex} has multiple nearby junction repairs; choose the intended junction manually.", pathModel);

            if ((node.NodeType & PathNodeType.Junction) != PathNodeType.Junction)
                return PathEditResult.Failed($"Node {nodeIndex} does not have a supported automatic repair.", pathModel);

            VectorSectionNode[] candidates = trackWorld.TrackDatabase?.TrackNodes
                .OfType<VectorNode>()
                .Select(vectorNode => trackWorld.SectionAt(vectorNode, node.Location))
                .Where(section => section != null)
                .Take(2)
                .ToArray() ?? Array.Empty<VectorSectionNode>();
            int trackNodeIndex;
            if (candidates.Length == 0)
            {
                TrackDistanceDiagnostic nearestTrackDistance = trackWorld.NearestTrackDistance(PointD.FromWorldLocation(node.Location));
                if (nearestTrackDistance == null || nearestTrackDistance.DistanceMeters > 1.0)
                    return PathEditResult.Failed($"Node {nodeIndex} is not on a valid track section.", pathModel);

                trackNodeIndex = nearestTrackDistance.TrackNodeIndex;
            }
            else
            {
                if (candidates.Length > 1)
                    return PathEditResult.Failed($"Node {nodeIndex} has multiple possible track repairs; choose the intended track manually.", pathModel);
                if (!trackWorld.SectionGeometry.TryGetValue(candidates[0], out SectionGeometry geometry))
                    return PathEditResult.Failed($"Node {nodeIndex} track section has no resolved geometry.", pathModel);

                trackNodeIndex = geometry.Node.NodeIndex;
            }

            PathNode replacementAnchor = new PathNode(node.Location)
            {
                NodeIndex = trackNodeIndex,
            };

            PathEditResult result = MoveNode(pathModel, nodeIndex, replacementAnchor, false);
            return result.Success
                ? PathEditResult.Succeeded($"Repaired node {nodeIndex} as a track point.", result.PathModel, result.ChangedNodeIndexes)
                : result;
        }

        private static JunctionNode[] FindNearbyJunctions(TrackWorld trackWorld, in WorldLocation location)
        {
            WorldLocation targetLocation = location;
            double maxDistanceSquared = NearbyJunctionRepairDistanceMeters * NearbyJunctionRepairDistanceMeters;
            return trackWorld.TrackDatabase?.TrackNodes
                .OfType<JunctionNode>()
                .Where(junction => WorldLocation.GetDistanceSquared2D(junction.Location, targetLocation) <= maxDistanceSquared)
                .OrderBy(junction => WorldLocation.GetDistanceSquared2D(junction.Location, targetLocation))
                .Take(2)
                .ToArray() ?? Array.Empty<JunctionNode>();
        }

        private static PathEditResult MoveNodeToJunction(PathModel pathModel, int nodeIndex, JunctionNode junctionNode)
        {
            PathNode replacementAnchor = new PathNode(junctionNode.Location)
            {
                NodeIndex = junctionNode.NodeIndex,
            };

            PathEditResult result = MoveNode(pathModel, nodeIndex, replacementAnchor, true);
            return result.Success
                ? PathEditResult.Succeeded($"Repaired node {nodeIndex} by snapping it to junction {junctionNode.NodeIndex}.", result.PathModel, result.ChangedNodeIndexes)
                : result;
        }

        private static PathNode CreateEndpointNode(PathNode anchor, PathNode existingNode, PathNodeType endpointType,
            bool isJunction, int nextMainNode, int nextSidingNode)
        {
            PathNodeType preservedIntent = existingNode?.NodeType & (PathNodeType.Wait | PathNodeType.Reversal) ?? PathNodeType.None;
            return new PathNode(anchor.Location)
            {
                NodeType = endpointType | preservedIntent | (isJunction ? PathNodeType.Junction : PathNodeType.None),
                NodeIndex = anchor.NodeIndex,
                NextMainNode = nextMainNode,
                NextSidingNode = nextSidingNode,
                WaitInfo = existingNode?.WaitInfo,
            };
        }

        private static bool TryFindMainTail(ImmutableArray<PathNode> nodes, int startIndex, out int tailIndex, out string failure)
        {
            bool[] visited = new bool[nodes.Length];
            int currentIndex = startIndex;
            while (true)
            {
                if (visited[currentIndex])
                {
                    tailIndex = -1;
                    failure = $"Cannot set an end anchor because the main path contains a cycle at node {currentIndex}.";
                    return false;
                }

                visited[currentIndex] = true;
                int nextIndex = nodes[currentIndex].NextMainNode;
                if (nextIndex == -1)
                {
                    tailIndex = currentIndex;
                    failure = null;
                    return true;
                }
                if (nextIndex < 0 || nextIndex >= nodes.Length)
                {
                    tailIndex = -1;
                    failure = $"Cannot set an end anchor because node {currentIndex} has an out-of-range main link {nextIndex}.";
                    return false;
                }

                currentIndex = nextIndex;
            }
        }

        // Returns the authored nodes, normalizing a default ImmutableArray to empty.
        private static ImmutableArray<PathNode> Nodes(PathModel pathModel)
        {
            return pathModel.PathNodes.IsDefault ? ImmutableArray<PathNode>.Empty : pathModel.PathNodes;
        }

        // Removes the node at removedIndex and re-indexes every surviving link: a link to the removed node
        // becomes -1 (broken), a link to a node after it is decremented by one, others are unchanged.
        private static ImmutableArray<PathNode> RemoveNodeAt(ImmutableArray<PathNode> nodes, int removedIndex)
        {
            ImmutableArray<PathNode>.Builder builder = ImmutableArray.CreateBuilder<PathNode>(nodes.Length - 1);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (i == removedIndex)
                    continue;
                builder.Add(nodes[i] with
                {
                    NextMainNode = AdjustLink(nodes[i].NextMainNode, removedIndex),
                    NextSidingNode = AdjustLink(nodes[i].NextSidingNode, removedIndex),
                });
            }
            return builder.ToImmutable();
        }

        // Re-targets a single absolute link index after the node at removedIndex is dropped.
        private static int AdjustLink(int link, int removedIndex)
        {
            if (link < 0 || link == removedIndex)
                return -1;
            return link > removedIndex ? link - 1 : link;
        }

        // Returns the index of the first node carrying the given flag, or -1. End is searched from the tail
        // (the authored end is the last End-flagged node), all other flags from the head.
        private static int IndexOfNodeType(ImmutableArray<PathNode> nodes, PathNodeType nodeType)
        {
            if (nodeType.Includes(PathNodeType.End))
            {
                for (int i = nodes.Length - 1; i >= 0; i--)
                {
                    if ((nodes[i].NodeType & nodeType) == nodeType)
                        return i;
                }
            }
            else
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    if ((nodes[i].NodeType & nodeType) == nodeType)
                        return i;
                }
            }
            return -1;
        }
    }
}
