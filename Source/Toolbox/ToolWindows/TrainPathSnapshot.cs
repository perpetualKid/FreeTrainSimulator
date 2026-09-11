using System.Collections.Immutable;

using FreeTrainSimulator.Toolbox.PathEditing;

namespace FreeTrainSimulator.Toolbox.ToolWindows
{
    /// <summary>
    /// Immutable snapshot of the hosted train-path tool window state, captured on the game thread and read
    /// lock-free by the WPF view model. Combines the available paths, the selected path id, the current
    /// path's node rows, and its metadata name/value rows.
    /// </summary>
    internal sealed record TrainPathSnapshot
    {
        /// <summary>Available paths for the loaded route.</summary>
        public ImmutableArray<TrainPathListRow> Paths { get; init; }

        /// <summary>Id of the currently selected path, or null when none is selected.</summary>
        public string SelectedPathId { get; init; }

        /// <summary>Node rows of the currently edited path.</summary>
        public ImmutableArray<TrainPathNodeRow> Nodes { get; init; }

        /// <summary>Index of the path node selected on the map, or -1 when none is selected.</summary>
        public int SelectedNodeIndex { get; init; } = -1;

        /// <summary>Name/value metadata rows for the currently edited path.</summary>
        public ImmutableArray<ToolWindowRow> Metadata { get; init; }

        public string PathName { get; init; }

        public string PathStart { get; init; }

        public string PathEnd { get; init; }

        public bool PlayerPath { get; init; }

        /// <summary>Equal-cost route candidates of the currently edited path's ambiguous spans.</summary>
        public ImmutableArray<TrainPathRouteCandidateRow> RouteCandidates { get; init; }

        /// <summary>Resolver diagnostics of the currently edited path.</summary>
        public ImmutableArray<TrainPathDiagnosticRow> Diagnostics { get; init; }

        /// <summary>Whether the current path is loaded without runtime route or renderer state for safe repair.</summary>
        public bool IsRepairMode { get; init; }

        /// <summary>Actionable feedback from the latest blocked save request.</summary>
        public string BlockedSaveMessage { get; init; }

        /// <summary>Diagnostic to focus for the latest blocked save request, when available.</summary>
        public TrainPathDiagnosticRow? BlockedSaveDiagnostic { get; init; }

        /// <summary>Version incremented for each blocked save request.</summary>
        public int BlockedSaveFeedbackVersion { get; init; }

        /// <summary>Whether an undo step is available.</summary>
        public bool CanUndo { get; init; }

        /// <summary>Whether a redo step is available.</summary>
        public bool CanRedo { get; init; }

        /// <summary>Whether the current path can be snapped to track.</summary>
        public bool CanSnapToTrack { get; init; }

        /// <summary>Whether a node move operation is currently active.</summary>
        public bool CanCancelMoveNode { get; init; }

        /// <summary>Whether the active node move has a valid preview that can be committed.</summary>
        public bool CanCommitMoveNode { get; init; }

        /// <summary>Current guided map placement mode.</summary>
        public PathEditorPlacementMode PlacementMode { get; init; }

        /// <summary>Whether the active placement or route-candidate interaction can be canceled.</summary>
        public bool CanCancelPathInteraction { get; init; }

        /// <summary>Whether the active placement has a valid track preview that can be committed.</summary>
        public bool CanCommitPlacement { get; init; }

        /// <summary>Whether start-anchor placement can begin.</summary>
        public bool CanPlaceStartAnchor { get; init; }

        /// <summary>Whether end-anchor placement can begin.</summary>
        public bool CanPlaceEndAnchor { get; init; }

        /// <summary>Whether the selected authored node can be moved safely.</summary>
        public bool CanMoveSelectedNode { get; init; }

        /// <summary>Whether the selected authored node has an applicable safe repair.</summary>
        public bool CanRepairSelectedNode { get; init; }

        /// <summary>Whether the selected authored node can be removed as a via point.</summary>
        public bool CanRemoveSelectedViaPoint { get; init; }

        /// <summary>Whether the active unsaved New Path model can be canceled.</summary>
        public bool CanCancelNewPath { get; init; }

        /// <summary>Whether progressive route building is active.</summary>
        public bool IsBuildingRoute { get; init; }

        /// <summary>Whether route building can finish at its last committed point.</summary>
        public bool CanFinishPath { get; init; }

        public bool CanBeginPassingBranch { get; init; }

        public bool CanCompletePassingBranch { get; init; }

        public bool CanCancelPassingBranch { get; init; }

        public bool CanRemovePassingBranch { get; init; }

        public bool HasPendingPassingBranchCandidate { get; init; }

        public PassingBranchAuthoringPhase PassingBranchPhase { get; init; }

        public string CommandResultMessage { get; init; }

        public bool CommandResultIsWarning { get; init; }

        public int CommandResultVersion { get; init; }

        /// <summary>An empty snapshot used before any path content is available.</summary>
        public static TrainPathSnapshot Empty { get; } = new TrainPathSnapshot
        {
            Paths = ImmutableArray<TrainPathListRow>.Empty,
            SelectedPathId = null,
            Nodes = ImmutableArray<TrainPathNodeRow>.Empty,
            SelectedNodeIndex = -1,
            Metadata = ImmutableArray<ToolWindowRow>.Empty,
            RouteCandidates = ImmutableArray<TrainPathRouteCandidateRow>.Empty,
            Diagnostics = ImmutableArray<TrainPathDiagnosticRow>.Empty,
            IsRepairMode = false,
            BlockedSaveMessage = null,
            BlockedSaveDiagnostic = null,
            BlockedSaveFeedbackVersion = 0,
            CanUndo = false,
            CanRedo = false,
            CanSnapToTrack = false,
            CanCancelMoveNode = false,
            CanCommitMoveNode = false,
            PlacementMode = PathEditorPlacementMode.None,
            CanCancelPathInteraction = false,
            CanCommitPlacement = false,
            CanPlaceStartAnchor = false,
            CanPlaceEndAnchor = false,
            CanMoveSelectedNode = false,
            CanRepairSelectedNode = false,
            CanRemoveSelectedViaPoint = false,
            CanBeginPassingBranch = false,
            CanCompletePassingBranch = false,
            CanCancelPassingBranch = false,
            CanRemovePassingBranch = false,
            HasPendingPassingBranchCandidate = false,
            PassingBranchPhase = PassingBranchAuthoringPhase.Idle,
        };
    }
}
