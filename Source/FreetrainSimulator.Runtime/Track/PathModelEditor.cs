using System;
using System.Collections.Immutable;
using System.Linq;

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

            // Swap Intermediate for End (keeping any Junction/Wait/Reversal flags) and break the trailing link.
            PathNodeType nodeType = (nodes[lastIndex].NodeType & ~PathNodeType.Intermediate) | PathNodeType.End;
            PathNode endNode = nodes[lastIndex] with { NodeType = nodeType, NextMainNode = -1 };

            return PathEditResult.Succeeded("Added end node.",
                pathModel with { PathNodes = nodes.SetItem(lastIndex, endNode) },
                ImmutableArray.Create(lastIndex));
        }

        /// <summary>
        /// Clears the end flag from the end node, leaving it as a regular intermediate node. Fails when the
        /// path has no end node.
        /// </summary>
        public static PathEditResult RemoveEnd(PathModel pathModel)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = Nodes(pathModel);
            int endIndex = IndexOfNodeType(nodes, PathNodeType.End);
            if (endIndex < 0)
                return PathEditResult.Failed("The path has no end node to remove.", pathModel);

            // Drop the End flag; if nothing else remains, the node becomes a plain intermediate node.
            PathNodeType nodeType = nodes[endIndex].NodeType & ~PathNodeType.End;
            if (nodeType == PathNodeType.None)
                nodeType = PathNodeType.Intermediate;

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

            // Swap Intermediate for Start (keeping any Junction flag); links are unchanged because the first
            // node keeps its position.
            PathNodeType nodeType = (nodes[0].NodeType & ~PathNodeType.Intermediate) | PathNodeType.Start;

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
            PathNodeType nodeType = (nodes[nodeIndex].NodeType & ~PathNodeType.Intermediate) | PathNodeType.End;
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
        /// main chain and re-indexing all following links. A via point is an authored intermediate anchor that
        /// constrains the route the resolver may take. Fails for an out-of-range index or when the preceding node
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

        private static PathNode CreateViaNode(PathNode anchor, bool junction, int nextMainNode)
        {
            return new PathNode(anchor.Location)
            {
                NodeType = junction ? PathNodeType.Junction : PathNodeType.Intermediate,
                NodeIndex = anchor.NodeIndex,
                NextMainNode = nextMainNode,
                NextSidingNode = -1,
            };
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

        // Drops a marker flag, falling back to a plain intermediate node when nothing else remains.
        private static PathNodeType WithoutMarker(PathNodeType nodeType, PathNodeType marker)
        {
            PathNodeType remaining = nodeType & ~marker;
            return remaining == PathNodeType.None ? PathNodeType.Intermediate : remaining;
        }

        /// <summary>
        /// Moves the node at <paramref name="nodeIndex"/> to a new track anchor, preserving its path links and
        /// wait metadata. Start/end/wait/reversal intent is preserved, while junction/intermediate classification
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
            nodeType |= isJunction ? PathNodeType.Junction : PathNodeType.Intermediate;

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
