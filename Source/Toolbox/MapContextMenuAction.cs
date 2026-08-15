namespace FreeTrainSimulator.Toolbox
{
    /// <summary>
    /// Editing actions offered by the map surface context menu.
    /// </summary>
    internal enum MapContextMenuAction
    {
        /// <summary>Visual separator between menu sections; carries no command.</summary>
        Separator,

        // Node scoped
        MoveNode,
        CancelPlacement,
        RemoveViaPoint,
        ClearWaitPoint,
        SetReversalPoint,
        ClearReversalPoint,
        RepairNode,

        // Span scoped; the node index identifies the span's preceding node
        AddViaPoint,
        RemoveRestOfPath,
        SelectRouteCandidate,
        RouteThroughJunctionExit,

        // Path scoped
        ExtendPath,
        ReResolvePath,
        StartNewPath,
        StartNewPathHere,
        SetStartHere,
        SetEndHere,
        SavePath,
        Undo,
        Redo,
    }
}
