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

        // The configured MaximumSparseSearchDistance is a floor, not a hard ceiling: two anchors that are far
        // apart (a long but otherwise simple span) can legitimately need a route longer than the default cap.
        // The effective cap is therefore expanded to allow a realistic detour relative to the straight-line
        // distance between the anchors (curves, junction ladders), while still bounding pathological searches.
        private const double SparseSearchDetourFactor = 3.0;

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
                ? BuildRoute(PathRouteBranchKind.Main, pathNodes, anchors, trackWorld, options, diagnostics, startNodeIndex,
                    static node => node.NextMainNode, TrackRouteSearchConstraints.Default, cancellationToken)
                : null;
            ValidateMainRouteReachesEnd(mainRoute, endNodeIndex, diagnostics);
            ImmutableArray<ResolvedPathRoute> passingRoutes = options.ResolvePassingBranches
                ? BuildPassingRoutes(pathNodes, anchors, mainRoute, trackWorld, options, diagnostics, cancellationToken)
                : ImmutableArray<ResolvedPathRoute>.Empty;
            ValidatePassingBranchRejoins(mainRoute, passingRoutes, diagnostics);
            ValidatePassingBranchDistinctness(mainRoute, passingRoutes, diagnostics);

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

        private static ImmutableHashSet<int> FindReachableNodes(ImmutableArray<PathNode> pathNodes, int startNodeIndex, List<PathRouteDiagnostic> diagnostics, CancellationToken cancellationToken)
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

            if (node.NodeType.Includes(PathNodeType.Junction) && trackWorld.JunctionAt(node.Location) == null)
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
                TrackNodeBase storedTrackNode = trackDatabase.TrackNodes[node.NodeIndex];

                if (storedTrackNode is not VectorNode || trackWorld.SectionGeometry.Count > 0)
                {
                    int locationTrackNodeIndex = storedTrackNode switch
                    {
                        VectorNode => ResolveTrackNodeIndexByLocation(node, trackWorld, out _, out _),
                        _ => node.NodeType switch
                        {
                            var nodeType when nodeType.Includes(PathNodeType.Junction) =>
                                ResolveNonVectorTrackNodeIndexByLocation<JunctionNode>(node.Location, trackDatabase, out _),
                            _ => ResolveNonVectorTrackNodeIndexByLocation(node.Location, trackDatabase, out _),
                        },
                    };

                    int locationTrackVectorSectionIndex = -1;
                    bool locationAmbiguous = false;

                    if (storedTrackNode is VectorNode)
                    {
                        locationTrackNodeIndex = ResolveTrackNodeIndexByLocation(node, trackWorld,
                            out locationTrackVectorSectionIndex, out locationAmbiguous);
                    }

                    bool anchorContainsLocation = StoredAnchorContainsLocation(storedTrackNode, node, trackWorld, out int storedTrackVectorSectionIndex);

                    if (anchorContainsLocation)
                    {
                        trackVectorSectionIndex = storedTrackVectorSectionIndex;
                        ambiguous = locationAmbiguous;
                    }
                    else
                    {
                        if (locationTrackNodeIndex >= 0)
                        {
                            // A valid index from a previous layout is not authoritative when the stored location
                            // now resolves elsewhere. Re-snap to the location so reloads remain usable after track
                            // database changes while retaining the mismatch diagnostic for callers.
                            trackVectorSectionIndex = locationTrackVectorSectionIndex;
                            ambiguous = locationAmbiguous;
                        }

                        diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.AnchorLocationMismatch,
                            FormatAnchorLocationMismatchMessage(authoredNodeIndex, node.NodeIndex, locationTrackNodeIndex),
                            authoredNodeIndex, "Review the path node location and stored track anchor before saving or repairing the path."));

                        return locationTrackNodeIndex;
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

            if (node.NodeType.Includes(PathNodeType.Junction))
            {
                return trackNode is JunctionNode junctionNode &&
                    ResolveNonVectorTrackNodeIndexByLocation<JunctionNode>(node.Location, trackWorld.TrackDatabase, out _) == junctionNode.NodeIndex;
            }

            switch (trackNode)
            {
                case JunctionNode junctionNode:
                    return ResolveNonVectorTrackNodeIndexByLocation<JunctionNode>(node.Location, trackWorld.TrackDatabase, out _) == junctionNode.NodeIndex;
                case EndNode endNode:
                    return ResolveNonVectorTrackNodeIndexByLocation<EndNode>(node.Location, trackWorld.TrackDatabase, out _) == endNode.NodeIndex;
                default:
                    return false;
            }
        }

        private static int ResolveNonVectorTrackNodeIndexByLocation(in WorldLocation location,
            TrackDatabase trackDatabase, out bool ambiguous)
        {
            int junctionIndex = ResolveNonVectorTrackNodeIndexByLocation<JunctionNode>(location, trackDatabase, out bool junctionAmbiguous);
            int endIndex = ResolveNonVectorTrackNodeIndexByLocation<EndNode>(location, trackDatabase, out bool endAmbiguous);

            if (junctionIndex < 0)
            {
                ambiguous = endAmbiguous;
                return endIndex;
            }

            if (endIndex < 0)
            {
                ambiguous = junctionAmbiguous;
                return junctionIndex;
            }

            double junctionDistance = WorldLocation.GetDistanceSquared2D(trackDatabase.TrackNodes[junctionIndex].Location, location);
            double endDistance = WorldLocation.GetDistanceSquared2D(trackDatabase.TrackNodes[endIndex].Location, location);
            ambiguous = junctionAmbiguous || endAmbiguous || Math.Abs(junctionDistance - endDistance) <= CostEpsilon;
            return junctionDistance <= endDistance ? junctionIndex : endIndex;
        }

        private static int ResolveNonVectorTrackNodeIndexByLocation<TNode>(in WorldLocation location,
            TrackDatabase trackDatabase, out bool ambiguous) where TNode : TrackNodeBase
        {
            WorldLocation targetLocation = location;
            (int NodeIndex, double Distance)[] matches = trackDatabase.TrackNodes
                .OfType<TNode>()
                .Select(node => (node.NodeIndex, WorldLocation.GetDistanceSquared2D(node.Location, targetLocation)))
                .Where(match => match.Item2 <= WorldLocation.ProximityTolerance)
                .OrderBy(match => match.Item2)
                .Take(2)
                .ToArray();
            ambiguous = matches.Length > 1;
            return matches.Length > 0 ? matches[0].NodeIndex : -1;
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

            if (node.NodeType.Includes(PathNodeType.Junction))
            {
                JunctionNode junctionNode = trackWorld.JunctionAt(node.Location);
                if (junctionNode != null)
                    return junctionNode.NodeIndex;
            }

            if (node.NodeType.Includes(PathNodeType.End))
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
            ImmutableArray<PathRouteAnchor> anchors, TrackWorld trackWorld, PathRouteResolverOptions options,
            List<PathRouteDiagnostic> diagnostics, int startNodeIndex, Func<PathNode, int> nextNodeSelector,
            TrackRouteSearchConstraints searchConstraints, CancellationToken cancellationToken)
        {
            List<ResolvedPathSpan> spans = new List<ResolvedPathSpan>();
            HashSet<int> visited = new HashSet<int>();
            int currentNodeIndex = startNodeIndex;
            int endNodeIndex = startNodeIndex;
            int previousNodeIndex = -1;

            while (currentNodeIndex >= 0 && currentNodeIndex < pathNodes.Length && visited.Add(currentNodeIndex))
            {
                cancellationToken.ThrowIfCancellationRequested();

                int nextNodeIndex = nextNodeSelector(pathNodes[currentNodeIndex]);
                if (!IsInRange(nextNodeIndex, pathNodes.Length))
                    break;

                TrackDirection? departureDirection = InferDepartureDirection(previousNodeIndex, currentNodeIndex, pathNodes, anchors, trackWorld);
                spans.Add(ResolveSpan(currentNodeIndex, nextNodeIndex, anchors, trackWorld, options, diagnostics,
                    searchConstraints.WithDepartureDirection(departureDirection), cancellationToken));
                endNodeIndex = nextNodeIndex;
                previousNodeIndex = currentNodeIndex;
                currentNodeIndex = nextNodeIndex;
            }

            return new ResolvedPathRoute(branchKind, startNodeIndex, endNodeIndex, spans.ToImmutableArray());
        }

        private static ImmutableArray<ResolvedPathRoute> BuildPassingRoutes(ImmutableArray<PathNode> pathNodes, ImmutableArray<PathRouteAnchor> anchors, ResolvedPathRoute mainRoute, TrackWorld trackWorld,
            PathRouteResolverOptions options, List<PathRouteDiagnostic> diagnostics, CancellationToken cancellationToken)
        {
            ImmutableArray<ResolvedPathRoute>.Builder routes = ImmutableArray.CreateBuilder<ResolvedPathRoute>();
            for (int i = 0; i < pathNodes.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // A passing branch starts only at a siding-start node that has both a main and a siding
                // successor; via siding nodes also carry NextSidingNode but must not start a branch.
                PathNode node = pathNodes[i];
                if (IsInRange(node.NextMainNode, pathNodes.Length) && IsInRange(node.NextSidingNode, pathNodes.Length))
                {
                    int rejoinNodeIndex = FindRouteEnd(pathNodes, i, static pathNode => pathNode.NextSidingNode);
                    ImmutableHashSet<TrackRouteTraversal> mainRouteEdges = MainRouteTraversalsBetween(mainRoute, i, rejoinNodeIndex);
                    bool requireAlternativeRoute = node.NextSidingNode == rejoinNodeIndex;
                    TrackRouteSearchConstraints searchConstraints = new TrackRouteSearchConstraints(null, mainRouteEdges,
                        requireAlternativeRoute);
                    ResolvedPathRoute route = BuildRoute(PathRouteBranchKind.Passing, pathNodes, anchors, trackWorld, options, diagnostics, i,
                        static pathNode => pathNode.NextSidingNode, searchConstraints, cancellationToken);
                    routes.Add(route);

                    if (requireAlternativeRoute && trackWorld != null && route.Spans.Any(span => span.Status == PathRouteSpanStatus.Unresolved))
                    {
                        diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Error,
                            PathRouteDiagnosticCode.PassingBranchMatchesMainRoute,
                            $"Passing branch from node {i} to node {rejoinNodeIndex} has no physically distinct route.",
                            i, rejoinNodeIndex, "Choose a rejoin or passing-route anchor on an alternate physical track."));
                    }
                }
            }
            return routes.ToImmutable();
        }

        private static int FindRouteEnd(ImmutableArray<PathNode> pathNodes, int startNodeIndex, Func<PathNode, int> nextNodeSelector)
        {
            HashSet<int> visited = new HashSet<int>();
            int currentNodeIndex = startNodeIndex;

            while (IsInRange(currentNodeIndex, pathNodes.Length) && visited.Add(currentNodeIndex))
            {
                int nextNodeIndex = nextNodeSelector(pathNodes[currentNodeIndex]);
                if (!IsInRange(nextNodeIndex, pathNodes.Length))
                    return currentNodeIndex;
                currentNodeIndex = nextNodeIndex;
            }
            return currentNodeIndex;
        }

        private static ImmutableHashSet<TrackRouteTraversal> MainRouteTraversalsBetween(ResolvedPathRoute mainRoute, int startNodeIndex, int endNodeIndex)
        {
            if (mainRoute == null)
                return ImmutableHashSet<TrackRouteTraversal>.Empty;

            ImmutableHashSet<TrackRouteTraversal>.Builder traversals = ImmutableHashSet.CreateBuilder<TrackRouteTraversal>();
            bool withinBranchBounds = false;
            foreach (ResolvedPathSpan span in mainRoute.Spans)
            {
                if (span.FromNodeIndex == startNodeIndex)
                    withinBranchBounds = true;
                if (!withinBranchBounds)
                    continue;

                ResolvedRouteCandidate candidate = span.Candidates.FirstOrDefault();

                if (candidate != null)
                    traversals.UnionWith(candidate.PhysicalTraversals);

                if (span.ToNodeIndex == endNodeIndex)
                    break;
            }

            return traversals.ToImmutable();
        }

        private static void ValidatePassingBranchDistinctness(ResolvedPathRoute mainRoute, ImmutableArray<ResolvedPathRoute> passingRoutes,
            List<PathRouteDiagnostic> diagnostics)
        {
            if (mainRoute == null)
                return;

            foreach (ResolvedPathRoute passingRoute in passingRoutes)
            {
                if (passingRoute.Spans.IsDefaultOrEmpty || passingRoute.Spans.Any(span => span.Status != PathRouteSpanStatus.Resolved))
                    continue;

                ImmutableArray<ResolvedPathSpan> boundedMainSpans = MainRouteSpansBetween(mainRoute, passingRoute.StartNodeIndex, passingRoute.EndNodeIndex);

                if (boundedMainSpans.IsDefaultOrEmpty || boundedMainSpans.Any(span => span.Status != PathRouteSpanStatus.Resolved))
                    continue;

                ImmutableHashSet<TrackRouteTraversal> mainTraversals = MainRouteTraversalsBetween(mainRoute,
                    passingRoute.StartNodeIndex, passingRoute.EndNodeIndex);
                ImmutableArray<TrackRouteTraversal> passingTraversals = passingRoute.Spans
                    .SelectMany(span => span.Candidates.FirstOrDefault()?.PhysicalTraversals ?? ImmutableArray<TrackRouteTraversal>.Empty)
                    .ToImmutableArray();

                if (passingTraversals.IsEmpty || passingTraversals.All(mainTraversals.Contains))
                {
                    diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Error,
                        PathRouteDiagnosticCode.PassingBranchMatchesMainRoute,
                        $"Passing branch from node {passingRoute.StartNodeIndex} to node {passingRoute.EndNodeIndex} does not use a physically distinct route.",
                        passingRoute.StartNodeIndex, passingRoute.EndNodeIndex,
                        "Add or retain a passing-route anchor on an alternate physical track."));
                }
            }
        }

        private static ImmutableArray<ResolvedPathSpan> MainRouteSpansBetween(ResolvedPathRoute mainRoute, int startNodeIndex, int endNodeIndex)
        {
            ImmutableArray<ResolvedPathSpan>.Builder spans = ImmutableArray.CreateBuilder<ResolvedPathSpan>();
            bool withinBranchBounds = false;

            foreach (ResolvedPathSpan span in mainRoute.Spans)
            {
                if (span.FromNodeIndex == startNodeIndex)
                    withinBranchBounds = true;

                if (!withinBranchBounds)
                    continue;

                spans.Add(span);

                if (span.ToNodeIndex == endNodeIndex)
                    break;
            }
            return spans.ToImmutable();
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

        private static ResolvedPathSpan ResolveSpan(int fromNodeIndex, int toNodeIndex,
            ImmutableArray<PathRouteAnchor> anchors, TrackWorld trackWorld, PathRouteResolverOptions options,
            List<PathRouteDiagnostic> diagnostics, TrackRouteSearchConstraints searchConstraints,
            CancellationToken cancellationToken)
        {
            if (trackWorld == null || anchors.IsDefaultOrEmpty || !IsInRange(fromNodeIndex, anchors.Length) || !IsInRange(toNodeIndex, anchors.Length))
                return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.NotResolved);

            PathRouteAnchor fromAnchor = anchors[fromNodeIndex];
            PathRouteAnchor toAnchor = anchors[toNodeIndex];
            if (!fromAnchor.HasTrackAnchor || !toAnchor.HasTrackAnchor)
                return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Unresolved);

            TrackRouteSearchResult routeSearchResult = FindTrackRoute(fromAnchor, toAnchor, trackWorld, options,
                searchConstraints, cancellationToken);
            if (routeSearchResult.Resolved)
            {
                if (routeSearchResult.Ambiguous && options.AllowMainRouteFirstTieBreaking)
                {
                    diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.AmbiguousRoute,
                        $"Path span from node {fromNodeIndex} to node {toNodeIndex} has {routeSearchResult.Candidates.Length} equal-cost routes; the deterministic first route was selected.",
                        fromNodeIndex, toNodeIndex, "Choose a candidate route or add an explicit via node if a different route is intended."));
                    return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Resolved,
                        routeSearchResult.TrackVectorNodeIndexes, routeSearchResult.GeneratedIntermediaryAnchors, routeSearchResult.Candidates);
                }

                if (routeSearchResult.Ambiguous)
                {
                    diagnostics.Add(new PathRouteDiagnostic(options.TreatAmbiguityAsError ? PathRouteDiagnosticSeverity.Error : PathRouteDiagnosticSeverity.Warning,
                        PathRouteDiagnosticCode.AmbiguousRoute,
                        $"Path span from node {fromNodeIndex} to node {toNodeIndex} has {routeSearchResult.Candidates.Length} equal-cost routes.",
                        fromNodeIndex, toNodeIndex, "Choose a candidate route or add an explicit via node to choose the intended route."));
                    return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Ambiguous,
                        routeSearchResult.TrackVectorNodeIndexes, routeSearchResult.GeneratedIntermediaryAnchors, routeSearchResult.Candidates);
                }

                return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Resolved,
                    routeSearchResult.TrackVectorNodeIndexes, routeSearchResult.GeneratedIntermediaryAnchors, routeSearchResult.Candidates);
            }

            diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.UnresolvedDenseSpan,
                $"Path span from node {fromNodeIndex} to node {toNodeIndex} could not be resolved by track graph routing.",
                fromNodeIndex, toNodeIndex, "Add explicit via nodes or increase the route search distance if appropriate."));

            return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Unresolved);
        }

        private static TrackRouteSearchResult FindTrackRoute(PathRouteAnchor fromAnchor, PathRouteAnchor toAnchor,
            TrackWorld trackWorld, PathRouteResolverOptions options, TrackRouteSearchConstraints searchConstraints,
            CancellationToken cancellationToken)
        {
            if (trackWorld?.TrackDatabase == null || !fromAnchor.HasTrackAnchor || !toAnchor.HasTrackAnchor)
                return TrackRouteSearchResult.Unresolved;

            TrackDatabase trackDatabase = trackWorld.TrackDatabase;
            if (!IsInRange(fromAnchor.TrackNodeIndex, trackDatabase.TrackNodes.Length) || !IsInRange(toAnchor.TrackNodeIndex, trackDatabase.TrackNodes.Length)
                || !IsInRange(fromAnchor.TrackNodeIndex, trackDatabase.TrackNodeConnectors.Length) || !IsInRange(toAnchor.TrackNodeIndex, trackDatabase.TrackNodeConnectors.Length))
                return TrackRouteSearchResult.Unresolved;

            if (fromAnchor.TrackNodeIndex == toAnchor.TrackNodeIndex && !searchConstraints.RequireAlternativeRoute)
            {
                if (searchConstraints.DepartureDirection.HasValue && trackDatabase.TrackNodes[fromAnchor.TrackNodeIndex] is VectorNode sameVectorNode)
                {
                    double fromPosition = AnchorPosition(fromAnchor, sameVectorNode, trackWorld);
                    double toPosition = AnchorPosition(toAnchor, sameVectorNode, trackWorld);
                    if (!double.IsNaN(fromPosition) && !double.IsNaN(toPosition) &&
                        (searchConstraints.DepartureDirection == TrackDirection.Ahead && toPosition + CostEpsilon < fromPosition ||
                        searchConstraints.DepartureDirection == TrackDirection.Reverse && toPosition - CostEpsilon > fromPosition))
                    {
                        return TrackRouteSearchResult.Unresolved;
                    }
                }

                return BuildTrackRouteSearchResult(ImmutableArray.Create(fromAnchor.TrackNodeIndex), trackWorld, options, 0.0);
            }

            double maximumCost = EffectiveSearchDistance(fromAnchor, toAnchor, options);
            TrackRouteSearchState startState = new TrackRouteSearchState(-1, fromAnchor.TrackNodeIndex, -1, -1,
                !searchConstraints.RequireAlternativeRoute);
            TrackRouteSearchWorkspace workspace = new TrackRouteSearchWorkspace(startState);

            while (workspace.TryDequeue(out TrackRouteSearchState currentState, out double queuedCost))
            {
                cancellationToken.ThrowIfCancellationRequested();

                double currentCost = workspace.Cost(currentState);
                if (queuedCost > currentCost + CostEpsilon || currentCost > maximumCost)
                    continue;
                if (currentState != startState && currentState.CurrentNodeIndex == toAnchor.TrackNodeIndex)
                    continue;

                if (!IsInRange(currentState.CurrentNodeIndex, trackDatabase.TrackNodeConnectors.Length))
                    continue;

                ImmutableArray<TrackNodeConnector> connectors = trackDatabase.TrackNodeConnectors[currentState.CurrentNodeIndex].TrackNodeConnectors;
                if (connectors.IsDefaultOrEmpty)
                    continue;

                foreach (int connectorIndex in Enumerable.Range(0, connectors.Length).OrderBy(index => connectors[index].Link).ThenBy(index => connectors[index].Direction).ThenBy(index => index))
                {
                    TrackNodeConnector connector = connectors[connectorIndex];
                    int nextNodeIndex = connector.Link;
                    if (!IsInRange(nextNodeIndex, trackDatabase.TrackNodes.Length) || trackDatabase.TrackNodes[nextNodeIndex] == null ||
                        !CanTraverse(currentState, connectorIndex, connector, trackDatabase,
                            searchConstraints.DepartureDirection))
                    {
                        continue;
                    }

                    ImmutableArray<TrackNodeConnector> nextConnectors = IsInRange(nextNodeIndex, trackDatabase.TrackNodeConnectors.Length)
                        ? trackDatabase.TrackNodeConnectors[nextNodeIndex].TrackNodeConnectors
                        : ImmutableArray<TrackNodeConnector>.Empty;
                    int connectorOrdinal = ConnectorOrdinal(connectors, connectorIndex, nextNodeIndex);
                    int reciprocalConnectorIndex = ReciprocalConnectorIndex(nextConnectors,
                        currentState.CurrentNodeIndex, connectorOrdinal);

                    if (reciprocalConnectorIndex >= 0)
                        RelaxConnectedState(reciprocalConnectorIndex);
                    else if (!nextConnectors.Any(nextConnector => nextConnector.Link == currentState.CurrentNodeIndex))
                        RelaxConnectedState(-1);

                    void RelaxConnectedState(int incomingConnectorIndex)
                    {
                        TrackNodeConnector incomingConnector = IsInRange(incomingConnectorIndex, nextConnectors.Length)
                            ? nextConnectors[incomingConnectorIndex]
                            : null;
                        double nextCost = currentCost + RouteSearchNodeCost(trackWorld, trackDatabase.TrackNodes[nextNodeIndex],
                            nextNodeIndex, toAnchor, incomingConnector);

                        if (nextCost > maximumCost)
                            return;

                        TrackRouteTraversal traversal = new TrackRouteTraversal(currentState.CurrentNodeIndex, connectorIndex, nextNodeIndex);
                        bool alternativeEdgeUsed = currentState.AlternativeEdgeUsed ||
                            !searchConstraints.MainRouteTraversals.Contains(traversal);
                        workspace.Relax(new TrackRouteSearchState(currentState.CurrentNodeIndex, nextNodeIndex,
                            connectorIndex, incomingConnectorIndex, alternativeEdgeUsed), currentState, nextCost);
                    }
                }

                static int ConnectorOrdinal(ImmutableArray<TrackNodeConnector> connectors, int connectorIndex, int linkedNodeIndex)
                {
                    int ordinal = 0;
                    for (int index = 0; index < connectorIndex; index++)
                    {
                        if (connectors[index].Link == linkedNodeIndex)
                            ordinal++;
                    }
                    return ordinal;
                }

                static int ReciprocalConnectorIndex(ImmutableArray<TrackNodeConnector> connectors, int previousNodeIndex, int ordinal)
                {
                    int matchOrdinal = 0;
                    for (int index = 0; index < connectors.Length; index++)
                    {
                        if (connectors[index].Link != previousNodeIndex)
                            continue;
                        if (matchOrdinal == ordinal)
                            return index;
                        matchOrdinal++;
                    }
                    return -1;
                }

            }

            KeyValuePair<TrackRouteSearchState, double>[] targetStates = workspace.Costs
                .Where(item => item.Key.CurrentNodeIndex == toAnchor.TrackNodeIndex &&
                    (!searchConstraints.RequireAlternativeRoute || item.Key.AlternativeEdgeUsed))
                .ToArray();
            if (targetStates.Length == 0)
                return TrackRouteSearchResult.Unresolved;

            double minimumCost = targetStates.Min(item => item.Value);
            ImmutableArray<TrackRouteSearchState> minimumTargetStates = targetStates
                .Where(item => Math.Abs(item.Value - minimumCost) <= CostEpsilon)
                .Select(item => item.Key)
                .OrderBy(state => state.PreviousNodeIndex)
                .ThenBy(state => state.IncomingConnectorIndex)
                .ToImmutableArray();
            ImmutableArray<ResolvedRouteCandidate> candidates = EnumerateRouteCandidates(workspace.OptimalPredecessors,
                startState, minimumTargetStates, fromAnchor, trackWorld, options, minimumCost,
                searchConstraints.DepartureDirection, cancellationToken);
            return candidates.IsEmpty
                ? TrackRouteSearchResult.Unresolved
                : new TrackRouteSearchResult(candidates[0].RouteNodeIndexes, candidates[0].TrackVectorNodeIndexes, candidates[0].GeneratedIntermediaryAnchors, candidates.Length > 1, candidates);
        }

        // Walks the optimal-predecessor sets backwards from the target to enumerate every distinct equal-cost
        // route. Predecessors are visited in ascending track node order and the resulting candidates are ordered
        // lexicographically, so both the selected route and a candidate index stay stable across resolutions.
        private static ImmutableArray<ResolvedRouteCandidate> EnumerateRouteCandidates(Dictionary<TrackRouteSearchState, List<TrackRouteSearchState>> optimalPredecessors, TrackRouteSearchState startState,
            ImmutableArray<TrackRouteSearchState> endStates, PathRouteAnchor fromAnchor, TrackWorld trackWorld, PathRouteResolverOptions options, double cost, TrackDirection? departureDirection, CancellationToken cancellationToken)
        {
            List<ImmutableArray<TrackRouteSearchState>> routes = new List<ImmutableArray<TrackRouteSearchState>>();
            List<TrackRouteSearchState> reversedRoute = new List<TrackRouteSearchState>();
            HashSet<TrackRouteSearchState> routeStates = new HashSet<TrackRouteSearchState>();

            void Walk(TrackRouteSearchState state)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!routeStates.Add(state))
                    return;

                reversedRoute.Add(state);
                if (state == startState)
                {
                    ImmutableArray<TrackRouteSearchState>.Builder route = ImmutableArray.CreateBuilder<TrackRouteSearchState>(reversedRoute.Count);
                    for (int routeIndex = reversedRoute.Count - 1; routeIndex >= 0; routeIndex--)
                        route.Add(reversedRoute[routeIndex]);
                    routes.Add(route.ToImmutable());
                }
                else
                {
                    IEnumerable<TrackRouteSearchState> predecessors = optimalPredecessors.TryGetValue(state, out List<TrackRouteSearchState> states)
                        ? states.OrderBy(predecessor => predecessor.PreviousNodeIndex).ThenBy(predecessor => predecessor.CurrentNodeIndex)
                            .ThenBy(predecessor => predecessor.IncomingConnectorIndex)
                        : Enumerable.Empty<TrackRouteSearchState>();
                    foreach (TrackRouteSearchState predecessor in predecessors)
                        Walk(predecessor);
                }

                reversedRoute.RemoveAt(reversedRoute.Count - 1);
                routeStates.Remove(state);
            }

            foreach (TrackRouteSearchState endState in endStates)
                Walk(endState);

            return routes
                .OrderBy(route => route.Select(state => state.CurrentNodeIndex).ToImmutableArray(), RouteComparer.Instance)
                .GroupBy(route => route.Select(state => state.CurrentNodeIndex).ToImmutableArray(), RouteEqualityComparer.Instance)
                .Select(group => group.First())
                .Select(route => BuildRouteCandidate(route.Select(state => state.CurrentNodeIndex).ToImmutableArray(), trackWorld, options,
                    cost, fromAnchor, departureDirection, BuildPhysicalTraversals(route)))
                .ToImmutableArray();
        }

        private static ImmutableArray<TrackRouteTraversal> BuildPhysicalTraversals(ImmutableArray<TrackRouteSearchState> route)
        {
            ImmutableArray<TrackRouteTraversal>.Builder traversals = ImmutableArray.CreateBuilder<TrackRouteTraversal>(Math.Max(0, route.Length - 1));
            for (int index = 1; index < route.Length; index++)
            {
                TrackRouteSearchState state = route[index];
                traversals.Add(new TrackRouteTraversal(state.PreviousNodeIndex, state.PreviousConnectorIndex,
                    state.CurrentNodeIndex));
            }
            return traversals.ToImmutable();
        }

        private static bool CanTraverse(TrackRouteSearchState currentState, int outgoingConnectorIndex, TrackNodeConnector outgoingConnector, TrackDatabase trackDatabase, TrackDirection? departureDirection)
        {
            if (currentState.PreviousNodeIndex < 0)
            {
                if (!departureDirection.HasValue)
                    return true;

                ImmutableArray<TrackNodeConnector> departureConnectors = trackDatabase.TrackNodeConnectors[currentState.CurrentNodeIndex].TrackNodeConnectors;
                if (departureConnectors.Length < 2)
                    return true;

                int requiredConnectorIndex = departureDirection == TrackDirection.Ahead ? 1 : 0;
                return outgoingConnectorIndex == requiredConnectorIndex;
            }
            if (currentState.IncomingConnectorIndex == outgoingConnectorIndex)
                return false;

            if (trackDatabase.TrackNodes[currentState.CurrentNodeIndex] is not JunctionNode)
                return true;

            ImmutableArray<TrackNodeConnector> connectors = trackDatabase.TrackNodeConnectors[currentState.CurrentNodeIndex].TrackNodeConnectors;
            bool hasInPins = connectors.Any(connector => connector.ConnectorType == ConnectorType.InPin);
            bool hasOutPins = connectors.Any(connector => connector.ConnectorType == ConnectorType.OutPin);
            if (!hasInPins || !hasOutPins)
                return true;

            TrackNodeConnector incomingConnector = IsInRange(currentState.IncomingConnectorIndex, connectors.Length)
                ? connectors[currentState.IncomingConnectorIndex]
                : connectors.FirstOrDefault(connector => connector.Link == currentState.PreviousNodeIndex);
            return incomingConnector != null && incomingConnector.ConnectorType != outgoingConnector.ConnectorType;
        }

        private static TrackDirection? InferDepartureDirection(int previousNodeIndex, int currentNodeIndex, ImmutableArray<PathNode> pathNodes, ImmutableArray<PathRouteAnchor> anchors, TrackWorld trackWorld)
        {
            if (trackWorld?.TrackDatabase == null || !IsInRange(previousNodeIndex, anchors.Length) ||
                !IsInRange(currentNodeIndex, anchors.Length))
                return null;

            PathRouteAnchor previous = anchors[previousNodeIndex];
            PathRouteAnchor current = anchors[currentNodeIndex];
            if (!IsInRange(current.TrackNodeIndex, trackWorld.TrackDatabase.TrackNodes.Length) ||
                previous.TrackNodeIndex != current.TrackNodeIndex ||
                trackWorld.TrackDatabase.TrackNodes[current.TrackNodeIndex] is not VectorNode vectorNode)
                return null;

            double previousPosition = AnchorPosition(previous, vectorNode, trackWorld);
            double currentPosition = AnchorPosition(current, vectorNode, trackWorld);
            if (double.IsNaN(previousPosition) || double.IsNaN(currentPosition) || Math.Abs(previousPosition - currentPosition) <= CostEpsilon)
                return null;

            TrackDirection direction = currentPosition > previousPosition ? TrackDirection.Ahead : TrackDirection.Reverse;
            bool reversal = pathNodes[currentNodeIndex].NodeType.Includes(PathNodeType.Reversal);
            TrackDirection result = reversal ? direction.Reverse() : direction;
            return result;
        }

        private static double AnchorPosition(PathRouteAnchor anchor, VectorNode vectorNode, TrackWorld trackWorld)
        {
            if (!IsInRange(anchor.TrackVectorSectionIndex, vectorNode.VectorSections.Length))
                return double.NaN;

            double position = 0;
            for (int sectionIndex = 0; sectionIndex < anchor.TrackVectorSectionIndex; sectionIndex++)
                position += trackWorld.SectionLength(vectorNode, sectionIndex);

            VectorSectionNode section = vectorNode.VectorSections[anchor.TrackVectorSectionIndex];
            return trackWorld.SectionGeometry.TryGetValue(section, out SectionGeometry geometry)
                ? position + geometry.DistanceOnSection(anchor.Location)
                : double.NaN;
        }

        private static ResolvedRouteCandidate BuildRouteCandidate(ImmutableArray<int> routeNodeIndexes, TrackWorld trackWorld,
            PathRouteResolverOptions options, double cost, PathRouteAnchor fromAnchor, TrackDirection? departureDirection,
            ImmutableArray<TrackRouteTraversal> physicalTraversals)
        {
            ImmutableArray<int>.Builder trackVectorNodeIndexes = ImmutableArray.CreateBuilder<int>();
            ImmutableArray<PathRouteAnchor>.Builder generatedAnchors = ImmutableArray.CreateBuilder<PathRouteAnchor>();
            TrackDatabase trackDatabase = trackWorld.TrackDatabase;
            // A departure direction is only known when the previous point sits on the same vector node. Materializing
            // it as an anchor near the departure end keeps that direction readable after the path is persisted.
            if (options.IncludeGeneratedIntermediaryNodes && departureDirection.HasValue && fromAnchor != null && trackDatabase.TrackNodes[fromAnchor.TrackNodeIndex] is VectorNode departureNode)
            {
                int sectionIndex = departureDirection == TrackDirection.Ahead ? departureNode.VectorSections.Length - 1 : 0;
                VectorSectionNode boundarySection = departureNode.VectorSections[sectionIndex];
                WorldLocation boundary = boundarySection.Location;
                if (trackWorld.SectionGeometry.TryGetValue(boundarySection, out SectionGeometry boundaryGeometry))
                {
                    double inset = Math.Min(boundaryGeometry.Length / 2, WorldLocation.ProximityTolerance * 2);
                    boundary = boundaryGeometry.LocationAt(departureDirection == TrackDirection.Ahead
                        ? boundaryGeometry.Length - inset
                        : inset);
                }
                generatedAnchors.Add(new PathRouteAnchor(-1, boundary, PathNodeType.Via,
                    departureNode.NodeIndex, sectionIndex));
            }
            for (int i = 0; i < routeNodeIndexes.Length; i++)
            {
                int trackNodeIndex = routeNodeIndexes[i];
                TrackNodeBase trackNode = trackDatabase.TrackNodes[trackNodeIndex];
                if (trackNode is VectorNode)
                    trackVectorNodeIndexes.Add(trackNodeIndex);

                if (options.IncludeGeneratedIntermediaryNodes && i > 0 && i < routeNodeIndexes.Length - 1)
                    AddGeneratedRouteAnchor(generatedAnchors,
                        new PathRouteAnchor(-1, trackNode.Location, PathNodeType.Via, trackNodeIndex, -1),
                        trackDatabase);
            }

            return new ResolvedRouteCandidate(routeNodeIndexes, trackVectorNodeIndexes.ToImmutable(), generatedAnchors.ToImmutable(), cost, physicalTraversals);
        }

        private static void AddGeneratedRouteAnchor(ImmutableArray<PathRouteAnchor>.Builder anchors, PathRouteAnchor anchor, TrackDatabase trackDatabase)
        {
            if (anchors.Count == 0 || anchors[^1].TrackVectorSectionIndex >= 0 || anchors[^1].Location != anchor.Location)
            {
                anchors.Add(anchor);
                return;
            }

            bool previousIsJunction = trackDatabase.TrackNodes[anchors[^1].TrackNodeIndex] is JunctionNode;
            bool currentIsJunction = trackDatabase.TrackNodes[anchor.TrackNodeIndex] is JunctionNode;
            if (!previousIsJunction && currentIsJunction)
                anchors[^1] = anchor;
        }

        // Lexicographic order over the traversed track node indexes, giving a stable candidate order.
        private sealed class RouteComparer : IComparer<ImmutableArray<int>>
        {
            internal static RouteComparer Instance { get; } = new RouteComparer();

            public int Compare(ImmutableArray<int> x, ImmutableArray<int> y)
            {
                int length = Math.Min(x.Length, y.Length);
                for (int i = 0; i < length; i++)
                {
                    int comparison = x[i].CompareTo(y[i]);
                    if (comparison != 0)
                        return comparison;
                }
                return x.Length.CompareTo(y.Length);
            }
        }

        private sealed class RouteEqualityComparer : IEqualityComparer<ImmutableArray<int>>
        {
            internal static RouteEqualityComparer Instance { get; } = new RouteEqualityComparer();

            public bool Equals(ImmutableArray<int> x, ImmutableArray<int> y)
            {
                return x.AsSpan().SequenceEqual(y.AsSpan());
            }

            public int GetHashCode(ImmutableArray<int> route)
            {
                HashCode hashCode = new HashCode();
                foreach (int nodeIndex in route)
                    hashCode.Add(nodeIndex);
                return hashCode.ToHashCode();
            }
        }

        private static TrackRouteSearchResult BuildTrackRouteSearchResult(ImmutableArray<int> routeNodeIndexes, TrackWorld trackWorld, PathRouteResolverOptions options, double cost)
        {
            ResolvedRouteCandidate candidate = BuildRouteCandidate(routeNodeIndexes, trackWorld, options, cost, null, null,
                ImmutableArray<TrackRouteTraversal>.Empty);
            return new TrackRouteSearchResult(candidate.RouteNodeIndexes, candidate.TrackVectorNodeIndexes,
                candidate.GeneratedIntermediaryAnchors, false, ImmutableArray.Create(candidate));
        }

        private static double RouteSearchNodeCost(TrackWorld trackWorld, TrackNodeBase trackNode, int trackNodeIndex,
            PathRouteAnchor targetAnchor, TrackNodeConnector incomingConnector)
        {
            if (trackNode is VectorNode vectorNode)
            {
                double length = trackWorld.VectorNodeLength(vectorNode);
                if (trackNodeIndex == targetAnchor.TrackNodeIndex && incomingConnector != null)
                {
                    double targetPosition = AnchorPosition(targetAnchor, vectorNode, trackWorld);
                    if (!double.IsNaN(targetPosition))
                    {
                        return incomingConnector.Direction == TrackDirection.Ahead
                            ? targetPosition
                            : Math.Max(0.0, length - targetPosition);
                    }
                }
                return length > 0.0 ? length : 1.0;
            }

            return 1.0;
        }

        // Computes the effective sparse-search cost cap. The configured distance is a floor that is expanded to
        // allow a realistic detour relative to the straight-line separation of the anchors, so a long but simple
        // span is not rejected by the default distance. Co-located anchors keep the configured distance.
        private static double EffectiveSearchDistance(PathRouteAnchor fromAnchor, PathRouteAnchor toAnchor, PathRouteResolverOptions options)
        {
            double configured = options?.MaximumSparseSearchDistance ?? PathRouteResolverOptions.DefaultMaximumSparseSearchDistance;
            double directDistance = Math.Sqrt(WorldLocation.GetDistanceSquared2D(fromAnchor.Location, toAnchor.Location));
            return Math.Max(configured, directDistance * SparseSearchDetourFactor);
        }

        private readonly record struct TrackRouteSearchConstraints
        {
            internal static TrackRouteSearchConstraints Default { get; } = new TrackRouteSearchConstraints(
                null, ImmutableHashSet<TrackRouteTraversal>.Empty, false);

            internal TrackDirection? DepartureDirection { get; }

            internal ImmutableHashSet<TrackRouteTraversal> MainRouteTraversals { get; }

            internal bool RequireAlternativeRoute { get; }

            internal TrackRouteSearchConstraints(TrackDirection? departureDirection,
                ImmutableHashSet<TrackRouteTraversal> mainRouteTraversals, bool requireAlternativeRoute)
            {
                DepartureDirection = departureDirection;
                MainRouteTraversals = mainRouteTraversals ?? ImmutableHashSet<TrackRouteTraversal>.Empty;
                RequireAlternativeRoute = requireAlternativeRoute;
            }

            internal TrackRouteSearchConstraints WithDepartureDirection(TrackDirection? departureDirection)
            {
                return new TrackRouteSearchConstraints(departureDirection, MainRouteTraversals, RequireAlternativeRoute);
            }
        }

        private sealed class TrackRouteSearchWorkspace
        {
            private readonly Dictionary<TrackRouteSearchState, double> costs = new Dictionary<TrackRouteSearchState, double>();
            private readonly PriorityQueue<TrackRouteSearchState, double> pendingStates = new PriorityQueue<TrackRouteSearchState, double>();

            internal IReadOnlyDictionary<TrackRouteSearchState, double> Costs => costs;

            internal Dictionary<TrackRouteSearchState, List<TrackRouteSearchState>> OptimalPredecessors { get; } =
                new Dictionary<TrackRouteSearchState, List<TrackRouteSearchState>>();

            internal TrackRouteSearchWorkspace(TrackRouteSearchState startState)
            {
                costs[startState] = 0.0;
                pendingStates.Enqueue(startState, 0.0);
            }

            internal double Cost(TrackRouteSearchState state) => costs[state];

            internal bool TryDequeue(out TrackRouteSearchState state, out double cost)
            {
                return pendingStates.TryDequeue(out state, out cost);
            }

            internal void Relax(TrackRouteSearchState nextState, TrackRouteSearchState currentState, double nextCost)
            {
                if (!costs.TryGetValue(nextState, out double existingCost) || nextCost + CostEpsilon < existingCost)
                {
                    costs[nextState] = nextCost;
                    OptimalPredecessors[nextState] = new List<TrackRouteSearchState> { currentState };
                    pendingStates.Enqueue(nextState, nextCost);
                }
                else if (Math.Abs(nextCost - existingCost) <= CostEpsilon)
                {
                    if (!OptimalPredecessors.TryGetValue(nextState, out List<TrackRouteSearchState> predecessors))
                        OptimalPredecessors[nextState] = predecessors = new List<TrackRouteSearchState>();
                    if (!predecessors.Contains(currentState))
                        predecessors.Add(currentState);
                }
            }
        }

        private readonly record struct TrackRouteSearchState
        {
            /// <summary>Track node visited immediately before the current node, or -1 for the initial state.</summary>
            internal int PreviousNodeIndex { get; }

            /// <summary>Track node represented by this search state.</summary>
            internal int CurrentNodeIndex { get; }

            /// <summary>Connector used to leave the previous node, or -1 for the initial state.</summary>
            internal int PreviousConnectorIndex { get; }

            /// <summary>Connector used to enter the current node, or -1 when no reciprocal connector is known.</summary>
            internal int IncomingConnectorIndex { get; }

            /// <summary>Whether the route has traversed a physical connection outside the bounded main route.</summary>
            internal bool AlternativeEdgeUsed { get; }

            internal TrackRouteSearchState(int previousNodeIndex, int currentNodeIndex, int previousConnectorIndex,
                int incomingConnectorIndex, bool alternativeEdgeUsed)
            {
                PreviousNodeIndex = previousNodeIndex;
                CurrentNodeIndex = currentNodeIndex;
                PreviousConnectorIndex = previousConnectorIndex;
                IncomingConnectorIndex = incomingConnectorIndex;
                AlternativeEdgeUsed = alternativeEdgeUsed;
            }
        }

        private sealed record TrackRouteSearchResult
        {
            /// <summary>Indexes of the track database route nodes forming the resolved route.</summary>
            internal ImmutableArray<int> RouteNodeIndexes { get; private init; }

            /// <summary>Indexes of the track vector nodes traversed by the resolved route.</summary>
            internal ImmutableArray<int> TrackVectorNodeIndexes { get; private init; }

            /// <summary>Anchors synthesized while resolving the route between the given anchors.</summary>
            internal ImmutableArray<PathRouteAnchor> GeneratedIntermediaryAnchors { get; private init; }

            /// <summary>Indicates more than one equally plausible route was found.</summary>
            internal bool Ambiguous { get; private init; }

            /// <summary>All candidate routes evaluated during the search.</summary>
            internal ImmutableArray<ResolvedRouteCandidate> Candidates { get; private init; }

            internal bool Resolved => !RouteNodeIndexes.IsEmpty;

            internal static TrackRouteSearchResult Unresolved { get; } = new TrackRouteSearchResult(
                ImmutableArray<int>.Empty, ImmutableArray<int>.Empty, ImmutableArray<PathRouteAnchor>.Empty, false,
                ImmutableArray<ResolvedRouteCandidate>.Empty);

            internal TrackRouteSearchResult(ImmutableArray<int> routeNodeIndexes, ImmutableArray<int> trackVectorNodeIndexes,
                ImmutableArray<PathRouteAnchor> generatedIntermediaryAnchors, bool ambiguous, ImmutableArray<ResolvedRouteCandidate> candidates)
            {
                RouteNodeIndexes = routeNodeIndexes.IsDefault ? ImmutableArray<int>.Empty : routeNodeIndexes;
                TrackVectorNodeIndexes = trackVectorNodeIndexes.IsDefault ? ImmutableArray<int>.Empty : trackVectorNodeIndexes;
                GeneratedIntermediaryAnchors = generatedIntermediaryAnchors.IsDefault ? ImmutableArray<PathRouteAnchor>.Empty : generatedIntermediaryAnchors;
                Ambiguous = ambiguous;
                Candidates = candidates.IsDefault ? ImmutableArray<ResolvedRouteCandidate>.Empty : candidates;
            }
        }

        private static bool IsInRange(int nodeIndex, int nodeCount) => nodeIndex >= 0 && nodeIndex < nodeCount;
    }
}
