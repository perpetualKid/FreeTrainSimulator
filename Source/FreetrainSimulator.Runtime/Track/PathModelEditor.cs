using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>Role of an authored node in the single supported passing-branch topology.</summary>
    public enum PassingBranchNodeRole
    {
        None,
        MainRoute,
        BranchStart,
        BranchInterior,
        BranchRejoin,
    }

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
        /// Truncates the path after <paramref name="nodeIndex"/>, removing all later nodes and marking the node
        /// at <paramref name="nodeIndex"/> as the new end. Fails for an out-of-range index or when the node is
        /// already the last node.
        /// </summary>
        public static PathEditResult RemoveRestOfPath(PathModel pathModel, int nodeIndex)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {nodeIndex} is out of range.", pathModel);
            if (nodeIndex == nodes.Length - 1)
                return PathEditResult.Failed("The selected node is already the last node; there is nothing to remove.", pathModel);

            ImmutableArray<PathNode>.Builder builder = ImmutableArray.CreateBuilder<PathNode>(nodeIndex + 1);
            for (int i = 0; i < nodeIndex; i++)
            {
                // Surviving nodes keep their index, so links within [0, nodeIndex] are unchanged; links into
                // the removed tail are broken.
                builder.Add(nodes[i] with
                {
                    NextMainNode = nodes[i].NextMainNode > nodeIndex ? -1 : nodes[i].NextMainNode,
                    NextSidingNode = nodes[i].NextSidingNode > nodeIndex ? -1 : nodes[i].NextSidingNode,
                });
            }

            // The truncation point becomes the new end node.
            PathNodeType nodeType = (nodes[nodeIndex].NodeType & ~PathNodeType.Via) | PathNodeType.End;
            builder.Add(nodes[nodeIndex] with { NodeType = nodeType, NextMainNode = -1, NextSidingNode = -1 });

            return PathEditResult.Succeeded($"Removed {nodes.Length - nodeIndex - 1} node(s) after node {nodeIndex}.",
                pathModel with { PathNodes = builder.ToImmutable() },
                ImmutableArray.Create(nodeIndex));
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
        /// Removes the via point at <paramref name="nodeIndex"/>, relinking its predecessors to its successor and
        /// re-indexing the remaining links. Fails for an out-of-range index or when the node is a start or end node.
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

            // Bridge the gap first (still using pre-removal indexes), then let RemoveNodeAt re-index everything.
            ImmutableArray<PathNode> relinked = ImmutableArray.CreateRange(nodes, node => node with
            {
                NextMainNode = node.NextMainNode == nodeIndex ? removed.NextMainNode : node.NextMainNode,
                NextSidingNode = node.NextSidingNode == nodeIndex ? removed.NextSidingNode : node.NextSidingNode,
            });

            return PathEditResult.Succeeded($"Removed via point {nodeIndex}.", pathModel with { PathNodes = RemoveNodeAt(relinked, nodeIndex) }, ImmutableArray.Create(nodeIndex));
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
            return PathEditResult.Succeeded($"Created passing branch from node {startNodeIndex} to node {rejoinNodeIndex}.",
                pathModel with { PathNodes = updatedNodes }, ImmutableArray.Create(startNodeIndex, rejoinNodeIndex));
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

        /// <summary>Moves an interior anchor of a supported single-level passing branch.</summary>
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

            ImmutableArray<int> starts = Enumerable.Range(0, nodes.Length)
                .Where(index => nodes[index].NextMainNode >= 0 && nodes[index].NextSidingNode >= 0).ToImmutableArray();
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

            return PathEditResult.Succeeded($"Removed passing branch from node {startNodeIndex}.",
                pathModel with { PathNodes = updatedNodes }, interiorNodes.Add(startNodeIndex));
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

        private static bool IsEligiblePassingBranch(ImmutableArray<PathNode> nodes, ImmutableArray<int> mainRoute,
            int startNodeIndex, int rejoinNodeIndex, out string failure)
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

        private static bool TryGetSinglePassingBranch(ImmutableArray<PathNode> nodes, int startNodeIndex, out int rejoinNodeIndex,
            out ImmutableArray<int> interiorNodes, out string failure)
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
            ImmutableArray<int> branchStarts = Enumerable.Range(0, nodes.Length)
                .Where(index => nodes[index].NextMainNode >= 0 && nodes[index].NextSidingNode >= 0).ToImmutableArray();
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
        /// seconds. Fails for an out-of-range index, a non-positive wait time, a junction node, or a start/end node.
        /// </summary>
        public static PathEditResult SetWaitPoint(PathModel pathModel, int nodeIndex, int waitTimeSeconds)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            if (waitTimeSeconds <= 0)
                return PathEditResult.Failed("A wait point requires a positive wait time.", pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (!CanAnnotateNode(nodes, nodeIndex, "wait point", out string failure))
                return PathEditResult.Failed(failure, pathModel);

            PathNode node = nodes[nodeIndex] with
            {
                NodeType = nodes[nodeIndex].NodeType | PathNodeType.Wait,
                WaitInfo = new PathNodeWaitInfo { WaitTime = waitTimeSeconds },
            };

            return PathEditResult.Succeeded($"Set wait point of {waitTimeSeconds}s on node {nodeIndex}.", pathModel with { PathNodes = nodes.SetItem(nodeIndex, node) }, ImmutableArray.Create(nodeIndex));
        }

        /// <summary>
        /// Clears the wait point marker and wait information from the node at <paramref name="nodeIndex"/>. Fails
        /// for an out-of-range index or when the node is not a wait point.
        /// </summary>
        public static PathEditResult ClearWaitPoint(PathModel pathModel, int nodeIndex)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (nodeIndex < 0 || nodeIndex >= nodes.Length)
                return PathEditResult.Failed($"Node index {nodeIndex} is out of range.", pathModel);
            if ((nodes[nodeIndex].NodeType & PathNodeType.Wait) != PathNodeType.Wait)
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
        /// junction node, or a start/end node.
        /// </summary>
        public static PathEditResult SetReversalPoint(PathModel pathModel, int nodeIndex)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            if (!CanAnnotateNode(nodes, nodeIndex, "reversal point", out string failure))
                return PathEditResult.Failed(failure, pathModel);

            PathNode node = nodes[nodeIndex] with { NodeType = nodes[nodeIndex].NodeType | PathNodeType.Reversal };

            return PathEditResult.Succeeded($"Set reversal point on node {nodeIndex}.", pathModel with { PathNodes = nodes.SetItem(nodeIndex, node) }, ImmutableArray.Create(nodeIndex));
        }

        /// <summary>
        /// Clears the reversal point marker from the node at <paramref name="nodeIndex"/>. Fails for an
        /// out-of-range index or when the node is not a reversal point.
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

        // Drops a marker flag, falling back to a plain via node when nothing else remains.
        private static PathNodeType WithoutMarker(PathNodeType nodeType, PathNodeType marker)
        {
            PathNodeType remaining = nodeType & ~marker;
            return remaining == PathNodeType.None ? PathNodeType.Via : remaining;
        }

        /// <summary>
        /// Moves the node at <paramref name="nodeIndex"/> to a new track anchor, preserving its path links and
        /// wait metadata. Start/end/wait/reversal intent is preserved, while junction/via classification
        /// is recalculated from <paramref name="isJunction"/>.
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

            return PathEditResult.Succeeded($"Moved node {nodeIndex}.",
                pathModel with { PathNodes = nodes.SetItem(nodeIndex, movedNode) },
                ImmutableArray.Create(nodeIndex));
        }

        /// <summary>
        /// Safely repairs the node at <paramref name="nodeIndex"/> when the repair is unambiguous. The first
        /// supported repair converts a node incorrectly marked as a junction into a track point when its stored
        /// location lies on exactly one vector section and no actual junction exists there.
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
