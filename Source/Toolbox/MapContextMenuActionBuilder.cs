using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Builds the ordered list of node-related actions offered for a path node on the map surface. Pure logic
    /// so the menu composition can be verified without any UI or graphics dependencies.
    /// </summary>
    internal static class MapContextMenuActionBuilder
    {
        /// <summary>
        /// Returns the actions available for <paramref name="node"/>. While a node move is in progress the
        /// only meaningful action is cancelling it, because the pointer drives the move preview.
        /// </summary>
        public static ImmutableArray<MapContextMenuAction> Build(TrainPathPointBase node, bool canMoveNode, bool isMovingNode)
        {
            if (isMovingNode)
                return ImmutableArray.Create(MapContextMenuAction.CancelMoveNode);

            if (node == null)
                return ImmutableArray<MapContextMenuAction>.Empty;

            ImmutableArray<MapContextMenuAction>.Builder actions = ImmutableArray.CreateBuilder<MapContextMenuAction>();

            if (canMoveNode)
                actions.Add(MapContextMenuAction.MoveNode);

            actions.Add(MapContextMenuAction.AddViaPoint);
            actions.Add(MapContextMenuAction.RemoveViaPoint);

            actions.Add(node.WaitInfo != null ? MapContextMenuAction.ClearWaitPoint : MapContextMenuAction.SetWaitPoint);
            actions.Add(node.NodeType.Includes(PathNodeType.Reversal) ? MapContextMenuAction.ClearReversalPoint : MapContextMenuAction.SetReversalPoint);

            if (node.ValidationResult != PathNodeInvalidReasons.None)
                actions.Add(MapContextMenuAction.RepairNode);

            actions.Add(MapContextMenuAction.RemoveRestOfPath);

            return actions.ToImmutable();
        }
    }
}
