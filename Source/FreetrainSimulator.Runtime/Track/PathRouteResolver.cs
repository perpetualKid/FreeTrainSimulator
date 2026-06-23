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
        /// <summary>
        /// Resolves and validates a path model.
        /// </summary>
        public static PathRouteResolution Resolve(PathModel pathModel, TrackWorld trackWorld, PathRouteResolverOptions options = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            options ??= PathRouteResolverOptions.Default;

            List<PathRouteDiagnostic> diagnostics = new List<PathRouteDiagnostic>();
            ImmutableArray<PathNode> pathNodes = pathModel.PathNodes.IsDefault ? ImmutableArray<PathNode>.Empty : pathModel.PathNodes;
            if (pathNodes.IsEmpty)
            {
                diagnostics.Add(new PathRouteDiagnostic(
                    PathRouteDiagnosticSeverity.Fatal,
                    PathRouteDiagnosticCode.EmptyPath,
                    "Path has no authored nodes.",
                    suggestedAction: "Add a start node and an end node."));

                return new PathRouteResolution(null, diagnostics: diagnostics.ToImmutableArray());
            }

            cancellationToken.ThrowIfCancellationRequested();

            int startNodeIndex = FindFirstNodeOfType(pathNodes, PathNodeType.Start);
            int endNodeIndex = FindLastNodeOfType(pathNodes, PathNodeType.End);

            if (startNodeIndex < 0)
            {
                diagnostics.Add(new PathRouteDiagnostic(
                    PathRouteDiagnosticSeverity.Fatal,
                    PathRouteDiagnosticCode.MissingStartNode,
                    "Path has no start node.",
                    suggestedAction: "Mark one authored node as the start node."));
            }

            if (endNodeIndex < 0)
            {
                diagnostics.Add(new PathRouteDiagnostic(
                    PathRouteDiagnosticSeverity.Fatal,
                    PathRouteDiagnosticCode.MissingEndNode,
                    "Path has no end node.",
                    suggestedAction: "Mark one authored node as the end node."));
            }

            ValidateLinks(pathNodes, diagnostics);
            ImmutableHashSet<int> reachableNodes = startNodeIndex >= 0
                ? FindReachableNodes(pathNodes, startNodeIndex, diagnostics, cancellationToken)
                : ImmutableHashSet<int>.Empty;
            ReportUnreachableNodes(pathNodes, reachableNodes, diagnostics);

            ImmutableArray<PathRouteAnchor> anchors = ResolveAnchors(pathNodes, trackWorld, diagnostics, cancellationToken);
            ResolvedPathRoute mainRoute = startNodeIndex >= 0
                ? BuildRoute(PathRouteBranchKind.Main, pathNodes, anchors, trackWorld, startNodeIndex, static node => node.NextMainNode, cancellationToken)
                : null;
            ImmutableArray<ResolvedPathRoute> passingRoutes = options.ResolvePassingBranches
                ? BuildPassingRoutes(pathNodes, anchors, trackWorld, cancellationToken)
                : ImmutableArray<ResolvedPathRoute>.Empty;

            return new PathRouteResolution(mainRoute, passingRoutes, anchors, diagnostics.ToImmutableArray());
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
                    diagnostics.Add(new PathRouteDiagnostic(
                        PathRouteDiagnosticSeverity.Fatal,
                        PathRouteDiagnosticCode.InvalidMainLink,
                        $"Path node {i} has invalid main link {node.NextMainNode}.",
                        nodeIndex: i,
                        fromNodeIndex: i,
                        toNodeIndex: node.NextMainNode,
                        suggestedAction: "Repair or remove the invalid main path link."));
                }

                if (node.NextSidingNode < -1 || node.NextSidingNode >= pathNodes.Length)
                {
                    diagnostics.Add(new PathRouteDiagnostic(
                        PathRouteDiagnosticSeverity.Error,
                        PathRouteDiagnosticCode.InvalidSidingLink,
                        $"Path node {i} has invalid siding link {node.NextSidingNode}.",
                        nodeIndex: i,
                        fromNodeIndex: i,
                        toNodeIndex: node.NextSidingNode,
                        suggestedAction: "Repair or remove the invalid passing path link."));
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
            Visit(startNodeIndex);
            return reachable.ToImmutableHashSet();

            void Visit(int nodeIndex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (nodeIndex < 0 || nodeIndex >= pathNodes.Length)
                    return;
                if (active.Contains(nodeIndex))
                {
                    diagnostics.Add(new PathRouteDiagnostic(
                        PathRouteDiagnosticSeverity.Warning,
                        PathRouteDiagnosticCode.UnsupportedGraphCycle,
                        $"Path graph contains a cycle at node {nodeIndex}.",
                        nodeIndex: nodeIndex,
                        suggestedAction: "Add explicit via nodes or repair links if this cycle is not intentional."));
                    return;
                }
                if (!reachable.Add(nodeIndex))
                    return;

                active.Add(nodeIndex);
                PathNode node = pathNodes[nodeIndex];
                Visit(node.NextMainNode);
                Visit(node.NextSidingNode);
                active.Remove(nodeIndex);
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
                    diagnostics.Add(new PathRouteDiagnostic(
                        PathRouteDiagnosticSeverity.Warning,
                        PathRouteDiagnosticCode.UnreachableNode,
                        $"Path node {i} is not reachable from the start node.",
                        nodeIndex: i,
                        suggestedAction: "Connect the node to the main or passing path, or remove it."));
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

            int trackNodeIndex = ResolveTrackNodeIndex(node, trackWorld, out int trackVectorSectionIndex, out bool ambiguous);
            if (trackNodeIndex < 0)
            {
                diagnostics.Add(new PathRouteDiagnostic(
                    PathRouteDiagnosticSeverity.Error,
                    PathRouteDiagnosticCode.AnchorNotOnTrack,
                    $"Path node {authoredNodeIndex} could not be resolved to track.",
                    nodeIndex: authoredNodeIndex,
                    suggestedAction: "Move the path node onto a valid track segment or junction."));
            }
            else if (ambiguous)
            {
                diagnostics.Add(new PathRouteDiagnostic(
                    PathRouteDiagnosticSeverity.Warning,
                    PathRouteDiagnosticCode.AmbiguousAnchor,
                    $"Path node {authoredNodeIndex} resolves to multiple plausible track anchors.",
                    nodeIndex: authoredNodeIndex,
                    suggestedAction: "Add a route-choice node or choose the intended track anchor."));
            }

            return new PathRouteAnchor(authoredNodeIndex, node.Location, node.NodeType, trackNodeIndex, trackVectorSectionIndex);
        }

        private static int ResolveTrackNodeIndex(PathNode node, TrackWorld trackWorld, out int trackVectorSectionIndex, out bool ambiguous)
        {
            trackVectorSectionIndex = -1;
            ambiguous = false;

            if (node.NodeIndex > 0 && IsInRange(node.NodeIndex, trackWorld.TrackDatabase?.TrackNodes.Length ?? 0) && trackWorld.TrackDatabase.TrackNodes[node.NodeIndex] != null)
                return node.NodeIndex;

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
            ImmutableArray<PathRouteAnchor> anchors, TrackWorld trackWorld, int startNodeIndex, Func<PathNode, int> nextNodeSelector, CancellationToken cancellationToken)
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

                spans.Add(ResolveSpan(currentNodeIndex, nextNodeIndex, anchors, trackWorld));
                endNodeIndex = nextNodeIndex;
                currentNodeIndex = nextNodeIndex;
            }

            return new ResolvedPathRoute(branchKind, startNodeIndex, endNodeIndex, spans.ToImmutableArray());
        }

        private static ImmutableArray<ResolvedPathRoute> BuildPassingRoutes(ImmutableArray<PathNode> pathNodes, 
            ImmutableArray<PathRouteAnchor> anchors, TrackWorld trackWorld, CancellationToken cancellationToken)
        {
            ImmutableArray<ResolvedPathRoute>.Builder routes = ImmutableArray.CreateBuilder<ResolvedPathRoute>();
            for (int i = 0; i < pathNodes.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // A passing branch starts only at a siding-start node that has both a main and a siding
                // successor; intermediate siding nodes also carry NextSidingNode but must not start a branch.
                PathNode node = pathNodes[i];
                if (IsInRange(node.NextMainNode, pathNodes.Length) && IsInRange(node.NextSidingNode, pathNodes.Length))
                    routes.Add(BuildRoute(PathRouteBranchKind.Passing, pathNodes, anchors, trackWorld, i, static pathNode => pathNode.NextSidingNode, cancellationToken));
            }
            return routes.ToImmutable();
        }

        private static ResolvedPathSpan ResolveSpan(int fromNodeIndex, int toNodeIndex, ImmutableArray<PathRouteAnchor> anchors, TrackWorld trackWorld)
        {
            if (trackWorld == null || anchors.IsDefaultOrEmpty || !IsInRange(fromNodeIndex, anchors.Length) || !IsInRange(toNodeIndex, anchors.Length))
                return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.NotResolved);

            PathRouteAnchor fromAnchor = anchors[fromNodeIndex];
            PathRouteAnchor toAnchor = anchors[toNodeIndex];
            if (!fromAnchor.HasTrackAnchor || !toAnchor.HasTrackAnchor)
                return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Unresolved);

            ImmutableArray<int> trackVectorNodeIndexes = ResolveDenseTrackVectorNodes(fromAnchor.TrackNodeIndex, toAnchor.TrackNodeIndex, trackWorld);
            return trackVectorNodeIndexes.IsEmpty
                ? new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Unresolved)
                : new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Resolved, trackVectorNodeIndexes);
        }

        private static ImmutableArray<int> ResolveDenseTrackVectorNodes(int startTrackNodeIndex, int endTrackNodeIndex, TrackWorld trackWorld)
        {
            if (startTrackNodeIndex == endTrackNodeIndex)
                return ImmutableArray.Create(startTrackNodeIndex);

            TrackDatabase trackDatabase = trackWorld.TrackDatabase;
            if (trackDatabase == null || !IsInRange(startTrackNodeIndex, trackDatabase.TrackNodeConnectors.Length) || !IsInRange(endTrackNodeIndex, trackDatabase.TrackNodeConnectors.Length))
                return ImmutableArray<int>.Empty;

            ImmutableArray<TrackNodeConnector> startConnectors = trackDatabase.TrackNodeConnectors[startTrackNodeIndex].TrackNodeConnectors;
            ImmutableArray<TrackNodeConnector> endConnectors = trackDatabase.TrackNodeConnectors[endTrackNodeIndex].TrackNodeConnectors;

            TrackNodeConnector[] sharedConnectors = startConnectors.Intersect(endConnectors, TrackNodeConnectorComparer.LinkOnlyComparer).ToArray();
            if (sharedConnectors.Length == 1)
                return ImmutableArray.Create(startTrackNodeIndex, endTrackNodeIndex);

            ImmutableArray<int> intermediaryTrackNodes = ResolveSingleIntermediaryTrackNode(startConnectors, endConnectors, trackDatabase);
            return intermediaryTrackNodes.IsEmpty
                ? ImmutableArray<int>.Empty
                : ImmutableArray.Create(startTrackNodeIndex, intermediaryTrackNodes[0], endTrackNodeIndex);
        }

        private static ImmutableArray<int> ResolveSingleIntermediaryTrackNode(
            ImmutableArray<TrackNodeConnector> startConnectors,
            ImmutableArray<TrackNodeConnector> endConnectors,
            TrackDatabase trackDatabase)
        {
            ImmutableArray<int>.Builder candidates = ImmutableArray.CreateBuilder<int>();
            foreach (TrackNodeConnector startConnector in startConnectors)
            {
                if (!IsInRange(startConnector.Link, trackDatabase.TrackNodeConnectors.Length))
                    continue;

                foreach (TrackNodeConnector endConnector in endConnectors)
                {
                    if (!IsInRange(endConnector.Link, trackDatabase.TrackNodeConnectors.Length))
                        continue;

                    IEnumerable<TrackNodeConnector> connections = trackDatabase.TrackNodeConnectors[startConnector.Link].TrackNodeConnectors
                        .Intersect(trackDatabase.TrackNodeConnectors[endConnector.Link].TrackNodeConnectors, TrackNodeConnectorComparer.LinkOnlyComparer);
                    foreach (TrackNodeConnector connection in connections)
                    {
                        if (trackDatabase.TrackNodes[connection.Link] is VectorNode && !candidates.Contains(connection.Link))
                            candidates.Add(connection.Link);
                    }
                }
            }

            return candidates.Count == 1 ? ImmutableArray.Create(candidates[0]) : ImmutableArray<int>.Empty;
        }

        private static bool IsInRange(int nodeIndex, int nodeCount) => nodeIndex >= 0 && nodeIndex < nodeCount;
    }
}
