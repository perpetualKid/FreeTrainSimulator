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
            return ResolveSelectedRoutes(pathModel, trackWorld, options, ImmutableDictionary<int, ResolvedRouteCandidate>.Empty, cancellationToken);
        }

        /// <summary>
        /// Resolves a path while constraining selected main-route spans to the chosen track-node sequences.
        /// Selection keys identify authored main-route span sources, not terminal or siding-only nodes.
        /// Unselected spans retain compatible shortest alternatives; contradictory choices remain unresolved.
        /// </summary>
        public static PathRouteResolution ResolveSelectedRoutes(PathModel pathModel, TrackWorld trackWorld,
            PathRouteResolverOptions options, IReadOnlyDictionary<int, ResolvedRouteCandidate> selectedCandidates,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(selectedCandidates);

            List<PathRouteDiagnostic> diagnostics = new List<PathRouteDiagnostic>();
            ImmutableArray<PathNode> pathNodes = pathModel.PathNodes.IsDefault ? ImmutableArray<PathNode>.Empty : pathModel.PathNodes;
            foreach (KeyValuePair<int, ResolvedRouteCandidate> selection in selectedCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(selection.Value);
                ArgumentOutOfRangeException.ThrowIfNegative(selection.Key, nameof(selectedCandidates));
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(selection.Key, pathNodes.Length, nameof(selectedCandidates));
            }

            if (pathNodes.IsEmpty)
            {
                diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Fatal, PathRouteDiagnosticCode.EmptyPath, "Path has no authored nodes.", "Add a start node and an end node."));

                return new PathRouteResolution(null, diagnostics.ToImmutableArray());
            }

            cancellationToken.ThrowIfCancellationRequested();

            int startNodeIndex = FindFirstNodeOfType(pathNodes, PathNodeType.Start);
            int endNodeIndex = FindLastNodeOfType(pathNodes, PathNodeType.End);

            if (selectedCandidates.Count > 0)
            {
                HashSet<int> mainSpanSources = new HashSet<int>();
                int current = startNodeIndex;

                while (IsInRange(current, pathNodes.Length) && IsInRange(pathNodes[current].NextMainNode, pathNodes.Length) && mainSpanSources.Add(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    current = pathNodes[current].NextMainNode;
                }

                foreach (int sourceIndex in selectedCandidates.Keys)
                {
                    if (!mainSpanSources.Contains(sourceIndex))
                        throw new ArgumentOutOfRangeException(nameof(selectedCandidates));
                }
            }

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
                    static node => node.NextMainNode, TrackRouteSearchConstraints.Default, null, selectedCandidates, cancellationToken)
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

            return trackNode switch
            {
                JunctionNode junctionNode => ResolveNonVectorTrackNodeIndexByLocation<JunctionNode>(node.Location, trackWorld.TrackDatabase, out _) == junctionNode.NodeIndex,
                EndNode endNode => ResolveNonVectorTrackNodeIndexByLocation<EndNode>(node.Location, trackWorld.TrackDatabase, out _) == endNode.NodeIndex,
                _ => false,
            };
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

        private static ResolvedPathRoute BuildRoute(PathRouteBranchKind branchKind, ImmutableArray<PathNode> pathNodes, ImmutableArray<PathRouteAnchor> anchors, TrackWorld trackWorld, PathRouteResolverOptions options,
            List<PathRouteDiagnostic> diagnostics, int startNodeIndex, Func<PathNode, int> nextNodeSelector, TrackRouteSearchConstraints searchConstraints, ResolvedPathRoute mainRoute,
            IReadOnlyDictionary<int, ResolvedRouteCandidate> selectedCandidates, CancellationToken cancellationToken)
        {
            List<RouteSpanLayer> layers = new List<RouteSpanLayer>();
            HashSet<TrackRouteArrival?> frontier = new HashSet<TrackRouteArrival?> { searchConstraints.Arrival };

            if (mainRoute != null && mainRoute.Spans.TakeWhile(span => span.FromNodeIndex != startNodeIndex).Any(span => span.Candidates.IsEmpty))
                frontier.Clear();

            HashSet<int> visited = new HashSet<int>();
            int currentNodeIndex = startNodeIndex;
            int endNodeIndex = startNodeIndex;

            while (currentNodeIndex >= 0 && currentNodeIndex < pathNodes.Length && visited.Add(currentNodeIndex))
            {
                cancellationToken.ThrowIfCancellationRequested();

                int nextNodeIndex = nextNodeSelector(pathNodes[currentNodeIndex]);

                if (!IsInRange(nextNodeIndex, pathNodes.Length))
                    break;

                RouteSpanLayer layer = new RouteSpanLayer(currentNodeIndex, nextNodeIndex);
                
                if (trackWorld != null && anchors[currentNodeIndex].HasTrackAnchor && anchors[nextNodeIndex].HasTrackAnchor)
                {
                    foreach (TrackRouteArrival? arrival in frontier)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        TrackRouteArrival? departure = DepartureArrival(arrival, pathNodes[currentNodeIndex]);
                        TrackRouteSearchResult result = FindTrackRoute(anchors[currentNodeIndex], anchors[nextNodeIndex], trackWorld, options, searchConstraints.WithArrival(departure), cancellationToken);

                        foreach (ResolvedRouteCandidate candidate in result.Candidates)
                        {
                            if (!selectedCandidates.TryGetValue(currentNodeIndex, out ResolvedRouteCandidate selectedCandidate)
                                || candidate.RouteNodeIndexes.AsSpan().SequenceEqual(selectedCandidate.RouteNodeIndexes.AsSpan()))
                            {
                                layer.Transitions.Add(new RouteBoundaryTransition(arrival, candidate));
                            }
                        }
                    }

                    // This is a local shortest frontier, not a total-path optimization. Once a boundary's
                    // longer approaches are discarded, a later failure must request a via, not revive them.
                    if (layer.Transitions.Count > 0)
                    {
                        double minimumCost = layer.Transitions.Min(transition => transition.Candidate.Cost);
                        layer.Transitions.RemoveAll(transition => transition.Candidate.Cost > minimumCost + CostEpsilon);
                    }
                }
                layers.Add(layer);
                frontier = layer.Transitions.Select(transition => transition.Candidate.Arrival).ToHashSet();
                endNodeIndex = nextNodeIndex;
                currentNodeIndex = nextNodeIndex;
            }

            // A branch must join the selected coherent main chain, not merely reach its anchor node.
            if (mainRoute != null && layers.Count > 0 && trackWorld != null)
                layers[^1].Transitions.RemoveAll(transition => !CanRejoinSelectedMain(transition.Candidate.Arrival, endNodeIndex, mainRoute, pathNodes, anchors, trackWorld, cancellationToken));

            // Keep the longest reachable prefix on failure, but never restart a failed prefix unconstrained.
            // Backward reachability prunes dead equal-cost approaches without multiplying complete chains.
            int lastReachable = layers.FindIndex(layer => layer.Transitions.Count == 0);
            
            if (lastReachable < 0)
                lastReachable = layers.Count;

            for (int index = lastReachable - 2; index >= 0; index--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                HashSet<TrackRouteArrival?> viable = layers[index + 1].Transitions.Select(transition => transition.SourceArrival).ToHashSet();
                layers[index].Transitions.RemoveAll(transition => !viable.Contains(transition.Candidate.Arrival));
            }

            List<ResolvedPathSpan> spans = new List<ResolvedPathSpan>();
            TrackRouteArrival? selectedArrival = searchConstraints.Arrival;
            foreach (RouteSpanLayer layer in layers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RouteBoundaryTransition selected = layer.Transitions.Where(transition => transition.SourceArrival == selectedArrival).OrderBy(transition => transition.Candidate.RouteNodeIndexes, RouteComparer.Instance).FirstOrDefault();
                ImmutableArray<ResolvedRouteCandidate> candidates = selected == null
                    ? ImmutableArray<ResolvedRouteCandidate>.Empty
                    : layer.Transitions.OrderBy(transition => ReferenceEquals(transition, selected) ? 0 : 1)
                        .ThenBy(transition => transition.Candidate.RouteNodeIndexes, RouteComparer.Instance)
                        .GroupBy(transition => transition.Candidate.RouteNodeIndexes, RouteEqualityComparer.Instance)
                        .Select(group => group.First().Candidate).ToImmutableArray();
                spans.Add(PresentSpan(layer.FromNodeIndex, layer.ToNodeIndex, candidates, trackWorld, options, diagnostics));
                selectedArrival = selected?.Candidate.Arrival;
            }
            return new ResolvedPathRoute(branchKind, startNodeIndex, endNodeIndex, spans.ToImmutableArray());
        }

        private static TrackRouteArrival? DepartureArrival(TrackRouteArrival? arrival, PathNode node)
        {
            return arrival.HasValue && arrival.Value.Direction.HasValue && node.NodeType.Includes(PathNodeType.Reversal)
                ? arrival.Value with { Direction = arrival.Value.Direction.Value.Reverse() }
                : arrival;
        }

        private static bool CanRejoinSelectedMain(TrackRouteArrival? arrival, int rejoinNodeIndex, ResolvedPathRoute mainRoute, ImmutableArray<PathNode> pathNodes, ImmutableArray<PathRouteAnchor> anchors, TrackWorld trackWorld,
            CancellationToken cancellationToken)
        {
            foreach (ResolvedPathSpan span in mainRoute.Spans.SkipWhile(span => span.FromNodeIndex != rejoinNodeIndex))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ResolvedRouteCandidate candidate = span.Candidates.FirstOrDefault();

                if (candidate == null)
                    return false;
                
                arrival = DepartureArrival(arrival, pathNodes[span.FromNodeIndex]);
                
                if (!candidate.PhysicalTraversals.IsEmpty)
                {
                    TrackRouteTraversal departure = candidate.PhysicalTraversals[0];
                    TrackRouteSearchState state = SeedState(departure.FromNodeIndex, arrival, true);
                    TrackNodeConnector connector = trackWorld.TrackDatabase.TrackNodeConnectors[departure.FromNodeIndex].TrackNodeConnectors[departure.FromConnectorIndex];
                    return CanTraverse(state, departure.FromConnectorIndex, connector, trackWorld.TrackDatabase, arrival?.Direction);
                }
                
                if (!TryDirectArrival(anchors[span.FromNodeIndex], anchors[span.ToNodeIndex], trackWorld, arrival, out arrival))
                    return false;
            }
            return true;
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
                    TrackRouteArrival? arrival = mainRoute?.Spans.LastOrDefault(span => span.ToNodeIndex == i)?.Candidates.FirstOrDefault()?.Arrival;
                    TrackRouteSearchConstraints searchConstraints = new TrackRouteSearchConstraints(arrival, mainRouteEdges, requireAlternativeRoute);
                    ResolvedPathRoute route = BuildRoute(PathRouteBranchKind.Passing, pathNodes, anchors, trackWorld, options, diagnostics, i,
                        static pathNode => pathNode.NextSidingNode, searchConstraints, mainRoute, ImmutableDictionary<int, ResolvedRouteCandidate>.Empty, cancellationToken);
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

        private static ResolvedPathSpan PresentSpan(int fromNodeIndex, int toNodeIndex, ImmutableArray<ResolvedRouteCandidate> candidates, TrackWorld trackWorld, PathRouteResolverOptions options, List<PathRouteDiagnostic> diagnostics)
        {
            if (trackWorld == null)
                return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.NotResolved);

            if (!candidates.IsEmpty)
            {
                ResolvedRouteCandidate primary = candidates[0];
                if (candidates.Length > 1 && options.AllowMainRouteFirstTieBreaking)
                {
                    diagnostics.Add(new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.AmbiguousRoute,
                        $"Path span from node {fromNodeIndex} to node {toNodeIndex} has {candidates.Length} equal-cost routes; the deterministic first route was selected.",
                        fromNodeIndex, toNodeIndex, "Choose a candidate route or add an explicit via node if a different route is intended."));
                    return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Resolved,
                        primary.TrackVectorNodeIndexes, primary.GeneratedIntermediaryAnchors, candidates);
                }

                if (candidates.Length > 1)
                {
                    diagnostics.Add(new PathRouteDiagnostic(options.TreatAmbiguityAsError ? PathRouteDiagnosticSeverity.Error : PathRouteDiagnosticSeverity.Warning,
                        PathRouteDiagnosticCode.AmbiguousRoute,
                        $"Path span from node {fromNodeIndex} to node {toNodeIndex} has {candidates.Length} equal-cost routes.",
                        fromNodeIndex, toNodeIndex, "Choose a candidate route or add an explicit via node to choose the intended route."));
                    return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Ambiguous,
                        primary.TrackVectorNodeIndexes, primary.GeneratedIntermediaryAnchors, candidates);
                }

                return new ResolvedPathSpan(fromNodeIndex, toNodeIndex, PathRouteSpanStatus.Resolved,
                    primary.TrackVectorNodeIndexes, primary.GeneratedIntermediaryAnchors, candidates);
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
                if (!TryDirectArrival(fromAnchor, toAnchor, trackWorld, searchConstraints.Arrival, out TrackRouteArrival? arrival))
                    return TrackRouteSearchResult.Unresolved;

                ResolvedRouteCandidate direct = BuildRouteCandidate(ImmutableArray.Create(fromAnchor.TrackNodeIndex),
                    trackWorld, options, 0.0, null, null, ImmutableArray<TrackRouteTraversal>.Empty) with { Arrival = arrival };
                return new TrackRouteSearchResult(direct.RouteNodeIndexes, direct.TrackVectorNodeIndexes,
                    direct.GeneratedIntermediaryAnchors, false, ImmutableArray.Create(direct));
            }

            double maximumCost = EffectiveSearchDistance(fromAnchor, toAnchor, options);
            TrackRouteSearchState startState = SeedState(fromAnchor.TrackNodeIndex, searchConstraints.Arrival,
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
                            currentState == startState ? searchConstraints.DepartureDirection : null))
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
                startState, minimumTargetStates, fromAnchor, toAnchor, trackWorld, options, minimumCost,
                searchConstraints.DepartureDirection, cancellationToken);
            return candidates.IsEmpty
                ? TrackRouteSearchResult.Unresolved
                : new TrackRouteSearchResult(candidates[0].RouteNodeIndexes, candidates[0].TrackVectorNodeIndexes, candidates[0].GeneratedIntermediaryAnchors, candidates.Length > 1, candidates);
        }

        // Walks the optimal-predecessor sets backwards from the target to enumerate every distinct equal-cost
        // route. Predecessors are visited in ascending track node order and the resulting candidates are ordered
        // lexicographically, so both the selected route and a candidate index stay stable across resolutions.
        private static ImmutableArray<ResolvedRouteCandidate> EnumerateRouteCandidates(Dictionary<TrackRouteSearchState, List<TrackRouteSearchState>> optimalPredecessors, TrackRouteSearchState startState,
            ImmutableArray<TrackRouteSearchState> endStates, PathRouteAnchor fromAnchor, PathRouteAnchor toAnchor, TrackWorld trackWorld, PathRouteResolverOptions options, double cost, TrackDirection? departureDirection, CancellationToken cancellationToken)
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
                .ThenBy(route => route.Select(state => state.PreviousConnectorIndex).ToImmutableArray(), RouteComparer.Instance)
                .Select(route => BuildRouteCandidate(route.Select(state => state.CurrentNodeIndex).ToImmutableArray(), trackWorld, options,
                    cost, fromAnchor, departureDirection, BuildPhysicalTraversals(route)) with
                    {
                        Arrival = TerminalArrival(route[^1], toAnchor, trackWorld),
                    })
                .ToImmutableArray();
        }

        private static TrackRouteSearchState SeedState(int trackNodeIndex, TrackRouteArrival? arrival, bool alternativeEdgeUsed)
        {
            return new TrackRouteSearchState(arrival?.PreviousNodeIndex ?? -1, trackNodeIndex, arrival?.PreviousConnectorIndex ?? -1, arrival?.IncomingConnectorIndex ?? -1, alternativeEdgeUsed);
        }

        private static TrackRouteArrival TerminalArrival(TrackRouteSearchState state, PathRouteAnchor anchor, TrackWorld trackWorld)
        {
            TrackDirection? direction = null;
            ImmutableArray<TrackNodeConnector> connectors = trackWorld.TrackDatabase.TrackNodeConnectors[state.CurrentNodeIndex].TrackNodeConnectors;

            if (trackWorld.TrackDatabase.TrackNodes[state.CurrentNodeIndex] is VectorNode vector && !double.IsNaN(AnchorPosition(anchor, vector, trackWorld)) && IsInRange(state.IncomingConnectorIndex, connectors.Length))
            {
                direction = state.IncomingConnectorIndex switch
                {
                    0 => TrackDirection.Ahead,
                    1 => TrackDirection.Reverse,
                    _ => null,
                };
            }
            return new TrackRouteArrival(state.PreviousNodeIndex, state.PreviousConnectorIndex, state.IncomingConnectorIndex, direction);
        }

        private static bool TryDirectArrival(PathRouteAnchor fromAnchor, PathRouteAnchor toAnchor, TrackWorld trackWorld, TrackRouteArrival? departure, out TrackRouteArrival? arrival)
        {
            arrival = departure;

            if (trackWorld.TrackDatabase.TrackNodes[fromAnchor.TrackNodeIndex] is not VectorNode vector)
                return true;

            double fromPosition = AnchorPosition(fromAnchor, vector, trackWorld);
            double toPosition = AnchorPosition(toAnchor, vector, trackWorld);

            if (double.IsNaN(fromPosition) || double.IsNaN(toPosition) || Math.Abs(toPosition - fromPosition) <= CostEpsilon)
                return true;

            TrackDirection direction = toPosition > fromPosition ? TrackDirection.Ahead : TrackDirection.Reverse;

            if (departure?.Direction.HasValue == true && departure.Value.Direction.Value != direction)
                return false;

            arrival = (departure ?? new TrackRouteArrival(-1, -1, -1, null)) with { Direction = direction };
            return true;
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
            // A seeded vector heading is authoritative even with a previous physical connector: an explicit
            // reversal deliberately allows departure back through that connector, once at the authored boundary.
            if (departureDirection.HasValue && trackDatabase.TrackNodes[currentState.CurrentNodeIndex] is VectorNode)
            {
                ImmutableArray<TrackNodeConnector> departureConnectors = trackDatabase.TrackNodeConnectors[currentState.CurrentNodeIndex].TrackNodeConnectors;
                int requiredConnectorIndex = departureDirection == TrackDirection.Ahead ? 1 : 0;
                return requiredConnectorIndex < departureConnectors.Length && outgoingConnectorIndex == requiredConnectorIndex;
            }

            if (currentState.PreviousNodeIndex < 0)
                return true;
            
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

        private static ResolvedRouteCandidate BuildRouteCandidate(ImmutableArray<int> routeNodeIndexes, TrackWorld trackWorld, PathRouteResolverOptions options, double cost, PathRouteAnchor fromAnchor, 
            TrackDirection? departureDirection, ImmutableArray<TrackRouteTraversal> physicalTraversals)
        {
            ImmutableArray<int>.Builder trackVectorNodeIndexes = ImmutableArray.CreateBuilder<int>();
            ImmutableArray<PathRouteAnchor>.Builder generatedAnchors = ImmutableArray.CreateBuilder<PathRouteAnchor>();
            TrackDatabase trackDatabase = trackWorld.TrackDatabase;
            // Materialize a known departure heading only when leaving the vector. A direct same-vector span
            // must not acquire a witness beyond its target.
            if (options.IncludeGeneratedIntermediaryNodes && departureDirection.HasValue && fromAnchor != null && trackDatabase.TrackNodes[fromAnchor.TrackNodeIndex] is VectorNode departureNode)
            {
                int sectionIndex = departureDirection == TrackDirection.Ahead ? departureNode.VectorSections.Length - 1 : 0;
                VectorSectionNode boundarySection = departureNode.VectorSections[sectionIndex];
                WorldLocation boundary = boundarySection.Location;

                if (trackWorld.SectionGeometry.TryGetValue(boundarySection, out SectionGeometry boundaryGeometry))
                {
                    double inset = Math.Min(boundaryGeometry.Length / 2, WorldLocation.ProximityTolerance * 2);

                    if (fromAnchor.TrackVectorSectionIndex == sectionIndex)
                    {
                        double sourceOffset = boundaryGeometry.DistanceOnSection(fromAnchor.Location);
                        double remaining = departureDirection == TrackDirection.Ahead
                            ? boundaryGeometry.Length - sourceOffset
                            : sourceOffset;
                        inset = Math.Min(inset, Math.Max(0.0, remaining) / 2);
                    }
                    boundary = boundaryGeometry.LocationAt(departureDirection == TrackDirection.Ahead
                        ? boundaryGeometry.Length - inset
                        : inset);
                }
                if (WorldLocation.GetDistanceSquared2D(boundary, fromAnchor.Location) > CostEpsilon * CostEpsilon)
                {
                    generatedAnchors.Add(new PathRouteAnchor(-1, boundary, PathNodeType.Via,
                        departureNode.NodeIndex, sectionIndex));
                }
            }

            for (int i = 0; i < routeNodeIndexes.Length; i++)
            {
                int trackNodeIndex = routeNodeIndexes[i];
                TrackNodeBase trackNode = trackDatabase.TrackNodes[trackNodeIndex];

                if (trackNode is VectorNode)
                    trackVectorNodeIndexes.Add(trackNodeIndex);

                if (options.IncludeGeneratedIntermediaryNodes && i > 0 && i < routeNodeIndexes.Length - 1)
                    AddGeneratedRouteAnchor(generatedAnchors, new PathRouteAnchor(-1, trackNode.Location, PathNodeType.Via, trackNodeIndex, -1), trackDatabase);
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

            /// <summary>Exact arrival at this authored boundary, absent only at a true route start.</summary>
            internal TrackRouteArrival? Arrival { get; }

            internal TrackDirection? DepartureDirection => Arrival?.Direction;

            internal ImmutableHashSet<TrackRouteTraversal> MainRouteTraversals { get; }

            internal bool RequireAlternativeRoute { get; }

            internal TrackRouteSearchConstraints(TrackRouteArrival? arrival,
                ImmutableHashSet<TrackRouteTraversal> mainRouteTraversals, bool requireAlternativeRoute)
            {
                Arrival = arrival;
                MainRouteTraversals = mainRouteTraversals ?? ImmutableHashSet<TrackRouteTraversal>.Empty;
                RequireAlternativeRoute = requireAlternativeRoute;
            }

            internal TrackRouteSearchConstraints WithArrival(TrackRouteArrival? arrival)
            {
                return new TrackRouteSearchConstraints(arrival, MainRouteTraversals, RequireAlternativeRoute);
            }
        }

        private sealed class RouteSpanLayer
        {
            /// <summary>Authored source of this boundary layer.</summary>
            internal int FromNodeIndex { get; }

            /// <summary>Authored target of this boundary layer.</summary>
            internal int ToNodeIndex { get; }

            /// <summary>Shortest physical transitions, retaining predecessor correlation.</summary>
            internal List<RouteBoundaryTransition> Transitions { get; } = new List<RouteBoundaryTransition>();

            internal RouteSpanLayer(int fromNodeIndex, int toNodeIndex)
            {
                FromNodeIndex = fromNodeIndex;
                ToNodeIndex = toNodeIndex;
            }
        }

        private sealed class RouteBoundaryTransition
        {
            /// <summary>Boundary context before applying this source anchor's reversal.</summary>
            internal TrackRouteArrival? SourceArrival { get; }

            /// <summary>Physical route and resulting boundary context.</summary>
            internal ResolvedRouteCandidate Candidate { get; }

            internal RouteBoundaryTransition(TrackRouteArrival? sourceArrival, ResolvedRouteCandidate candidate)
            {
                SourceArrival = sourceArrival;
                Candidate = candidate;
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
