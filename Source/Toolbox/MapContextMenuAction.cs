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
        CancelMoveNode,
        RemoveViaPoint,
        SetWaitPoint,
        ClearWaitPoint,
        SetReversalPoint,
        ClearReversalPoint,
        RepairNode,

        // Span scoped; the node index identifies the span's preceding node
        AddViaPoint,
        RemoveRestOfPath,
        SelectRouteCandidate,

        // Path scoped
        ExtendPath,
        ReResolvePath,
        StartNewPath,
        SavePath,
        Undo,
        Redo,
    }
}
