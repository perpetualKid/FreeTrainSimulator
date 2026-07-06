using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Converts resolved path routes into persisted <see cref="PathModel"/> instances without mutating inputs.
    /// </summary>
    public static class PathModelRouteGenerator
    {
        /// <summary>
        /// Generates a main-path <see cref="PathModel"/> from an existing source model and route resolution.
        /// Passing branches present in the resolution are ignored; the result contains the main route only.
        /// </summary>
        public static PathGenerationResult GenerateMainPath(PathModel sourcePath, PathRouteResolution resolution,
            TrackWorld trackWorld, PathRouteResolverOptions options)
        {
            return Generate(sourcePath, resolution, trackWorld, options, includePassingBranches: false);
        }

        /// <summary>
        /// Generates a <see cref="PathModel"/> from an existing source model and route resolution, weaving any
        /// resolved passing branches back into the generated graph. Paths whose passing branches cannot be
        /// represented (non-rejoining, unresolved, or nested/overlapping) are refused via
        /// <see cref="PathGenerationResult.Failed"/> so the caller can preserve the authored path unchanged.
        /// </summary>
        public static PathGenerationResult GeneratePath(PathModel sourcePath, PathRouteResolution resolution,
            TrackWorld trackWorld, PathRouteResolverOptions options)
        {
            return Generate(sourcePath, resolution, trackWorld, options, includePassingBranches: true);
        }

        private static PathGenerationResult Generate(PathModel sourcePath, PathRouteResolution resolution,
            TrackWorld trackWorld, PathRouteResolverOptions options, bool includePassingBranches)
        {
            ArgumentNullException.ThrowIfNull(sourcePath);
            ArgumentNullException.ThrowIfNull(resolution);
            ArgumentNullException.ThrowIfNull(options);

            if (resolution.MainRoute == null)
                return PathGenerationResult.Failed("Cannot generate a path because the resolver did not produce a main route.", sourcePath, resolution.Diagnostics);

            if (!resolution.IsValid)
                return PathGenerationResult.Failed("Cannot generate a path while resolver errors or fatal diagnostics remain.", sourcePath, resolution.Diagnostics);

            if (resolution.Diagnostics.Any(diagnostic => diagnostic.Code == PathRouteDiagnosticCode.AmbiguousRoute) && !options.AllowMainRouteFirstTieBreaking)
                return PathGenerationResult.Failed("Cannot generate a path while ambiguous route diagnostics remain.", sourcePath, resolution.Diagnostics);

            ImmutableArray<ResolvedPathSpan> spans = resolution.MainRoute.Spans;
            if (spans.IsDefaultOrEmpty)
                return PathGenerationResult.Failed("Cannot generate a path because the resolved main route has no spans.", sourcePath, resolution.Diagnostics);

            if (!TryValidateSpans(spans, options, out string spanFailure))
                return PathGenerationResult.Failed(spanFailure, sourcePath, resolution.Diagnostics);

            ImmutableArray<PathRouteAnchor> orderedAnchors = BuildOrderedAnchors(resolution, spans);
            if (orderedAnchors.Length < 2)
                return PathGenerationResult.Failed("Cannot generate a path because fewer than two route anchors were resolved.", sourcePath, resolution.Diagnostics);

            ImmutableArray<PathNode> sourceNodes = sourcePath.PathNodes.IsDefault ? ImmutableArray<PathNode>.Empty : sourcePath.PathNodes;

            // Emit the main-route anchors as sequential path nodes and record, for each authored anchor, the
            // generated node index it received. The map lets passing-branch generation (and any future cross-link
            // rebuild) wire NextSidingNode/rejoin links back to the correct generated nodes rather than relying on
            // a naive 0..N linear numbering.
            GeneratedNodeBuilder builder = new GeneratedNodeBuilder(orderedAnchors.Length);
            for (int i = 0; i < orderedAnchors.Length; i++)
            {
                PathRouteAnchor anchor = orderedAnchors[i];
                PathNode sourceNode = IsAuthoredAnchor(anchor, sourceNodes.Length) ? sourceNodes[anchor.AuthoredNodeIndex] : null;
                PathNodeType nodeType = BuildNodeType(anchor, sourceNode, i, orderedAnchors.Length, trackWorld);
                int nextMainNode = i == orderedAnchors.Length - 1 ? -1 : i + 1;
                builder.Add(anchor, new PathNode(anchor.Location)
                {
                    NodeType = nodeType,
                    NodeIndex = anchor.TrackNodeIndex,
                    NextMainNode = nextMainNode,
                    NextSidingNode = -1,
                    WaitInfo = sourceNode?.WaitInfo,
                });
            }

            string message = "Generated path from resolved main route.";
            if (includePassingBranches && !resolution.PassingRoutes.IsDefaultOrEmpty)
            {
                if (!TryEmitPassingBranches(resolution, sourceNodes, trackWorld, options, builder, out string passingFailure))
                    return PathGenerationResult.Failed(passingFailure, sourcePath, resolution.Diagnostics);

                message = "Generated path from resolved main and passing routes.";
            }

            PathModel generatedPath = new PathModel(sourcePath)
            {
                PathNodes = builder.ToImmutableNodes(),
            };

            return PathGenerationResult.Succeeded(message, generatedPath,
                resolution.Diagnostics, builder.ChangedNodeIndexes());
        }

        private static bool TryValidateSpans(ImmutableArray<ResolvedPathSpan> spans, PathRouteResolverOptions options, out string failure)
        {
            foreach (ResolvedPathSpan span in spans)
            {
                if (span.Status == PathRouteSpanStatus.Ambiguous && !options.AllowMainRouteFirstTieBreaking)
                {
                    failure = "Cannot generate a path from an ambiguous route span.";
                    return false;
                }
                if (span.Status != PathRouteSpanStatus.Resolved)
                {
                    failure = "Cannot generate a path because at least one route span is unresolved.";
                    return false;
                }
            }
            failure = null;
            return true;
        }

        // Weaves each resolved passing branch into the already-emitted main graph. Interior passing anchors (the
        // nodes strictly between the branch start and its rejoin) are appended as new generated nodes and chained
        // through NextSidingNode, mirroring how the resolver walks a passing branch. The branch-start main node's
        // NextSidingNode is repointed to the first interior node (or directly to the rejoin node when the branch
        // has no interior nodes), and the last interior node's NextSidingNode rejoins the main graph.
        private static bool TryEmitPassingBranches(PathRouteResolution resolution, ImmutableArray<PathNode> sourceNodes,
            TrackWorld trackWorld, PathRouteResolverOptions options, GeneratedNodeBuilder builder, out string failure)
        {
            failure = null;
            foreach (ResolvedPathRoute passingRoute in resolution.PassingRoutes.OrderBy(route => route.StartNodeIndex))
            {
                if (passingRoute.Spans.IsDefaultOrEmpty)
                {
                    failure = "Cannot generate a path because a passing branch has no resolved spans.";
                    return false;
                }

                if (!TryValidateSpans(passingRoute.Spans, options, out failure))
                    return false;

                if (!builder.TryGetGenerated(passingRoute.StartNodeIndex, out int branchStartGenerated))
                {
                    failure = "Cannot generate a path because a passing branch does not start on the main route.";
                    return false;
                }

                if (!builder.TryGetGenerated(passingRoute.EndNodeIndex, out int rejoinGenerated))
                {
                    failure = "Cannot generate a path because a passing branch does not rejoin the main route.";
                    return false;
                }

                if (!TryEmitPassingBranch(passingRoute, resolution, sourceNodes, trackWorld, builder, branchStartGenerated, rejoinGenerated, out failure))
                    return false;
            }
            return true;
        }

        private static bool TryEmitPassingBranch(ResolvedPathRoute passingRoute, PathRouteResolution resolution,
            ImmutableArray<PathNode> sourceNodes, TrackWorld trackWorld, GeneratedNodeBuilder builder,
            int branchStartGenerated, int rejoinGenerated, out string failure)
        {
            failure = null;

            // Collect the interior anchors: every span's generated intermediaries plus its target authored anchor,
            // excluding the final span target which is the rejoin node already present on the main route.
            ImmutableArray<ResolvedPathSpan> spans = passingRoute.Spans;
            List<PathRouteAnchor> interiorAnchors = new List<PathRouteAnchor>();
            for (int i = 0; i < spans.Length; i++)
            {
                interiorAnchors.AddRange(spans[i].GeneratedIntermediaryAnchors);
                if (i < spans.Length - 1)
                    interiorAnchors.Add(resolution.AuthoredNodeAnchors[spans[i].ToNodeIndex]);
            }

            int previousGenerated = branchStartGenerated;
            foreach (PathRouteAnchor anchor in interiorAnchors)
            {
                if (IsAuthoredAnchor(anchor, sourceNodes.Length) && builder.TryGetGenerated(anchor.AuthoredNodeIndex, out _))
                {
                    failure = "Cannot generate a path because a passing branch reuses a node already on another route.";
                    return false;
                }

                PathNode sourceNode = IsAuthoredAnchor(anchor, sourceNodes.Length) ? sourceNodes[anchor.AuthoredNodeIndex] : null;
                int generatedIndex = builder.NextIndex;
                builder.Add(anchor, new PathNode(anchor.Location)
                {
                    NodeType = BuildPassingNodeType(anchor, sourceNode, trackWorld),
                    NodeIndex = anchor.TrackNodeIndex,
                    NextMainNode = -1,
                    NextSidingNode = -1,
                    WaitInfo = sourceNode?.WaitInfo,
                });

                builder.SetSidingLink(previousGenerated, generatedIndex);
                previousGenerated = generatedIndex;
            }

            builder.SetSidingLink(previousGenerated, rejoinGenerated);
            return true;
        }

        private static PathNodeType BuildPassingNodeType(PathRouteAnchor anchor, PathNode sourceNode, TrackWorld trackWorld)
        {
            PathNodeType nodeType = sourceNode?.NodeType ?? anchor.NodeType;
            nodeType &= ~(PathNodeType.Start | PathNodeType.End | PathNodeType.Intermediate);
            nodeType |= PathNodeType.Intermediate;

            if (trackWorld?.TrackNodeByIndex(anchor.TrackNodeIndex) is JunctionNode)
                nodeType |= PathNodeType.Junction;

            return nodeType;
        }

        private static ImmutableArray<PathRouteAnchor> BuildOrderedAnchors(PathRouteResolution resolution, ImmutableArray<ResolvedPathSpan> spans)
        {
            ImmutableArray<PathRouteAnchor>.Builder builder = ImmutableArray.CreateBuilder<PathRouteAnchor>();
            for (int i = 0; i < spans.Length; i++)
            {
                ResolvedPathSpan span = spans[i];
                if (i == 0)
                    builder.Add(resolution.AuthoredNodeAnchors[span.FromNodeIndex]);

                builder.AddRange(span.GeneratedIntermediaryAnchors);
                builder.Add(resolution.AuthoredNodeAnchors[span.ToNodeIndex]);
            }
            return builder.ToImmutable();
        }

        private static PathNodeType BuildNodeType(PathRouteAnchor anchor, PathNode sourceNode, int nodeIndex, int nodeCount, TrackWorld trackWorld)
        {
            PathNodeType nodeType = sourceNode?.NodeType ?? anchor.NodeType;
            nodeType &= ~(PathNodeType.Start | PathNodeType.End | PathNodeType.Intermediate);

            if (nodeIndex == 0)
                nodeType |= PathNodeType.Start;
            else if (nodeIndex == nodeCount - 1)
                nodeType |= PathNodeType.End;
            else
                nodeType |= PathNodeType.Intermediate;

            if (trackWorld?.TrackNodeByIndex(anchor.TrackNodeIndex) is JunctionNode)
                nodeType |= PathNodeType.Junction;

            return nodeType;
        }

        private static bool IsAuthoredAnchor(PathRouteAnchor anchor, int sourceNodeCount)
        {
            return anchor.AuthoredNodeIndex >= 0 && anchor.AuthoredNodeIndex < sourceNodeCount;
        }

        /// <summary>
        /// Accumulates generated <see cref="PathNode"/> instances in emission order while recording,
        /// for every authored anchor, the generated node index it received. The authored-to-generated
        /// map allows passing-branch generation to wire NextSidingNode and rejoin links to the correct
        /// generated nodes instead of relying on the input node numbering.
        /// </summary>
        private sealed class GeneratedNodeBuilder
        {
            private readonly List<PathNode> nodes;
            private readonly Dictionary<int, int> authoredToGenerated;

            public GeneratedNodeBuilder(int capacity)
            {
                nodes = new List<PathNode>(capacity);
                authoredToGenerated = new Dictionary<int, int>(capacity);
            }

            /// <summary>Generated node index that the next added node will receive.</summary>
            public int NextIndex => nodes.Count;

            /// <summary>Adds a generated node, mapping its authored anchor index when present.</summary>
            public void Add(PathRouteAnchor anchor, PathNode node)
            {
                if (anchor.AuthoredNodeIndex >= 0)
                    authoredToGenerated[anchor.AuthoredNodeIndex] = nodes.Count;
                nodes.Add(node);
            }

            /// <summary>Attempts to resolve the generated node index for an authored node index.</summary>
            public bool TryGetGenerated(int authoredNodeIndex, out int generatedNodeIndex)
            {
                return authoredToGenerated.TryGetValue(authoredNodeIndex, out generatedNodeIndex);
            }

            /// <summary>Replaces the generated node at <paramref name="generatedNodeIndex"/> with a re-linked copy.</summary>
            public void SetSidingLink(int generatedNodeIndex, int nextSidingNode)
            {
                nodes[generatedNodeIndex] = nodes[generatedNodeIndex] with { NextSidingNode = nextSidingNode };
            }

            /// <summary>Replaces the generated node at <paramref name="generatedNodeIndex"/> with a re-linked copy.</summary>
            public void SetMainLink(int generatedNodeIndex, int nextMainNode)
            {
                nodes[generatedNodeIndex] = nodes[generatedNodeIndex] with { NextMainNode = nextMainNode };
            }

            public ImmutableArray<PathNode> ToImmutableNodes() => nodes.ToImmutableArray();

            public ImmutableArray<int> ChangedNodeIndexes()
            {
                ImmutableArray<int>.Builder builder = ImmutableArray.CreateBuilder<int>(nodes.Count);
                for (int i = 0; i < nodes.Count; i++)
                    builder.Add(i);
                return builder.MoveToImmutable();
            }
        }
    }
}
