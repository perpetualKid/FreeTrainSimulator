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
    /// Resolves authored path models into deterministic, UI-neutral route descriptions and diagnostics.
    /// </summary>
    public static class PathRouteResolver
    {
        private const double CostEpsilon = 1e-6;

        /// <summary>
        /// Resolves and validates a path model.
        /// </summary>
        public static PathRouteResolution Resolve(PathModel pathModel, TrackWorld trackWorld)
        {
            return Resolve(pathModel, trackWorld, PathRouteResolverOptions.Default, CancellationToken.None);
        }

        /// <summary>
        /// Resolves and validates a path model.
        /// </summary>
        public static PathRouteResolution Resolve(PathModel pathModel, TrackWorld trackWorld, PathRouteResolverOptions options)
        {
            return Resolve(pathModel, trackWorld, options, CancellationToken.None);
        }

        /// <summary>
        /// Resolves and validates a path model.
        /// </summary>
        public static PathRouteResolution Resolve(PathModel pathModel, TrackWorld trackWorld, CancellationToken cancellationToken)
        {
            return Resolve(pathModel, trackWorld, PathRouteResolverOptions.Default, cancellationToken);
        }

        /// <summary>
        /// Resolves and validates a path model.
        /// </summary>
        public static PathRouteResolution Resolve(PathModel pathModel, TrackWorld trackWorld, PathRouteResolverOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(options);

            List<PathRouteDiagnostic> diagnostics = new List<PathRouteDiagnostic>();
            ImmutableArray<PathNode> pathNodes = pathModel.PathNodes.IsDefault ? ImmutableArray<PathNode>.Empty : pathModel.PathNodes;
            if (pathNodes.IsEmpty)
            {
                diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Fatal, PathRouteDiagnosticCode.EmptyPath,
                    "Path has no authored nodes.", "Add a start node and an end node."));

                return new PathRouteResolution(null, diagnostics.ToImmutableArray());
            }

            cancellationToken.ThrowIfCancellationRequested();

            int startNodeIndex = FindFirstNodeOfType(pathNodes, PathNodeType.Start);
            int endNodeIndex = FindLastNodeOfType(pathNodes, PathNodeType.End);

            if (startNodeIndex < 0)
            {
                diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Fatal, PathRouteDiagnosticCode.MissingStartNode,
                    "Path has no start node.", "Mark one authored node as the start node."));
            }

            if (endNodeIndex < 0)
            {
                diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Fatal, PathRouteDiagnosticCode.MissingEndNode,
                    "Path has no end node.", "Mark one authored node as the end node."));
            }

            ValidateLinks(pathNodes, diagnostics);
            ImmutableHashSet<int> reachableNodes = startNodeIndex >= 0
                ? FindReachableNodes(pathNodes, startNodeIndex, diagnostics, cancellationToken)
                : ImmutableHashSet<int>.Empty;
            ReportUnreachableNodes(pathNodes, reachableNodes, diagnostics);

            ImmutableArray<PathRouteAnchor> anchors = ResolveAnchors(pathNodes, trackWorld, diagnostics, cancellationToken);
            ResolvedPathRoute mainRoute = startNodeIndex >= 0
                ? BuildRoute(PathRouteBranchKind.Main, pathNodes, anchors, trackWorld, options, diagnostics, startNodeIndex, static node => node.NextMainNode, cancellationToken)
                : null;
            ValidateMainRouteReachesEnd(mainRoute, endNodeIndex, diagnostics);
            ImmutableArray<ResolvedPathRoute> passingRoutes = options.ResolvePassingBranches
                ? BuildPassingRoutes(pathNodes, anchors, trackWorld, options, diagnostics, cancellationToken)
                : ImmutableArray<ResolvedPathRoute>.Empty;
            ValidatePassingBranchRejoins(mainRoute, passingRoutes, diagnostics);

            return new PathRouteResolution(mainRoute, passingRoutes, anchors, diagnostics.ToImmutableArray());
        }

        private static void ValidateMainRouteReachesEnd(ResolvedPathRoute mainRoute, int endNodeIndex, List<PathRouteDiagnostic> diagnostics)
        {
            if (mainRoute == null || endNodeIndex < 0 || mainRoute.EndNodeIndex == endNodeIndex)
                return;

            diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Fatal, PathRouteDiagnosticCode.MainRouteDoesNotReachEnd,
                $"Main path ends at node {mainRoute.EndNodeIndex} before reaching authored end node {endNodeIndex}.",
                mainRoute.EndNodeIndex, mainRoute.EndNodeIndex, endNodeIndex,
                "Reconnect the main path so it reaches the authored end node."));
        }

        private static int FindFirstNodeOfType(ImmutableArray<PathNode> pathNodes, PathNodeType nodeType)
        {
            for (int i = 0; i < pathNodes.Length; i++)
            {
                if ((pathNodes[i].NodeType & nodeType) == nodeType)
                    return i;
            }
            return -1;
        }

        private static int FindLastNodeOfType(ImmutableArray<PathNode> pathNodes, PathNodeType nodeType)
        {
            for (int i = pathNodes.Length - 1; i >= 0; i--)
            {
                if ((pathNodes[i].NodeType & nodeType) == nodeType)
                    return i;
            }
            return -1;
        }

        private static void ValidateLinks(ImmutableArray<PathNode> pathNodes, List<PathRouteDiagnostic> diagnostics)
        {
            for (int i = 0; i < pathNodes.Length; i++)
            {
                PathNode node = pathNodes[i];
                if (node.NextMainNode < -1 || node.NextMainNode >= pathNodes.Length)
                {
                    diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Fatal, PathRouteDiagnosticCode.InvalidMainLink,
                        $"Path node {i} has invalid main link {node.NextMainNode}.", i, i, node.NextMainNode,
                        "Repair or remove the invalid main path link."));
                }

                if (node.NextSidingNode < -1 || node.NextSidingNode >= pathNodes.Length)
                {
                    diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Error, PathRouteDiagnosticCode.InvalidSidingLink,
                        $"Path node {i} has invalid siding link {node.NextSidingNode}.", i, i, node.NextSidingNode,
                        "Repair or remove the invalid passing path link."));
                }
            }
        }

        private static ImmutableHashSet<int> FindReachableNodes(
            ImmutableArray<PathNode> pathNodes,
            int startNodeIndex,
            List<PathRouteDiagnostic> diagnostics,
            CancellationToken cancellationToken)
        {
            HashSet<int> reachable = new HashSet<int>();
            HashSet<int> active = new HashSet<int>();
            HashSet<(int FromNodeIndex, int ToNodeIndex)> reportedCycles = new HashSet<(int FromNodeIndex, int ToNodeIndex)>();
            Visit(startNodeIndex);
            return reachable.ToImmutableHashSet();

            void Visit(int nodeIndex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (nodeIndex < 0 || nodeIndex >= pathNodes.Length)
                    return;
                if (active.Contains(nodeIndex))
                {
                    diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.UnsupportedGraphCycle,
                        $"Path graph contains a cycle at node {nodeIndex}.", nodeIndex,
                        "Add explicit via nodes or repair links if this cycle is not intentional."));
                    return;
                }
                if (!reachable.Add(nodeIndex))
                    return;

                active.Add(nodeIndex);
                PathNode node = pathNodes[nodeIndex];
                VisitNext(nodeIndex, node.NextMainNode, "main", "Repair the main path links or add explicit via nodes if the loop is intentional.");
                VisitNext(nodeIndex, node.NextSidingNode, "siding", "Repair the passing path links or add explicit via nodes if the loop is intentional.");
                active.Remove(nodeIndex);
            }

            void VisitNext(int fromNodeIndex, int toNodeIndex, string linkKind, string suggestedAction)
            {
                if (!IsInRange(toNodeIndex, pathNodes.Length))
                    return;
                if (active.Contains(toNodeIndex))
                {
                    if (reportedCycles.Add((fromNodeIndex, toNodeIndex)))
                    {
                        diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.UnsupportedGraphCycle,
                            $"Path graph contains a {linkKind} link cycle from node {fromNodeIndex} to node {toNodeIndex}.",
                            fromNodeIndex, toNodeIndex, suggestedAction));
                    }
                    return;
                }

                Visit(toNodeIndex);
            }
        }

        private static void ReportUnreachableNodes(ImmutableArray<PathNode> pathNodes, ImmutableHashSet<int> reachableNodes, List<PathRouteDiagnostic> diagnostics)
        {
            if (reachableNodes.IsEmpty)
                return;

            for (int i = 0; i < pathNodes.Length; i++)
            {
                if (!reachableNodes.Contains(i))
                {
                    diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.UnreachableNode,
                        $"Path node {i} is not reachable from the start node.", i,
                        "Connect the node to the main or passing path, or remove it."));
                }
            }
        }

        private static ImmutableArray<PathRouteAnchor> ResolveAnchors(
            ImmutableArray<PathNode> pathNodes,
            TrackWorld trackWorld,
            List<PathRouteDiagnostic> diagnostics,
            CancellationToken cancellationToken)
        {
            ImmutableArray<PathRouteAnchor>.Builder anchors = ImmutableArray.CreateBuilder<PathRouteAnchor>(pathNodes.Length);
            for (int i = 0; i < pathNodes.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                PathNode node = pathNodes[i];
                PathRouteAnchor anchor = ResolveAnchor(i, node, trackWorld, diagnostics);
                anchors.Add(anchor);
            }
            return anchors.MoveToImmutable();
        }

        private static PathRouteAnchor ResolveAnchor(int authoredNodeIndex, PathNode node, TrackWorld trackWorld, List<PathRouteDiagnostic> diagnostics)
        {
            if (trackWorld == null)
                return new PathRouteAnchor(authoredNodeIndex, node.Location, node.NodeType);

            if ((node.NodeType & PathNodeType.Junction) == PathNodeType.Junction && trackWorld.JunctionAt(node.Location) == null)
            {
                diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Error, PathRouteDiagnosticCode.NoJunctionNode,
                    $"Path node {authoredNodeIndex} is marked as a junction, but no junction exists at its stored location.", authoredNodeIndex,
                    "Move the node to a junction or convert it to a track point."));
            }

            int trackNodeIndex = ResolveTrackNodeIndex(authoredNodeIndex, node, trackWorld, diagnostics, out int trackVectorSectionIndex, out bool ambiguous);
            if (trackNodeIndex < 0)
            {
                diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Error, PathRouteDiagnosticCode.AnchorNotOnTrack,
                    $"Path node {authoredNodeIndex} could not be resolved to track.", authoredNodeIndex,
                    "Move the path node onto a valid track segment or junction."));
            }
            else if (ambiguous)
            {
                diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.AmbiguousAnchor,
                    $"Path node {authoredNodeIndex} resolves to multiple plausible track anchors.", authoredNodeIndex,
                    "Add a route-choice node or choose the intended track anchor."));
            }

            return new PathRouteAnchor(authoredNodeIndex, node.Location, node.NodeType, trackNodeIndex, trackVectorSectionIndex);
        }

        private static int ResolveTrackNodeIndex(int authoredNodeIndex, PathNode node, TrackWorld trackWorld, List<PathRouteDiagnostic> diagnostics,
            out int trackVectorSectionIndex, out bool ambiguous)
        {
            trackVectorSectionIndex = -1;
            ambiguous = false;

            TrackDatabase trackDatabase = trackWorld.TrackDatabase;
            if (trackDatabase == null)
                return -1;

            if (node.NodeIndex > 0 && IsInRange(node.NodeIndex, trackDatabase.TrackNodes.Length) && trackDatabase.TrackNodes[node.NodeIndex] != null)
            {
                if (trackWorld.SectionGeometry.Count > 0)
                {
                    int locationTrackNodeIndex = ResolveTrackNodeIndexByLocation(node, trackWorld, out int locationTrackVectorSectionIndex, out bool locationAmbiguous);
                    bool anchorContainsLocation = StoredAnchorContainsLocation(trackDatabase.TrackNodes[node.NodeIndex], node, trackWorld, out int storedTrackVectorSectionIndex);
                    if (anchorContainsLocation)
                    {
                        trackVectorSectionIndex = storedTrackVectorSectionIndex;
                        ambiguous = locationAmbiguous;
                    }
                    else
                    {
                        if (locationTrackNodeIndex >= 0)
                        {
                            trackVectorSectionIndex = locationTrackNodeIndex == node.NodeIndex ? locationTrackVectorSectionIndex : -1;
                            ambiguous = locationAmbiguous;
                        }

                        diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.AnchorLocationMismatch,
                            FormatAnchorLocationMismatchMessage(authoredNodeIndex, node.NodeIndex, locationTrackNodeIndex),
                            authoredNodeIndex, "Review the path node location and stored track anchor before saving or repairing the path."));
                    }
                }

                return node.NodeIndex;
            }

            return ResolveTrackNodeIndexByLocation(node, trackWorld, out trackVectorSectionIndex, out ambiguous);
        }

        private static bool StoredAnchorContainsLocation(TrackNodeBase trackNode, PathNode node, TrackWorld trackWorld, out int trackVectorSectionIndex)
        {
            trackVectorSectionIndex = -1;

            if (trackNode is VectorNode vectorNode)
            {
                VectorSectionNode section = trackWorld.SectionAt(vectorNode, node.Location);
                if (section == null || !trackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry geometry))
                    return false;

                trackVectorSectionIndex = geometry.SectionIndex;
                return true;
            }

            return true;
        }

        private static string FormatAnchorLocationMismatchMessage(int authoredNodeIndex, int storedTrackNodeIndex, int locationTrackNodeIndex)
        {
            return locationTrackNodeIndex >= 0
                ? $"Path node {authoredNodeIndex} has track anchor {storedTrackNodeIndex}, but its stored location resolves to track node {locationTrackNodeIndex}."
                : $"Path node {authoredNodeIndex} has track anchor {storedTrackNodeIndex}, but its stored location is not on that track node.";
        }

        private static int ResolveTrackNodeIndexByLocation(PathNode node, TrackWorld trackWorld, out int trackVectorSectionIndex, out bool ambiguous)
        {
            trackVectorSectionIndex = -1;
            ambiguous = false;

            if ((node.NodeType & PathNodeType.Junction) == PathNodeType.Junction)
            {
                JunctionNode junctionNode = trackWorld.JunctionAt(node.Location);
                if (junctionNode != null)
                    return junctionNode.NodeIndex;
            }

            if ((node.NodeType & PathNodeType.End) == PathNodeType.End)
            {
                EndNode endNode = trackWorld.EndNodeAt(node.Location);
                if (endNode != null)
                    return endNode.NodeIndex;
            }

            VectorSectionNode section = trackWorld.SectionAt(node.Location);
            if (section == null)
                return -1;

            if (!trackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry geometry))
                return -1;

            int count = trackWorld.SectionsAt(node.Location).Take(2).Count();
            ambiguous = count > 1;
            trackVectorSectionIndex = geometry.SectionIndex;
            return geometry.Node.NodeIndex;
        }

        private static ResolvedPathRoute BuildRoute(PathRouteBranchKind branchKind, ImmutableArray<PathNode> pathNodes,
            ImmutableArray<PathRouteAnchor> anchors, TrackWorld trackWorld, PathRouteResolverOptions options, List<PathRouteDiagnostic> diagnostics, int startNodeIndex, Func<PathNode, int> nextNodeSelector, CancellationToken cancellationToken)
        {
            List<ResolvedPathSpan> spans = new List<ResolvedPathSpan>();
            HashSet<int> visited = new HashSet<int>();
            int currentNodeIndex = startNodeIndex;
            int endNodeIndex = startNodeIndex;

            while (currentNodeIndex >= 0 && currentNodeIndex < pathNodes.Length && visited.Add(currentNodeIndex))
            {
                cancellationToken.ThrowIfCancellationRequested();

                int nextNodeIndex = nextNodeSelector(pathNodes[currentNodeIndex]);
                if (!IsInRange(nextNodeIndex, pathNodes.Length))
                    break;

                spans.Add(ResolveSpan(currentNodeIndex, nextNodeIndex, anchors, trackWorld, options, diagnostics, cancellationToken));
                endNodeIndex = nextNodeIndex;
                currentNodeIndex = nextNodeIndex;
            }

            return new ResolvedPathRoute(branchKind, startNodeIndex, endNodeIndex, spans.ToImmutableArray());
        }

        private static ImmutableArray<ResolvedPathRoute> BuildPassingRoutes(ImmutableArray<PathNode> pathNodes, 
            ImmutableArray<PathRouteAnchor> anchors, TrackWorld trackWorld, PathRouteResolverOptions options, List<PathRouteDiagnostic> diagnostics, CancellationToken cancellationToken)
        {
            ImmutableArray<ResolvedPathRoute>.Builder routes = ImmutableArray.CreateBuilder<ResolvedPathRoute>();
            for (int i = 0; i < pathNodes.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // A passing branch starts only at a siding-start node that has both a main and a siding
                // successor; intermediate siding nodes also carry NextSidingNode but must not start a branch.
                PathNode node = pathNodes[i];
                if (IsInRange(node.NextMainNode, pathNodes.Length) && IsInRange(node.NextSidingNode, pathNodes.Length))
                    routes.Add(BuildRoute(PathRouteBranchKind.Passing, pathNodes, anchors, trackWorld, options, diagnostics, i, static pathNode => pathNode.NextSidingNode, cancellationToken));
            }
            return routes.ToImmutable();
        }

        private static void ValidatePassingBranchRejoins(ResolvedPathRoute mainRoute, ImmutableArray<ResolvedPathRoute> passingRoutes, List<PathRouteDiagnostic> diagnostics)
        {
            if (mainRoute == null || passingRoutes.IsEmpty)
                return;

            foreach (ResolvedPathRoute passingRoute in passingRoutes)
            {
                ImmutableHashSet<int> mainRouteNodes = MainRouteNodesAfterBranchStart(mainRoute, passingRoute.StartNodeIndex);
                if (!mainRouteNodes.Contains(passingRoute.EndNodeIndex))
                {
                    diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.PassingBranchDoesNotRejoinMain,
                        $"Passing branch starting at node {passingRoute.StartNodeIndex} ends at node {passingRoute.EndNodeIndex}, which is not on the remaining main path.",
                        passingRoute.StartNodeIndex, passingRoute.EndNodeIndex, "Reconnect the passing branch to a later main path node."));
                }
            }
        }

        private static ImmutableHashSet<int> MainRouteNodesAfterBranchStart(ResolvedPathRoute mainRoute, int branchStartNodeIndex)
        {
            ImmutableHashSet<int>.Builder nodes = ImmutableHashSet.CreateBuilder<int>();
            bool afterBranchStart = false;
            foreach (ResolvedPathSpan span in mainRoute.Spans)
            {
                if (span.FromNodeIndex == branchStartNodeIndex)
                    afterBranchStart = true;
                if (afterBranchStart)
                    nodes.Add(span.ToNodeIndex);
            }
            return nodes.ToImmutable();
        }

        private static ResolvedPathSpan ResolveSpan(int fromNodeIndex, int toNodeIndex, ImmutableArray<PathRouteAnchor> anchors,
            TrackWorld trackWorld, PathRouteResolverOptions options, List<PathRouteDiagnostic> diagnostics, CancellationToken cancellationToken)
        {
            if (trackWorld == null || anchors.IsDefaultOrEmpty || !IsInRange(fromNodeIndex, anchors.Length) || !IsInRange(toNodeIndex, anchors.Length))
                return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.NotResolved);

            PathRouteAnchor fromAnchor = anchors[fromNodeIndex];
            PathRouteAnchor toAnchor = anchors[toNodeIndex];
            if (!fromAnchor.HasTrackAnchor || !toAnchor.HasTrackAnchor)
                return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Unresolved);

            TrackRouteSearchResult routeSearchResult = FindTrackRoute(fromAnchor, toAnchor, trackWorld, options, cancellationToken);
            if (routeSearchResult.Resolved)
            {
                if (routeSearchResult.Ambiguous && options.AllowMainRouteFirstTieBreaking)
                {
                    diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.AmbiguousRoute,
                        $"Path span from node {fromNodeIndex} to node {toNodeIndex} has multiple equal-cost routes; the deterministic first route was selected.",
                        fromNodeIndex, toNodeIndex, "Add an explicit via node if a different route is intended."));
                    return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Resolved,
                        routeSearchResult.TrackVectorNodeIndexes, routeSearchResult.GeneratedIntermediaryAnchors);
                }

                if (routeSearchResult.Ambiguous)
                {
                    diagnostics.Add(new PathRouteDiagnostic(options.TreatAmbiguityAsError ? PathRouteDiagnosticSeverity.Error : PathRouteDiagnosticSeverity.Warning,
                        PathRouteDiagnosticCode.AmbiguousRoute,
                        $"Path span from node {fromNodeIndex} to node {toNodeIndex} has multiple equal-cost routes.",
                        fromNodeIndex, toNodeIndex, "Add an explicit via node to choose the intended route."));
                    return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Ambiguous,
                        routeSearchResult.TrackVectorNodeIndexes, routeSearchResult.GeneratedIntermediaryAnchors);
                }

                return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Resolved,
                    routeSearchResult.TrackVectorNodeIndexes, routeSearchResult.GeneratedIntermediaryAnchors);
            }

            diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.UnresolvedDenseSpan,
                $"Path span from node {fromNodeIndex} to node {toNodeIndex} could not be resolved by track graph routing.",
                fromNodeIndex, toNodeIndex, "Add explicit via nodes or increase the route search distance if appropriate."));

            return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Unresolved);
        }

        private static TrackRouteSearchResult FindTrackRoute(PathRouteAnchor fromAnchor, PathRouteAnchor toAnchor, TrackWorld trackWorld,
            PathRouteResolverOptions options, CancellationToken cancellationToken)
        {
            if (trackWorld?.TrackDatabase == null || !fromAnchor.HasTrackAnchor || !toAnchor.HasTrackAnchor)
                return TrackRouteSearchResult.Unresolved;

            TrackDatabase trackDatabase = trackWorld.TrackDatabase;
            if (!IsInRange(fromAnchor.TrackNodeIndex, trackDatabase.TrackNodes.Length) || !IsInRange(toAnchor.TrackNodeIndex, trackDatabase.TrackNodes.Length)
                || !IsInRange(fromAnchor.TrackNodeIndex, trackDatabase.TrackNodeConnectors.Length) || !IsInRange(toAnchor.TrackNodeIndex, trackDatabase.TrackNodeConnectors.Length))
                return TrackRouteSearchResult.Unresolved;

            if (fromAnchor.TrackNodeIndex == toAnchor.TrackNodeIndex)
                return BuildTrackRouteSearchResult(ImmutableArray.Create(fromAnchor.TrackNodeIndex), trackWorld, options, false);

            double maximumCost = options?.MaximumSparseSearchDistance ?? PathRouteResolverOptions.DefaultMaximumSparseSearchDistance;
            int nodeCount = trackDatabase.TrackNodes.Length;
            double[] costs = Enumerable.Repeat(double.PositiveInfinity, nodeCount).ToArray();
            int[] previousNodes = Enumerable.Repeat(-1, nodeCount).ToArray();
            bool[] ambiguousNodes = new bool[nodeCount];
            PriorityQueue<int, double> pendingNodes = new PriorityQueue<int, double>();

            costs[fromAnchor.TrackNodeIndex] = 0.0;
            pendingNodes.Enqueue(fromAnchor.TrackNodeIndex, 0.0);

            while (pendingNodes.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int currentNodeIndex = pendingNodes.Dequeue();
                double currentCost = costs[currentNodeIndex];
                if (currentCost > maximumCost)
                    continue;

                if (!IsInRange(currentNodeIndex, trackDatabase.TrackNodeConnectors.Length))
                    continue;

                ImmutableArray<TrackNodeConnector> connectors = trackDatabase.TrackNodeConnectors[currentNodeIndex].TrackNodeConnectors;
                if (connectors.IsDefaultOrEmpty)
                    continue;

                foreach (TrackNodeConnector connector in connectors.OrderBy(connector => connector.Link))
                {
                    int nextNodeIndex = connector.Link;
                    if (!IsInRange(nextNodeIndex, nodeCount) || trackDatabase.TrackNodes[nextNodeIndex] == null)
                        continue;

                    double nextCost = currentCost + RouteSearchNodeCost(trackWorld, trackDatabase.TrackNodes[nextNodeIndex]);
                    if (nextCost > maximumCost)
                        continue;

                    if (nextCost + CostEpsilon < costs[nextNodeIndex])
                    {
                        costs[nextNodeIndex] = nextCost;
                        previousNodes[nextNodeIndex] = currentNodeIndex;
                        ambiguousNodes[nextNodeIndex] = ambiguousNodes[currentNodeIndex];
                        pendingNodes.Enqueue(nextNodeIndex, nextCost);
                    }
                    else if (Math.Abs(nextCost - costs[nextNodeIndex]) <= CostEpsilon && previousNodes[nextNodeIndex] != currentNodeIndex)
                    {
                        ambiguousNodes[nextNodeIndex] = true;
                    }
                }
            }

            if (double.IsPositiveInfinity(costs[toAnchor.TrackNodeIndex]))
                return TrackRouteSearchResult.Unresolved;

            ImmutableArray<int> routeNodeIndexes = ReconstructRoute(previousNodes, fromAnchor.TrackNodeIndex, toAnchor.TrackNodeIndex);
            return routeNodeIndexes.IsEmpty
                ? TrackRouteSearchResult.Unresolved
                : BuildTrackRouteSearchResult(routeNodeIndexes, trackWorld, options, ambiguousNodes[toAnchor.TrackNodeIndex]);
        }

        private static ImmutableArray<int> ReconstructRoute(int[] previousNodes, int startNodeIndex, int endNodeIndex)
        {
            List<int> routeNodeIndexes = new List<int>();
            HashSet<int> visited = new HashSet<int>();
            int currentNodeIndex = endNodeIndex;
            while (currentNodeIndex >= 0 && visited.Add(currentNodeIndex))
            {
                routeNodeIndexes.Add(currentNodeIndex);
                if (currentNodeIndex == startNodeIndex)
                    break;

                currentNodeIndex = previousNodes[currentNodeIndex];
            }

            if (routeNodeIndexes[^1] != startNodeIndex)
                return ImmutableArray<int>.Empty;

            routeNodeIndexes.Reverse();
            return routeNodeIndexes.ToImmutableArray();
        }

        private static TrackRouteSearchResult BuildTrackRouteSearchResult(ImmutableArray<int> routeNodeIndexes, TrackWorld trackWorld,
            PathRouteResolverOptions options, bool ambiguous)
        {
            ImmutableArray<int>.Builder trackVectorNodeIndexes = ImmutableArray.CreateBuilder<int>();
            ImmutableArray<PathRouteAnchor>.Builder generatedAnchors = ImmutableArray.CreateBuilder<PathRouteAnchor>();
            TrackDatabase trackDatabase = trackWorld.TrackDatabase;
            for (int i = 0; i < routeNodeIndexes.Length; i++)
            {
                int trackNodeIndex = routeNodeIndexes[i];
                TrackNodeBase trackNode = trackDatabase.TrackNodes[trackNodeIndex];
                if (trackNode is VectorNode)
                    trackVectorNodeIndexes.Add(trackNodeIndex);

                if (options.IncludeGeneratedIntermediaryNodes && i > 0 && i < routeNodeIndexes.Length - 1)
                    generatedAnchors.Add(new PathRouteAnchor(-1, trackNode.Location, PathNodeType.Intermediate, trackNodeIndex, -1));
            }

            return new TrackRouteSearchResult(routeNodeIndexes, trackVectorNodeIndexes.ToImmutable(), generatedAnchors.ToImmutable(), ambiguous);
        }

        private static double RouteSearchNodeCost(TrackWorld trackWorld, TrackNodeBase trackNode)
        {
            if (trackNode is VectorNode vectorNode)
            {
                double length = trackWorld.VectorNodeLength(vectorNode);
                return length > 0.0 ? length : 1.0;
            }

            return 1.0;
        }

        private sealed class TrackRouteSearchResult
        {
            internal static TrackRouteSearchResult Unresolved { get; } = new TrackRouteSearchResult(
                ImmutableArray<int>.Empty, ImmutableArray<int>.Empty, ImmutableArray<PathRouteAnchor>.Empty, false);

            internal TrackRouteSearchResult(ImmutableArray<int> routeNodeIndexes, ImmutableArray<int> trackVectorNodeIndexes,
                ImmutableArray<PathRouteAnchor> generatedIntermediaryAnchors, bool ambiguous)
            {
                RouteNodeIndexes = routeNodeIndexes.IsDefault ? ImmutableArray<int>.Empty : routeNodeIndexes;
                TrackVectorNodeIndexes = trackVectorNodeIndexes.IsDefault ? ImmutableArray<int>.Empty : trackVectorNodeIndexes;
                GeneratedIntermediaryAnchors = generatedIntermediaryAnchors.IsDefault ? ImmutableArray<PathRouteAnchor>.Empty : generatedIntermediaryAnchors;
                Ambiguous = ambiguous;
            }

            internal ImmutableArray<int> RouteNodeIndexes { get; }

            internal ImmutableArray<int> TrackVectorNodeIndexes { get; }

            internal ImmutableArray<PathRouteAnchor> GeneratedIntermediaryAnchors { get; }

            internal bool Ambiguous { get; }

            internal bool Resolved => !RouteNodeIndexes.IsEmpty;
        }

        private static bool IsInRange(int nodeIndex, int nodeCount) => nodeIndex >= 0 && nodeIndex < nodeCount;
    }
}
