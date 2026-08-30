namespace FreeTrainSimulator.Toolbox.PathEditing
{
    internal enum PathEditorPlacementMode
    {
        None,
        MoveNode,
        StartAnchor,
        EndAnchor,
        BuildRoute,
    }

    internal enum PassingBranchAuthoringPhase
    {
        Idle,
        SelectingRejoin,
        SelectingCandidate,
    }

    /// <summary>
    /// Outcome of resolving the span(s) affected by an authored anchor edit.
    /// </summary>
    internal enum PathSpanCommitStatus
    {
        /// <summary>The authored edit itself was rejected; nothing was resolved.</summary>
        Failed,
        /// <summary>Every affected span resolved to a single route; the result can be committed.</summary>
        Resolved,
        /// <summary>At least one affected span has several equal-cost routes; the user must choose.</summary>
        Ambiguous,
        /// <summary>At least one affected span could not be routed; the edit must not be committed.</summary>
        Unresolved,
    }

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
        StartPassingBranch,
        RejoinPassingBranch,
        CancelPassingBranch,
        RemovePassingBranch,

        // Span scoped; the node index identifies the span's preceding node
        AddViaPoint,
        RemoveRestOfPath,
        SelectRouteCandidate,
        RouteThroughJunctionExit,

        // Path scoped
        ContinuePath,
        AddRoutePointHere,
        FinishPathHere,
        FinishPath,
        ReResolvePath,
        StartNewPath,
        StartNewPathHere,
        SetStartHere,
        SetEndHere,
        SavePath,
        Undo,
        Redo,
    }

    /// <summary>
    /// Scope a map context menu action operates on.
    /// </summary>
    internal enum MapContextMenuScope
    {
        /// <summary>Action targets the path node under the pointer.</summary>
        Node,

        /// <summary>Action targets the path span under the pointer.</summary>
        Span,

        /// <summary>Action targets the current path or the editor as a whole.</summary>
        Path,
    }
}
