using System;
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
        /// </summary>
        public static PathGenerationResult GenerateMainPath(PathModel sourcePath, PathRouteResolution resolution,
            TrackWorld trackWorld, PathRouteResolverOptions options)
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

            foreach (ResolvedPathSpan span in spans)
            {
                if (span.Status == PathRouteSpanStatus.Ambiguous && !options.AllowMainRouteFirstTieBreaking)
                    return PathGenerationResult.Failed("Cannot generate a path from an ambiguous route span.", sourcePath, resolution.Diagnostics);
                if (span.Status != PathRouteSpanStatus.Resolved)
                    return PathGenerationResult.Failed("Cannot generate a path because at least one route span is unresolved.", sourcePath, resolution.Diagnostics);
            }

            ImmutableArray<PathRouteAnchor> orderedAnchors = BuildOrderedAnchors(resolution, spans);
            if (orderedAnchors.Length < 2)
                return PathGenerationResult.Failed("Cannot generate a path because fewer than two route anchors were resolved.", sourcePath, resolution.Diagnostics);

            ImmutableArray<PathNode> sourceNodes = sourcePath.PathNodes.IsDefault ? ImmutableArray<PathNode>.Empty : sourcePath.PathNodes;
            ImmutableArray<PathNode>.Builder nodes = ImmutableArray.CreateBuilder<PathNode>(orderedAnchors.Length);
            ImmutableArray<int>.Builder changedNodeIndexes = ImmutableArray.CreateBuilder<int>(orderedAnchors.Length);
            for (int i = 0; i < orderedAnchors.Length; i++)
            {
                PathRouteAnchor anchor = orderedAnchors[i];
                PathNode sourceNode = IsAuthoredAnchor(anchor, sourceNodes.Length) ? sourceNodes[anchor.AuthoredNodeIndex] : null;
                PathNodeType nodeType = BuildNodeType(anchor, sourceNode, i, orderedAnchors.Length, trackWorld);
                nodes.Add(new PathNode(anchor.Location)
                {
                    NodeType = nodeType,
                    NodeIndex = anchor.TrackNodeIndex,
                    NextMainNode = i == orderedAnchors.Length - 1 ? -1 : i + 1,
                    NextSidingNode = -1,
                    WaitInfo = sourceNode?.WaitInfo,
                });
                changedNodeIndexes.Add(i);
            }

            PathModel generatedPath = new PathModel(sourcePath)
            {
                PathNodes = nodes.ToImmutable(),
            };

            return PathGenerationResult.Succeeded("Generated path from resolved main route.", generatedPath,
                resolution.Diagnostics, changedNodeIndexes.ToImmutable());
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
    }
}
