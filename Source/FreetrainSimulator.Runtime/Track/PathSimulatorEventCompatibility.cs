using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Checks the event combinations supported by the current Simulator path consumer, not general MSTS validity.
    /// </summary>
    /// <remarks>
    /// Does not resolve track geometry or certify siding topology, projection, node placement, or start/end markers.
    /// Those checks remain with the route resolver and Simulator. Passing endpoints retain main-path event semantics.
    /// </remarks>
    public static class PathSimulatorEventCompatibility
    {
        private const string firstMainEventMessage = "Unsupported wait/reverse event at first main node; the circuit consumer skips this event.";
        private const string firstMainEventAction = "Place the wait or reversal after the first main node.";
        private const string junctionOrCombinedEventMessage = "Unsupported junction or combined wait/reverse event at node {0}.";
        private const string junctionOrCombinedEventAction = "Place a single wait or reversal on an ordinary main-path node.";
        private const string passingInteriorEventMessage = "Unsupported siding event at node {0}; branch waits/reversals cannot be preserved.";
        private const string passingInteriorEventAction = "Place the event on the main path outside the passing interior.";
        private const string multipleReversalsMessage = "Multiple reversals in one continuous vector occurrence at node {0}; the circuit consumer cannot preserve every leg.";
        private const string multipleReversalsAction = "Separate successive reversals with a junction before using the path in Simulator.";
        private const string emptyPathMessage = "Path has no authored nodes.";
        private const string emptyPathAction = "Add a start node and an end node.";
        private const string missingStartMessage = "Path has no usable main root at node index {0}.";
        private const string missingStartAction = "Select the existing start node as the main root.";
        private const string invalidMainLinkMessage = "Path node {0} has an out-of-range or missing main target.";
        private const string invalidMainLinkAction = "Repair or remove the invalid main path link.";
        private const string invalidSidingLinkMessage = "Path node {0} has an out-of-range or missing siding target.";
        private const string invalidSidingLinkAction = "Repair or remove the invalid passing path link.";
        private const string graphCycleMessage = "Path traversal contains a cycle at node {0}.";
        private const string graphCycleAction = "Repair the cyclic main or passing path links.";
        private const string passingRejoinMessage = "Siding at node {0} does not rejoin the main path downstream.";
        private const string passingRejoinAction = "Connect the passing branch to a downstream main-path node.";
        private const string unreachableNodeMessage = "Path node {0} is missing or is not reachable from the start node.";
        private const string unreachableNodeAction = "Connect the node to the main or passing path, or remove it.";

        /// <summary>
        /// Checks one prospective or materialized node, using its junction marker as the junction context.
        /// </summary>
        /// <param name="node">Node whose event markers and wait payload are checked.</param>
        /// <param name="nodeIndex">Node index to associate with a refusal.</param>
        /// <param name="isFirst">Whether this is the first node of the main traversal.</param>
        /// <param name="isPassingInterior">Whether this node lies strictly between passing endpoints.</param>
        /// <returns>The first event refusal, or <see langword="null"/> when supported.</returns>
        public static PathRouteDiagnostic ValidateNode(PathNode node, int nodeIndex, bool isFirst, bool isPassingInterior)
        {
            ArgumentNullException.ThrowIfNull(node);

            return ValidateNode(node, nodeIndex, isFirst, isPassingInterior, node.NodeType.Includes(PathNodeType.Junction));
        }

        /// <summary>
        /// Checks one node using the caller's resolved junction context instead of its junction marker.
        /// </summary>
        /// <param name="node">Node whose event markers and wait payload are checked.</param>
        /// <param name="nodeIndex">Node index to associate with a refusal.</param>
        /// <param name="isFirst">Whether this is the first node of the main traversal.</param>
        /// <param name="isPassingInterior">Whether this node lies strictly between passing endpoints.</param>
        /// <param name="isJunction">Whether the node is a junction in the caller's track context.</param>
        /// <returns>The first event refusal, or <see langword="null"/> when supported.</returns>
        public static PathRouteDiagnostic ValidateNode(PathNode node, int nodeIndex, bool isFirst, bool isPassingInterior, bool isJunction)
        {
            ArgumentNullException.ThrowIfNull(node);

            return ValidateNode(node.NodeType, node.WaitInfo?.WaitTime ?? 0, nodeIndex, isFirst, isPassingInterior, isJunction);
        }

        /// <summary>
        /// Checks event markers and wait payload without materializing a source node.
        /// </summary>
        /// <param name="nodeType">Event markers in the caller's current node context.</param>
        /// <param name="waitTime">Wait payload in seconds.</param>
        /// <param name="nodeIndex">Node index to associate with a refusal.</param>
        /// <param name="isFirst">Whether this is the first node of the main traversal.</param>
        /// <param name="isPassingInterior">Whether this node lies strictly between passing endpoints.</param>
        /// <param name="isJunction">Whether the node is a junction in the caller's track context.</param>
        /// <returns>The first event refusal, or <see langword="null"/> when supported.</returns>
        public static PathRouteDiagnostic ValidateNode(PathNodeType nodeType, int waitTime, int nodeIndex, bool isFirst, bool isPassingInterior, bool isJunction)
        {
            bool hasEvent = (nodeType & (PathNodeType.Wait | PathNodeType.Reversal)) != 0;
            if (isFirst && hasEvent)
                return CreateDiagnostic(PathRouteDiagnosticCode.UnsupportedFirstMainEvent, nodeIndex);

            if (isJunction && hasEvent)
                return CreateDiagnostic(PathRouteDiagnosticCode.UnsupportedJunctionEvent, nodeIndex);

            if (nodeType.Includes(PathNodeType.Wait | PathNodeType.Reversal))
                return CreateDiagnostic(PathRouteDiagnosticCode.UnsupportedCombinedWaitReversal, nodeIndex);

            if (isPassingInterior && (hasEvent || waitTime != 0))
                return CreateDiagnostic(PathRouteDiagnosticCode.UnsupportedPassingInteriorEvent, nodeIndex);

            return null;
        }

        /// <summary>
        /// Updates main-traversal reversal state, resetting it only at a junction in the caller's track context.
        /// </summary>
        /// <param name="node">Node whose reversal marker is checked.</param>
        /// <param name="nodeIndex">Node index to associate with a refusal.</param>
        /// <param name="isJunction">Whether the node is a junction in the caller's track context.</param>
        /// <param name="reversed">Whether a reversal has occurred since the last junction.</param>
        /// <returns>A multiple-reversal refusal, or <see langword="null"/> when supported.</returns>
        public static PathRouteDiagnostic ValidateTraversalNode(PathNode node, int nodeIndex, bool isJunction, ref bool reversed)
        {
            ArgumentNullException.ThrowIfNull(node);

            if (isJunction)
                reversed = false;
            else if (node.NodeType.Includes(PathNodeType.Reversal))
            {
                if (reversed)
                    return CreateDiagnostic(PathRouteDiagnosticCode.MultipleReversalsInContinuousVector, nodeIndex);
                reversed = true;
            }

            return null;
        }

        /// <summary>
        /// Checks events in the exact materialized graph, following main links from the supplied root and siding
        /// links strictly between main-path endpoints. Reversal state resets only at a junction, not at waits or vias.
        /// </summary>
        /// <param name="pathModel">Materialized path to check without modifying it.</param>
        /// <param name="startNodeIndex">Explicit main root selected by the caller, normally from the Start marker.</param>
        /// <returns>Event refusals and structural diagnostics needed for safe traversal; empty when none are found.</returns>
        /// <remarks>
        /// Junction context comes from node markers; callers resolving junctions independently must check nodes with
        /// the resolved-context overload. Invalid targets, missing nodes, cycles, unreachable nodes and branches that
        /// do not rejoin downstream are reported, but other Simulator structural guards remain necessary.
        /// There is no passing-branch count limit, track lookup, or geometry reconstruction.
        /// </remarks>
        public static ImmutableArray<PathRouteDiagnostic> Validate(PathModel pathModel, int startNodeIndex)
        {
            ArgumentNullException.ThrowIfNull(pathModel);

            ImmutableArray<PathNode> nodes = pathModel.PathNodes;
            if (nodes.IsDefaultOrEmpty)
                return ImmutableArray.Create(CreateDiagnostic(PathRouteDiagnosticCode.EmptyPath, -1));

            if (startNodeIndex < 0 || startNodeIndex >= nodes.Length || nodes[startNodeIndex] == null)
                return ImmutableArray.Create(CreateDiagnostic(PathRouteDiagnosticCode.MissingStartNode, startNodeIndex));

            ImmutableArray<PathRouteDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<PathRouteDiagnostic>();
            for (int i = 0; i < nodes.Length; i++)
            {
                PathNode node = nodes[i];
                if (node == null)
                {
                    diagnostics.Add(CreateDiagnostic(PathRouteDiagnosticCode.UnreachableNode, i));
                    continue;
                }

                if (!IsValidLink(node.NextMainNode, nodes))
                    diagnostics.Add(CreateDiagnostic(PathRouteDiagnosticCode.InvalidMainLink, i) with
                    { FromNodeIndex = i, ToNodeIndex = node.NextMainNode });

                if (!IsValidLink(node.NextSidingNode, nodes))
                    diagnostics.Add(CreateDiagnostic(PathRouteDiagnosticCode.InvalidSidingLink, i) with
                    { FromNodeIndex = i, ToNodeIndex = node.NextSidingNode });
            }

            if (diagnostics.Count > 0)
                return diagnostics.ToImmutable();

            List<int> main = new List<int>();
            Dictionary<int, int> positions = new Dictionary<int, int>();
            bool reversed = false;
            for (int current = startNodeIndex; current >= 0; current = nodes[current].NextMainNode)
            {
                if (!positions.TryAdd(current, main.Count))
                {
                    diagnostics.Add(CreateDiagnostic(PathRouteDiagnosticCode.UnsupportedGraphCycle, current));
                    return diagnostics.ToImmutable();
                }

                main.Add(current);
                PathNode node = nodes[current];
                PathRouteDiagnostic diagnostic = ValidateNode(node, current, current == startNodeIndex, false);
                PathRouteDiagnostic reversalDiagnostic = ValidateTraversalNode(node, current,
                    node.NodeType.Includes(PathNodeType.Junction), ref reversed);
                diagnostic ??= reversalDiagnostic;

                if (diagnostic != null)
                    diagnostics.Add(diagnostic);
            }

            HashSet<int> reachable = new HashSet<int>(main);
            foreach (int split in main)
            {
                int current = nodes[split].NextSidingNode;
                if (current < 0)
                    continue;

                HashSet<int> visited = new HashSet<int>();
                while (current >= 0 && !positions.ContainsKey(current))
                {
                    if (!visited.Add(current))
                    {
                        diagnostics.Add(CreateDiagnostic(PathRouteDiagnosticCode.UnsupportedGraphCycle, current));
                        break;
                    }

                    if (reachable.Add(current))
                    {
                        PathRouteDiagnostic diagnostic = ValidateNode(nodes[current], current, false, true);
                        if (diagnostic != null)
                            diagnostics.Add(diagnostic);
                    }
                    current = nodes[current].NextSidingNode;
                }

                if (current < 0 || (positions.TryGetValue(current, out int rejoin) && rejoin <= positions[split]))
                    diagnostics.Add(CreateDiagnostic(PathRouteDiagnosticCode.PassingBranchDoesNotRejoinMain, split));
            }

            for (int i = 0; i < nodes.Length; i++)
            {
                if (!reachable.Contains(i))
                    diagnostics.Add(CreateDiagnostic(PathRouteDiagnosticCode.UnreachableNode, i));
            }

            return diagnostics.ToImmutable();
        }

        private static bool IsValidLink(int target, ImmutableArray<PathNode> nodes)
        {
            return target == -1 || (target >= 0 && target < nodes.Length && nodes[target] != null);
        }

        private static PathRouteDiagnostic CreateDiagnostic(PathRouteDiagnosticCode code, int nodeIndex)
        {
            (string message, string action) = code switch
            {
                PathRouteDiagnosticCode.UnsupportedFirstMainEvent => (firstMainEventMessage, firstMainEventAction),
                PathRouteDiagnosticCode.UnsupportedCombinedWaitReversal or PathRouteDiagnosticCode.UnsupportedJunctionEvent
                    => (junctionOrCombinedEventMessage, junctionOrCombinedEventAction),
                PathRouteDiagnosticCode.UnsupportedPassingInteriorEvent => (passingInteriorEventMessage, passingInteriorEventAction),
                PathRouteDiagnosticCode.MultipleReversalsInContinuousVector => (multipleReversalsMessage, multipleReversalsAction),
                PathRouteDiagnosticCode.EmptyPath => (emptyPathMessage, emptyPathAction),
                PathRouteDiagnosticCode.MissingStartNode => (missingStartMessage, missingStartAction),
                PathRouteDiagnosticCode.InvalidMainLink => (invalidMainLinkMessage, invalidMainLinkAction),
                PathRouteDiagnosticCode.InvalidSidingLink => (invalidSidingLinkMessage, invalidSidingLinkAction),
                PathRouteDiagnosticCode.UnsupportedGraphCycle => (graphCycleMessage, graphCycleAction),
                PathRouteDiagnosticCode.PassingBranchDoesNotRejoinMain => (passingRejoinMessage, passingRejoinAction),
                PathRouteDiagnosticCode.UnreachableNode => (unreachableNodeMessage, unreachableNodeAction),
                _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
            };
            return new PathRouteDiagnostic(code is PathRouteDiagnosticCode.EmptyPath or PathRouteDiagnosticCode.MissingStartNode
                ? PathRouteDiagnosticSeverity.Fatal : PathRouteDiagnosticSeverity.Error, code,
                string.Format(CultureInfo.InvariantCulture, message, nodeIndex), nodeIndex, action);
        }
    }
}