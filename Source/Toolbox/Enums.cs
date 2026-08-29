using System.ComponentModel;

namespace FreeTrainSimulator.Toolbox
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

    public enum UserCommand
    {
        [Description("Cancel or Close")] Cancel, //Escape Key
        [Description("Span another instance")] NewInstance,
        [Description("Change Screen Mode")] ChangeScreenMode,
        [Description("Quit")] QuitWindow,
        [Description("Move Left (East)")] MoveLeft,
        [Description("Move Right (West)")] MoveRight,
        [Description("Move Up (North)")] MoveUp,
        [Description("Move Down (South)")] MoveDown,
        [Description("Zoom In")] ZoomIn,
        [Description("Zoom Out")] ZoomOut,
        [Description("Reset Zoom and Center Location")] ResetZoomAndLocation,
        [Description("Screenshot")] PrintScreen,
        [Description("Debug Information (Tab)")] DisplayDebugScreen,
        [Description("Location Window (Tab)")] DisplayLocationWindow,
        [Description("Help Window (Tab)")] DisplayHelpWindow,
        [Description("Settings Window (Tab)")] DisplaySettingsWindow,
        [Description("Log Window (Tab)")] DisplayLogWindow,
        [Description("Train Path Window (Tab)")] DisplayTrainPathWindow,
        [Description("Path Editor Undo")] PathEditorUndo,
        [Description("Path Editor Redo")] PathEditorRedo,           
        [Description("Path Editor Alternate Redo")] PathEditorAlternateRedo,
        [Description("Remove Selected Via Point")] RemoveSelectedViaPoint,
        [Description("Commit Path Placement")] CommitPathPlacement,
        [Description("Next Route Candidate")] NextRouteCandidate,
        [Description("Previous Route Candidate")] PreviousRouteCandidate,
        [Description("Accept Route Candidate")] AcceptRouteCandidate,
    }
}
    