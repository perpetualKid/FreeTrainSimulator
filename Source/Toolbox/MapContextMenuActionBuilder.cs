using System;
using System.Collections.Immutable;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Builds the ordered map context menu for the element under the pointer. Pure logic so the menu
    /// composition can be verified without any UI or graphics dependencies.
    /// </summary>
    internal static class MapContextMenuActionBuilder
    {
        /// <summary>
        /// Editor state the menu composition depends on, captured on the game thread.
        /// </summary>
        internal readonly record struct MapContextMenuState
        {
            /// <summary>Whether a map placement operation is currently in progress.</summary>
            public bool IsPlacementActive { get; init; }

            /// <summary>Whether progressive route building is currently in progress.</summary>
            public bool IsBuildingRoute { get; init; }

            /// <summary>Whether route building has a committed provisional endpoint and can be finished.</summary>
            public bool CanFinishPath { get; init; }

            public bool CanSetStartAnchor { get; init; }

            public bool CanSetEndAnchor { get; init; }

            /// <summary>Whether an undo snapshot is available.</summary>
            public bool CanUndo { get; init; }

            /// <summary>Whether a redo snapshot is available.</summary>
            public bool CanRedo { get; init; }

            /// <summary>Whether a path is loaded and can be appended to.</summary>
            public bool CanContinuePath { get; init; }

            /// <summary>Whether a path is loaded and can be re-resolved.</summary>
            public bool CanReResolvePath { get; init; }

            /// <summary>Whether a path is loaded and can be saved.</summary>
            public bool CanSavePath { get; init; }

            /// <summary>Whether a new path can be started.</summary>
            public bool CanStartNewPath { get; init; }

            /// <summary>Whether the active path is the unsaved model created by New Path.</summary>
            public bool IsNewPath { get; init; }
        }

        /// <summary>
        /// Builds the menu for a path node. While a node move is in progress the only meaningful action is
        /// cancelling it, because the pointer drives the move preview.
        /// </summary>
        public static ImmutableArray<MapContextMenuItem> BuildForNode(TrainPathPointBase node, int nodeIndex, bool canMoveNode, in MapContextMenuState state)
        {
            return BuildForNode(node, nodeIndex, canMoveNode, state, null);
        }

        /// <summary>Builds the node menu with a valid snapped context anchor for endpoint commands.</summary>
        public static ImmutableArray<MapContextMenuItem> BuildForNode(TrainPathPointBase node, int nodeIndex, bool canMoveNode,
            in MapContextMenuState state, PathNode placementAnchor)
        {
            if (state.IsPlacementActive)
                return BuildPlacementActions(state, placementAnchor, nodeIndex);

            if (node == null)
                return ImmutableArray<MapContextMenuItem>.Empty;

            ImmutableArray<MapContextMenuItem>.Builder items = ImmutableArray.CreateBuilder<MapContextMenuItem>();

            if (canMoveNode)
                items.Add(new MapContextMenuItem(MapContextMenuAction.MoveNode, nodeIndex));
            AddAnchorPlacementActions(items, state, placementAnchor);

            // Creating or editing a wait point is done by editing the Wait property. Keep the one-click clear
            // action here because removal remains useful directly on the map.
            if (node.WaitInfo != null)
                items.Add(new MapContextMenuItem(MapContextMenuAction.ClearWaitPoint, nodeIndex));
            items.Add(new MapContextMenuItem(
                node.NodeType.Includes(PathNodeType.Reversal) ? MapContextMenuAction.ClearReversalPoint : MapContextMenuAction.SetReversalPoint, nodeIndex));

            // Start and end nodes are managed through their own commands, so removing them as a via point is
            // never meaningful.
            if (IsRemovableViaPoint(node))
                items.Add(new MapContextMenuItem(MapContextMenuAction.RemoveViaPoint, nodeIndex));

            if (node.ValidationResult != PathNodeInvalidReasons.None)
                items.Add(new MapContextMenuItem(MapContextMenuAction.RepairNode, nodeIndex));

            AddSeparator(items);
            items.Add(new MapContextMenuItem(MapContextMenuAction.RemoveRestOfPath, nodeIndex));

            AddHistoryActions(items, state);
            return Finalize(items);
        }

        /// <summary>
        /// Builds the menu for a path span. <paramref name="fromNodeIndex"/> is the span's preceding node;
        /// <paramref name="candidates"/> holds the equal-cost route candidates when the span is ambiguous.
        /// </summary>
        public static ImmutableArray<MapContextMenuItem> BuildForSpan(int fromNodeIndex, PathNode placementAnchor,
            ImmutableArray<ResolvedRouteCandidate> candidates, in MapContextMenuState state)
        {
            return BuildForSpan(fromNodeIndex, placementAnchor, candidates, -1, state);
        }

        /// <summary>
        /// Builds the span menu with explicit exit choices when the context location is a junction.
        /// </summary>
        public static ImmutableArray<MapContextMenuItem> BuildForSpan(int fromNodeIndex, PathNode placementAnchor,
            ImmutableArray<ResolvedRouteCandidate> candidates, int junctionNodeIndex, in MapContextMenuState state)
        {
            ArgumentNullException.ThrowIfNull(placementAnchor);

            if (state.IsPlacementActive)
                return BuildPlacementActions(state, placementAnchor, fromNodeIndex);

            ImmutableArray<MapContextMenuItem>.Builder items = ImmutableArray.CreateBuilder<MapContextMenuItem>();

            items.Add(new MapContextMenuItem(MapContextMenuAction.AddViaPoint, fromNodeIndex) { PlacementAnchor = placementAnchor });
            AddAnchorPlacementActions(items, state, placementAnchor);
            items.Add(new MapContextMenuItem(MapContextMenuAction.RemoveRestOfPath, fromNodeIndex));

            if (!candidates.IsDefaultOrEmpty)
            {
                AddSeparator(items);
                AddJunctionExitActions(items, fromNodeIndex, candidates, junctionNodeIndex);
                for (int i = 0; i < candidates.Length; i++)
                {
                    items.Add(new MapContextMenuItem(MapContextMenuAction.SelectRouteCandidate, fromNodeIndex, i,
                        string.Join(" - ", candidates[i].RouteNodeIndexes)));
                }
            }

            AddHistoryActions(items, state);
            return Finalize(items);
        }

        private static void AddJunctionExitActions(ImmutableArray<MapContextMenuItem>.Builder items, int fromNodeIndex,
            ImmutableArray<ResolvedRouteCandidate> candidates, int junctionNodeIndex)
        {
            if (junctionNodeIndex < 0)
                return;

            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                ImmutableArray<int> routeNodeIndexes = candidates[candidateIndex].RouteNodeIndexes;
                int junctionRouteIndex = routeNodeIndexes.IndexOf(junctionNodeIndex);
                if (junctionRouteIndex < 0 || junctionRouteIndex == routeNodeIndexes.Length - 1)
                    continue;

                items.Add(new MapContextMenuItem(MapContextMenuAction.RouteThroughJunctionExit, fromNodeIndex,
                    candidateIndex, routeNodeIndexes[junctionRouteIndex + 1].ToString()));
            }
        }

        /// <summary>
        /// Builds the menu shown when the pointer is not over a node or span.
        /// </summary>
        public static ImmutableArray<MapContextMenuItem> BuildForMap(in MapContextMenuState state)
        {
            return BuildForMap(state, null);
        }

        /// <summary>
        /// Builds the map menu, attaching a snapped track anchor to endpoint commands when the context-menu
        /// location is valid track.
        /// </summary>
        public static ImmutableArray<MapContextMenuItem> BuildForMap(in MapContextMenuState state, PathNode placementAnchor)
        {
            if (state.IsPlacementActive)
                return BuildPlacementActions(state, placementAnchor, -1);

            ImmutableArray<MapContextMenuItem>.Builder items = ImmutableArray.CreateBuilder<MapContextMenuItem>();

            if (state.CanContinuePath)
                items.Add(new MapContextMenuItem(MapContextMenuAction.ContinuePath));
            if (state.CanReResolvePath && !state.IsNewPath)
                items.Add(new MapContextMenuItem(MapContextMenuAction.ReResolvePath));
            AddAnchorPlacementActions(items, state, placementAnchor);
            if (state.CanStartNewPath && !state.IsNewPath && placementAnchor != null)
                items.Add(new MapContextMenuItem(MapContextMenuAction.StartNewPathHere) { PlacementAnchor = placementAnchor });
            if (state.CanSavePath)
                items.Add(new MapContextMenuItem(MapContextMenuAction.SavePath));

            AddHistoryActions(items, state);
            return Finalize(items);
        }

        private static ImmutableArray<MapContextMenuItem> BuildPlacementActions(in MapContextMenuState state,
            PathNode placementAnchor, int nodeIndex)
        {
            if (!state.IsBuildingRoute)
                return ImmutableArray.Create(new MapContextMenuItem(MapContextMenuAction.CancelPlacement, nodeIndex));

            ImmutableArray<MapContextMenuItem>.Builder items = ImmutableArray.CreateBuilder<MapContextMenuItem>();
            if (placementAnchor != null)
            {
                items.Add(new MapContextMenuItem(MapContextMenuAction.AddRoutePointHere, nodeIndex) { PlacementAnchor = placementAnchor });
                items.Add(new MapContextMenuItem(MapContextMenuAction.FinishPathHere, nodeIndex) { PlacementAnchor = placementAnchor });
            }
            if (state.CanFinishPath)
                items.Add(new MapContextMenuItem(MapContextMenuAction.FinishPath, nodeIndex));
            AddSeparator(items);
            items.Add(new MapContextMenuItem(MapContextMenuAction.CancelPlacement, nodeIndex));
            return Finalize(items);
        }

        private static void AddAnchorPlacementActions(ImmutableArray<MapContextMenuItem>.Builder items, in MapContextMenuState state, PathNode placementAnchor)
        {
            if (placementAnchor == null)
                return;

            if (state.CanSetStartAnchor)
                items.Add(new MapContextMenuItem(MapContextMenuAction.SetStartHere) { PlacementAnchor = placementAnchor });
            if (state.CanSetEndAnchor)
                items.Add(new MapContextMenuItem(MapContextMenuAction.SetEndHere) { PlacementAnchor = placementAnchor });
        }

        // Undo/Redo are offered on every scope because they are the most frequently needed actions while
        // editing; the remaining path-scoped actions stay on the map menu to keep node/span menus focused.
        private static void AddHistoryActions(ImmutableArray<MapContextMenuItem>.Builder items, in MapContextMenuState state)
        {
            if (!state.CanUndo && !state.CanRedo)
                return;

            AddSeparator(items);
            if (state.CanUndo)
                items.Add(new MapContextMenuItem(MapContextMenuAction.Undo));
            if (state.CanRedo)
                items.Add(new MapContextMenuItem(MapContextMenuAction.Redo));
        }

        // Starts a new section, unless there is nothing to separate from yet.
        private static void AddSeparator(ImmutableArray<MapContextMenuItem>.Builder items)
        {
            if (items.Count > 0 && !items[^1].IsSeparator)
                items.Add(MapContextMenuItem.Separator);
        }

        // Drops a trailing separator so the menu never ends with a divider.
        private static ImmutableArray<MapContextMenuItem> Finalize(ImmutableArray<MapContextMenuItem>.Builder items)
        {
            while (items.Count > 0 && items[^1].IsSeparator)
                items.RemoveAt(items.Count - 1);

            return items.ToImmutable();
        }

        private static bool IsRemovableViaPoint(TrainPathPointBase node)
        {
            return !node.NodeType.Includes(PathNodeType.Start) && !node.NodeType.Includes(PathNodeType.End);
        }
    }
}
