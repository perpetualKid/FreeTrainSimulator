using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted train-path dockable tool window. Pulls an immutable
    /// <see cref="TrainPathSnapshot"/> from the <see cref="TrainPathToolWindow"/> bridge on the shared
    /// <see cref="ToolWindowRefreshScheduler"/> and exposes three views (available paths, the selected path's
    /// nodes, and its metadata). Path selection and node highlight are forwarded back to the bridge (which
    /// marshals them onto the game thread).
    /// </summary>
    internal sealed class TrainPathToolWindowViewModel : PollingToolWindowViewModel
    {
        private readonly TrainPathToolWindow toolWindow;
        private string searchText = string.Empty;
        private string statusMessage = string.Empty;
        private bool statusMessageIsWarning;
        private TrainPathListItemViewModel selectedPath;
        private TrainPathNodeItemViewModel selectedNode;
        private TrainPathRouteCandidateItemViewModel selectedRouteCandidate;
        private TrainPathDiagnosticItemViewModel selectedDiagnostic;
        private int selectedTabIndex;
        private string snapshotSelectedPathId;
        private bool canUndo;
        private bool canRedo;
        private bool canSnapToTrack;
        private bool canCreatePath;
        private bool canSavePath;
        private bool canCancelNewPath;
        private bool isBuildingRoute;
        private bool canFinishPath;
        private bool canCancelMoveNode;
        private bool canCommitMoveNode;
        private bool canPlaceStartAnchor;
        private bool canPlaceEndAnchor;
        private bool canCancelPlacement;
        private bool canCommitPlacement;
        private PathEditorPlacementMode placementMode;
        private int? selectedNodeWaitTime;
        private bool suppressSelectionCommand;
        private bool suppressWaitTimeCommand;
        private int blockedSaveFeedbackVersion;
        private string blockedSaveStatusMessage;
        private bool isRepairMode;
        private bool canMoveSelectedNode;
        private bool canRepairSelectedNode;
        private bool canRemoveSelectedViaPoint;
        private int selectedNodeCapabilityIndex = -1;
        private bool canBeginPassingBranch;
        private bool canCompletePassingBranch;
        private bool canCancelPassingBranch;
        private bool canRemovePassingBranch;
        private bool hasPendingPassingBranchCandidate;
        private int commandResultVersion;
        private PassingBranchAuthoringPhase passingBranchPhase;
        private string pathName = string.Empty;
        private string pathStart = string.Empty;
        private string pathEnd = string.Empty;
        private bool playerPath;

        public TrainPathToolWindowViewModel(TrainPathToolWindow toolWindow, ToolWindowRefreshScheduler scheduler)
            : base(scheduler, ToolWindowRefreshScheduler.BaseInterval)
        {
            ArgumentNullException.ThrowIfNull(toolWindow);

            this.toolWindow = toolWindow;
            UndoCommand = new RelayCommand(_ => toolWindow.Undo(), _ => CanUndo);
            RedoCommand = new RelayCommand(_ => toolWindow.Redo(), _ => CanRedo);
            SnapToTrackCommand = new RelayCommand(_ => toolWindow.SnapToTrack(), _ => CanSnapToTrack);
            MoveSelectedNodeCommand = new RelayCommand(_ => MoveSelectedNode(), _ => CanMoveSelectedNode);
            CommitMoveNodeCommand = new RelayCommand(_ => CommitMoveNode(), _ => CanCommitMoveNode);
            CancelMoveNodeCommand = new RelayCommand(_ => CancelMoveNode(), _ => CanCancelMoveNode);
            SetStartHereCommand = new RelayCommand(_ => BeginStartAnchorPlacement(), _ => CanPlaceStartAnchor);
            SetEndHereCommand = new RelayCommand(_ => BeginEndAnchorPlacement(), _ => CanPlaceEndAnchor);
            CommitPlacementCommand = new RelayCommand(_ => CommitPlacement(), _ => CanCommitPlacement);
            CancelPlacementCommand = new RelayCommand(_ => CancelPlacement(), _ => CanCancelPlacement);
            RepairSelectedNodeCommand = new RelayCommand(_ => RepairSelectedNode(), _ => CanRepairSelectedNode);
            ToggleReversalPointCommand = new RelayCommand(_ => ToggleReversalPoint(), _ => CanAnnotateSelectedNode);
            AddViaPointCommand = new RelayCommand(_ => AddViaPoint(), _ => CanAddViaPoint);
            RemoveViaPointCommand = new RelayCommand(_ => RemoveViaPoint(), _ => CanRemoveViaPoint);
            BeginPassingBranchCommand = new RelayCommand(_ => BeginPassingBranch(), _ => CanBeginPassingBranch);
            CompletePassingBranchCommand = new RelayCommand(_ => CompletePassingBranch(), _ => CanCompletePassingBranch);
            CancelPassingBranchCommand = new RelayCommand(_ => CancelPassingBranch(), _ => CanCancelPassingBranch);
            RemovePassingBranchCommand = new RelayCommand(_ => RemovePassingBranch(), _ => CanRemovePassingBranch);
            NewPathCommand = new RelayCommand(_ => NewPath(), _ => CanCreatePath);
            CancelNewPathCommand = new RelayCommand(_ => CancelNewPath(), _ => CanCancelNewPath);
            ContinuePathCommand = new RelayCommand(_ => ContinuePath(), _ => CanContinuePath);
            FinishPathCommand = new RelayCommand(_ => FinishPath(), _ => CanFinishPath);
            SavePathCommand = new RelayCommand(_ => toolWindow.SavePath(), _ => CanSavePath);
            ValidateAllPathsCommand = new RelayCommand(_ => ValidateAllPaths());
            AcceptRouteCandidateCommand = new RelayCommand(_ => AcceptRouteCandidate(), _ => CanAcceptRouteCandidate);
            RepairDiagnosticCommand = new RelayCommand(_ => RepairDiagnostic(), _ => CanRepairDiagnostic);
        }

        public PassingBranchAuthoringPhase PassingBranchPhase
        {
            get => passingBranchPhase;
            private set => SetProperty(ref passingBranchPhase, value);
        }

        /// <summary>
        /// Wait time of the selected node. Entering a positive value creates or updates a wait point; clearing
        /// the value or entering zero removes an existing wait point.
        /// </summary>
        public int? SelectedNodeWaitTime
        {
            get => selectedNodeWaitTime;
            set
            {
                if (!SetProperty(ref selectedNodeWaitTime, value))
                    return;

                if (suppressWaitTimeCommand || SelectedNode == null)
                    return;

                int nodeIndex = SelectedNode.Index;
                if (value is int waitTime && waitTime > 0)
                {
                    toolWindow.SetWaitPoint(nodeIndex, waitTime);
                    SetStatusMessage($"Wait point on node {nodeIndex} set to {waitTime}s.", false);
                }
                else if (SelectedNode.HasWaitPoint)
                {
                    toolWindow.ClearWaitPoint(nodeIndex);
                    SetStatusMessage($"Wait point on node {nodeIndex} cleared.", false);
                }
            }
        }

        public string Title => toolWindow.Title;

        public ObservableCollection<TrainPathListItemViewModel> Paths { get; } = new ObservableCollection<TrainPathListItemViewModel>();

        public ObservableCollection<TrainPathNodeItemViewModel> Nodes { get; } = new ObservableCollection<TrainPathNodeItemViewModel>();

        public ObservableCollection<DebugToolWindowRowViewModel> SelectedNodeDetailRows { get; } = new ObservableCollection<DebugToolWindowRowViewModel>();

        public ObservableCollection<DebugToolWindowRowViewModel> Metadata { get; } = new ObservableCollection<DebugToolWindowRowViewModel>();

        public string PathName
        {
            get => pathName;
            set => SetProperty(ref pathName, value);
        }

        public string PathStart
        {
            get => pathStart;
            set => SetProperty(ref pathStart, value);
        }

        public string PathEnd
        {
            get => pathEnd;
            set => SetProperty(ref pathEnd, value);
        }

        public bool PlayerPath
        {
            get => playerPath;
            set => SetProperty(ref playerPath, value);
        }

        public void CommitMetadata()
        {
            toolWindow.SetMetadata(PathName, PathStart, PathEnd, PlayerPath);
        }

        /// <summary>Equal-cost route candidates of the current path's ambiguous spans.</summary>
        public ObservableCollection<TrainPathRouteCandidateItemViewModel> RouteCandidates { get; } = new ObservableCollection<TrainPathRouteCandidateItemViewModel>();

        /// <summary>Resolver diagnostics of the current path.</summary>
        public ObservableCollection<TrainPathDiagnosticItemViewModel> Diagnostics { get; } = new ObservableCollection<TrainPathDiagnosticItemViewModel>();

        public RelayCommand UndoCommand { get; }

        public RelayCommand RedoCommand { get; }

        public RelayCommand SnapToTrackCommand { get; }

        public RelayCommand MoveSelectedNodeCommand { get; }

        public RelayCommand CommitMoveNodeCommand { get; }

        public RelayCommand CancelMoveNodeCommand { get; }

        public RelayCommand SetStartHereCommand { get; }

        public RelayCommand SetEndHereCommand { get; }

        public RelayCommand CommitPlacementCommand { get; }

        public RelayCommand CancelPlacementCommand { get; }

        public RelayCommand RepairSelectedNodeCommand { get; }

        public RelayCommand ToggleReversalPointCommand { get; }

        public RelayCommand AddViaPointCommand { get; }

        public RelayCommand RemoveViaPointCommand { get; }

        public RelayCommand BeginPassingBranchCommand { get; }

        public RelayCommand CompletePassingBranchCommand { get; }

        public RelayCommand CancelPassingBranchCommand { get; }

        public RelayCommand RemovePassingBranchCommand { get; }

        public RelayCommand NewPathCommand { get; }

        public RelayCommand CancelNewPathCommand { get; }

        public RelayCommand ContinuePathCommand { get; }

        public RelayCommand FinishPathCommand { get; }

        public RelayCommand SavePathCommand { get; }

        public RelayCommand ValidateAllPathsCommand { get; }

        public RelayCommand AcceptRouteCandidateCommand { get; }

        public RelayCommand RepairDiagnosticCommand { get; }

        public bool CanAcceptRouteCandidate => !IsRepairMode && SelectedRouteCandidate != null && !CanCancelPlacement;

        public bool CanRepairDiagnostic => SelectedDiagnostic?.CanRepair == true && !CanCancelPlacement;

        /// <summary>Whether the selected path has fatal diagnostics and is safely loaded as raw authored data.</summary>
        public bool IsRepairMode
        {
            get => isRepairMode;
            private set
            {
                if (!SetProperty(ref isRepairMode, value))
                    return;

                OnPropertyChanged(nameof(AreRepairNodeActionsVisible));
                AcceptRouteCandidateCommand.RaiseCanExecuteChanged();
                AddViaPointCommand.RaiseCanExecuteChanged();
                RemoveViaPointCommand.RaiseCanExecuteChanged();
                BeginPassingBranchCommand.RaiseCanExecuteChanged();
                CompletePassingBranchCommand.RaiseCanExecuteChanged();
                RemovePassingBranchCommand.RaiseCanExecuteChanged();
                BeginPassingBranchCommand.RaiseCanExecuteChanged();
                CompletePassingBranchCommand.RaiseCanExecuteChanged();
                CancelPassingBranchCommand.RaiseCanExecuteChanged();
                RemovePassingBranchCommand.RaiseCanExecuteChanged();
                ToggleReversalPointCommand.RaiseCanExecuteChanged();
                ContinuePathCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>Whether repair-only selected-node actions should be presented in the Path Nodes toolbar.</summary>
        public bool AreRepairNodeActionsVisible => IsRepairMode;

        public PathEditorPlacementMode PlacementMode
        {
            get => placementMode;
            private set => SetProperty(ref placementMode, value);
        }

        public bool IsBuildingRoute
        {
            get => isBuildingRoute;
            private set
            {
                if (SetProperty(ref isBuildingRoute, value))
                    ContinuePathCommand.RaiseCanExecuteChanged();
            }
        }

        public bool CanContinuePath => !IsRepairMode && CanSavePath && !CanCancelPlacement;

        public bool CanFinishPath
        {
            get => canFinishPath;
            private set
            {
                if (SetProperty(ref canFinishPath, value))
                    FinishPathCommand.RaiseCanExecuteChanged();
            }
        }

        public bool CanCancelNewPath
        {
            get => canCancelNewPath;
            private set
            {
                if (SetProperty(ref canCancelNewPath, value))
                    CancelNewPathCommand.RaiseCanExecuteChanged();
            }
        }

        public bool CanPlaceStartAnchor
        {
            get => canPlaceStartAnchor;
            private set
            {
                if (SetProperty(ref canPlaceStartAnchor, value))
                    SetStartHereCommand.RaiseCanExecuteChanged();
            }
        }

        public bool CanPlaceEndAnchor
        {
            get => canPlaceEndAnchor;
            private set
            {
                if (SetProperty(ref canPlaceEndAnchor, value))
                    SetEndHereCommand.RaiseCanExecuteChanged();
            }
        }

        public bool CanCancelPlacement
        {
            get => canCancelPlacement;
            private set
            {
                if (SetProperty(ref canCancelPlacement, value))
                {
                    CancelPlacementCommand.RaiseCanExecuteChanged();
                    ContinuePathCommand.RaiseCanExecuteChanged();
                    MoveSelectedNodeCommand.RaiseCanExecuteChanged();
                    RepairSelectedNodeCommand.RaiseCanExecuteChanged();
                    ToggleReversalPointCommand.RaiseCanExecuteChanged();
                    AddViaPointCommand.RaiseCanExecuteChanged();
                    RemoveViaPointCommand.RaiseCanExecuteChanged();
                    AcceptRouteCandidateCommand.RaiseCanExecuteChanged();
                    RepairDiagnosticCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool CanCommitPlacement
        {
            get => canCommitPlacement;
            private set
            {
                if (SetProperty(ref canCommitPlacement, value))
                    CommitPlacementCommand.RaiseCanExecuteChanged();
            }
        }

        public int SelectedTabIndex
        {
            get => selectedTabIndex;
            set => SetProperty(ref selectedTabIndex, value);
        }

        /// <summary>Currently selected resolver diagnostic.</summary>
        public TrainPathDiagnosticItemViewModel SelectedDiagnostic
        {
            get => selectedDiagnostic;
            set
            {
                if (!SetProperty(ref selectedDiagnostic, value))
                    return;

                RepairDiagnosticCommand.RaiseCanExecuteChanged();
                if (suppressSelectionCommand)
                    return;

                if (value == null)
                {
                    toolWindow.HighlightDiagnosticTarget(-1, -1, -1);
                    return;
                }

                toolWindow.HighlightDiagnosticTarget(value.NodeIndex, value.FromNodeIndex, value.ToNodeIndex);
                if (value.IsAmbiguousRoute)
                    NavigateToRouteCandidates(value);
            }
        }

        /// <summary>
        /// Currently selected route candidate. Selecting a candidate previews it on the map; clearing the
        /// selection discards the preview.
        /// </summary>
        public TrainPathRouteCandidateItemViewModel SelectedRouteCandidate
        {
            get => selectedRouteCandidate;
            set
            {
                if (!SetProperty(ref selectedRouteCandidate, value))
                    return;

                AcceptRouteCandidateCommand.RaiseCanExecuteChanged();
                if (suppressSelectionCommand)
                    return;

                if (value == null)
                {
                    toolWindow.ClearRouteCandidatePreview();
                    return;
                }

                toolWindow.PreviewRouteCandidate(value.FromNodeIndex, value.CandidateIndex);
                SetStatusMessage($"Previewing route candidate {value.CandidateIndex + 1} for nodes {value.FromNodeIndex}-{value.ToNodeIndex}.", false);
            }
        }

        public bool CanUndo
        {
            get => canUndo;
            private set
            {
                if (SetProperty(ref canUndo, value))
                    UndoCommand.RaiseCanExecuteChanged();
            }
        }

        public bool CanCancelMoveNode
        {
            get => canCancelMoveNode;
            private set
            {
                if (SetProperty(ref canCancelMoveNode, value))
                {
                    CancelMoveNodeCommand.RaiseCanExecuteChanged();
                    MoveSelectedNodeCommand.RaiseCanExecuteChanged();
                    RepairDiagnosticCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool CanCommitMoveNode
        {
            get => canCommitMoveNode;
            private set
            {
                if (SetProperty(ref canCommitMoveNode, value))
                    CommitMoveNodeCommand.RaiseCanExecuteChanged();
            }
        }

        public bool CanMoveSelectedNode => canMoveSelectedNode && IsSelectedCapabilityNode() && !CanCancelPlacement;

        public bool CanRepairSelectedNode => canRepairSelectedNode && IsSelectedCapabilityNode() && !CanCancelPlacement;

        public bool CanAnnotateSelectedNode => !IsRepairMode && SelectedNode != null && !CanCancelPlacement;

        public bool CanAddViaPoint => !IsRepairMode && SelectedNode != null && !CanCancelPlacement;

        public bool CanRemoveViaPoint => canRemoveSelectedViaPoint && IsSelectedCapabilityNode() && !CanCancelPlacement;

        public bool CanBeginPassingBranch => canBeginPassingBranch && IsSelectedCapabilityNode();

        public bool CanCompletePassingBranch => canCompletePassingBranch && IsSelectedCapabilityNode();

        public bool CanCancelPassingBranch => canCancelPassingBranch;

        public bool CanRemovePassingBranch => canRemovePassingBranch && IsSelectedCapabilityNode();

        public bool HasPendingPassingBranchCandidate
        {
            get => hasPendingPassingBranchCandidate;
            private set => SetProperty(ref hasPendingPassingBranchCandidate, value);
        }

        public bool CanRedo
        {
            get => canRedo;
            private set
            {
                if (SetProperty(ref canRedo, value))
                    RedoCommand.RaiseCanExecuteChanged();
            }
        }

        public bool CanSnapToTrack
        {
            get => canSnapToTrack;
            private set
            {
                if (SetProperty(ref canSnapToTrack, value))
                    SnapToTrackCommand.RaiseCanExecuteChanged();
            }
        }

        public bool CanCreatePath
        {
            get => canCreatePath;
            private set
            {
                if (SetProperty(ref canCreatePath, value))
                    NewPathCommand.RaiseCanExecuteChanged();
            }
        }

        public bool CanSavePath
        {
            get => canSavePath;
            private set
            {
                if (SetProperty(ref canSavePath, value))
                {
                    SavePathCommand.RaiseCanExecuteChanged();
                    ContinuePathCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SearchText
        {
            get => searchText;
            set
            {
                if (SetProperty(ref searchText, value))
                    ApplyPathFilter();
            }
        }

        public string StatusMessage
        {
            get => statusMessage;
            private set => SetProperty(ref statusMessage, value);
        }

        public bool StatusMessageIsWarning
        {
            get => statusMessageIsWarning;
            private set => SetProperty(ref statusMessageIsWarning, value);
        }

        public TrainPathListItemViewModel SelectedPath
        {
            get => selectedPath;
            set
            {
                if (!SetProperty(ref selectedPath, value))
                    return;

                if (suppressSelectionCommand)
                    return;

                SetStatusMessage(string.Empty, false);
                toolWindow.SelectPath(value?.Id);
            }
        }

        public TrainPathNodeItemViewModel SelectedNode
        {
            get => selectedNode;
            set
            {
                if (!SetProperty(ref selectedNode, value))
                    return;

                if (suppressSelectionCommand)
                    return;

                selectedNodeCapabilityIndex = -1;
                toolWindow.HighlightNode(value?.Index ?? -1);
                SyncWaitTimeFromSelectedNode();
                UpdateSelectedNodeDetailRows();
                MoveSelectedNodeCommand.RaiseCanExecuteChanged();
                RepairSelectedNodeCommand.RaiseCanExecuteChanged();
                ToggleReversalPointCommand.RaiseCanExecuteChanged();
                AddViaPointCommand.RaiseCanExecuteChanged();
                RemoveViaPointCommand.RaiseCanExecuteChanged();
            }
        }

        protected override void OnStarted() => toolWindow.Active = true;

        protected override void OnStopped() => toolWindow.Active = false;

        protected override void Refresh()
        {
            TrainPathSnapshot snapshot = toolWindow.CaptureTrainPathSnapshot();

            SyncPaths(snapshot.Paths);
            SyncNodes(snapshot.Nodes, snapshot.SelectedNodeIndex);
            UpdateSelectedNodeDetailRows();
            DebugToolWindowRowViewModel.Sync(Metadata, snapshot.Metadata);
            SyncEditableMetadata(snapshot);
            SyncRouteCandidates(snapshot.RouteCandidates);
            SyncDiagnostics(snapshot.Diagnostics);
            ApplyBlockedSaveFeedback(snapshot);
            ApplyCommandResultFeedback(snapshot);
            CanUndo = snapshot.CanUndo;
            CanRedo = snapshot.CanRedo;
            CanSnapToTrack = snapshot.CanSnapToTrack;
            IsRepairMode = snapshot.IsRepairMode;
            UpdateSelectedNodeCapabilities(snapshot);
            bool wasMovingNode = CanCancelMoveNode;
            CanCancelMoveNode = snapshot.CanCancelMoveNode;
            CanCommitMoveNode = snapshot.CanCommitMoveNode;
            PlacementMode = snapshot.PlacementMode;
            CanPlaceStartAnchor = snapshot.CanPlaceStartAnchor;
            CanPlaceEndAnchor = snapshot.CanPlaceEndAnchor;
            bool wasPlacing = CanCancelPlacement;
            CanCancelPlacement = snapshot.CanCancelPlacement;
            CanCommitPlacement = snapshot.CanCommitPlacement;
            CanCreatePath = toolWindow.CanCreatePath;
            CanSavePath = toolWindow.CanSavePath;
            CanCancelNewPath = snapshot.CanCancelNewPath;
            IsBuildingRoute = snapshot.IsBuildingRoute;
            CanFinishPath = snapshot.CanFinishPath;
            canBeginPassingBranch = snapshot.CanBeginPassingBranch;
            canCompletePassingBranch = snapshot.CanCompletePassingBranch;
            canCancelPassingBranch = snapshot.CanCancelPassingBranch;
            canRemovePassingBranch = snapshot.CanRemovePassingBranch;
            HasPendingPassingBranchCandidate = snapshot.HasPendingPassingBranchCandidate;
            PassingBranchPhase = snapshot.PassingBranchPhase;
            BeginPassingBranchCommand.RaiseCanExecuteChanged();
            CompletePassingBranchCommand.RaiseCanExecuteChanged();
            CancelPassingBranchCommand.RaiseCanExecuteChanged();
            RemovePassingBranchCommand.RaiseCanExecuteChanged();

            if (wasMovingNode && !CanCancelMoveNode && IsMoveGuidanceMessage(StatusMessage))
                SetStatusMessage(string.Empty, false);
            if (wasPlacing && !CanCancelPlacement && IsMoveGuidanceMessage(StatusMessage))
                SetStatusMessage(string.Empty, false);

            if (!string.Equals(snapshotSelectedPathId, snapshot.SelectedPathId, StringComparison.Ordinal))
            {
                snapshotSelectedPathId = snapshot.SelectedPathId;
                UpdateSelectedPathFromSnapshot();
            }
        }

        private void SyncEditableMetadata(TrainPathSnapshot snapshot)
        {
            PathName = snapshot.PathName ?? string.Empty;
            PathStart = snapshot.PathStart ?? string.Empty;
            PathEnd = snapshot.PathEnd ?? string.Empty;
            PlayerPath = snapshot.PlayerPath;
        }

        private void UpdateSelectedNodeCapabilities(TrainPathSnapshot snapshot)
        {
            selectedNodeCapabilityIndex = snapshot.SelectedNodeIndex;
            canMoveSelectedNode = snapshot.CanMoveSelectedNode;
            canRepairSelectedNode = snapshot.CanRepairSelectedNode;
            canRemoveSelectedViaPoint = snapshot.CanRemoveSelectedViaPoint;
            MoveSelectedNodeCommand.RaiseCanExecuteChanged();
            RepairSelectedNodeCommand.RaiseCanExecuteChanged();
            RemoveViaPointCommand.RaiseCanExecuteChanged();
            BeginPassingBranchCommand.RaiseCanExecuteChanged();
            CompletePassingBranchCommand.RaiseCanExecuteChanged();
            RemovePassingBranchCommand.RaiseCanExecuteChanged();
        }

        private void ApplyCommandResultFeedback(TrainPathSnapshot snapshot)
        {
            if (snapshot.CommandResultVersion == commandResultVersion)
                return;

            commandResultVersion = snapshot.CommandResultVersion;
            if (!string.IsNullOrWhiteSpace(snapshot.CommandResultMessage))
                SetStatusMessage(snapshot.CommandResultMessage, snapshot.CommandResultIsWarning);
        }

        private bool IsSelectedCapabilityNode()
        {
            return SelectedNode != null && SelectedNode.Index == selectedNodeCapabilityIndex;
        }

        private void ApplyBlockedSaveFeedback(TrainPathSnapshot snapshot)
        {
            if (snapshot.BlockedSaveFeedbackVersion == blockedSaveFeedbackVersion)
                return;

            blockedSaveFeedbackVersion = snapshot.BlockedSaveFeedbackVersion;
            if (string.IsNullOrWhiteSpace(snapshot.BlockedSaveMessage))
            {
                if (string.Equals(StatusMessage, blockedSaveStatusMessage, StringComparison.Ordinal))
                    SetStatusMessage(string.Empty, false);
                blockedSaveStatusMessage = null;
                return;
            }

            blockedSaveStatusMessage = snapshot.BlockedSaveMessage;
            SetStatusMessage(snapshot.BlockedSaveMessage, true);
            SelectedTabIndex = 2;

            if (snapshot.BlockedSaveDiagnostic is not TrainPathDiagnosticRow diagnostic)
                return;

            SelectedDiagnostic = Diagnostics.FirstOrDefault(item => item.Code == diagnostic.Code
                && item.NodeIndex == diagnostic.NodeIndex
                && item.FromNodeIndex == diagnostic.FromNodeIndex
                && item.ToNodeIndex == diagnostic.ToNodeIndex);
        }

        private void ContinuePath()
        {
            toolWindow.ContinuePath();
            SetStatusMessage("Click track to add route points; finish explicitly when the path is complete.", false);
        }

        private void FinishPath()
        {
            toolWindow.FinishPath();
            SetStatusMessage("Path finished.", false);
        }

        private void CancelNewPath()
        {
            toolWindow.CancelNewPath();
            SetStatusMessage("New path canceled.", false);
        }

        private void NewPath()
        {
            toolWindow.StartNewPathPlacement();
            SetStatusMessage("Click track to set the start; continue clicking to add route points, then double-click to finish.", false);
        }

        private void BeginStartAnchorPlacement()
        {
            toolWindow.BeginStartAnchorPlacement();
            SetStatusMessage("Select a valid track location for the start anchor.", false);
        }

        private void BeginEndAnchorPlacement()
        {
            toolWindow.BeginEndAnchorPlacement();
            SetStatusMessage("Select a valid track location for the end anchor.", false);
        }

        private void CommitPlacement()
        {
            toolWindow.CommitPlacement();
            SetStatusMessage(IsBuildingRoute ? "Route point added; select the next point or finish the path." : "Placement committed.", false);
        }

        private void CancelPlacement()
        {
            PathEditorPlacementMode canceledMode = PlacementMode;
            toolWindow.CancelPlacement();
            SetStatusMessage(canceledMode switch
            {
                PathEditorPlacementMode.StartAnchor => "Start anchor placement canceled.",
                PathEditorPlacementMode.EndAnchor => "End anchor placement canceled.",
                PathEditorPlacementMode.BuildRoute => "Current route-point placement canceled; committed points were retained.",
                _ => "Node move canceled.",
            }, false);
        }

        private void SyncDiagnostics(ImmutableArray<TrainPathDiagnosticRow> rows)
        {
            if (rows.IsDefault)
                rows = ImmutableArray<TrainPathDiagnosticRow>.Empty;

            TrainPathDiagnosticItemViewModel selected = SelectedDiagnostic;
            for (int i = 0; i < rows.Length; i++)
            {
                if (i < Diagnostics.Count)
                    Diagnostics[i].Update(rows[i]);
                else
                    Diagnostics.Add(new TrainPathDiagnosticItemViewModel(rows[i]));
            }

            for (int i = Diagnostics.Count - 1; i >= rows.Length; i--)
                Diagnostics.RemoveAt(i);

            if (selected != null && !Diagnostics.Contains(selected))
                RestoreDiagnosticSelection(null);
        }

        private void RestoreDiagnosticSelection(TrainPathDiagnosticItemViewModel diagnostic)
        {
            suppressSelectionCommand = true;
            try
            {
                SelectedDiagnostic = diagnostic;
            }
            finally
            {
                suppressSelectionCommand = false;
            }
        }

        private void NavigateToRouteCandidates(TrainPathDiagnosticItemViewModel diagnostic)
        {
            TrainPathRouteCandidateItemViewModel candidate = RouteCandidates.FirstOrDefault(item =>
                item.FromNodeIndex == diagnostic.FromNodeIndex && item.ToNodeIndex == diagnostic.ToNodeIndex);
            if (candidate == null)
            {
                SetStatusMessage($"No route candidates are available for {diagnostic.Target}.", true);
                return;
            }

            SelectedTabIndex = 3;
            SelectedRouteCandidate = candidate;
        }

        private void RepairDiagnostic()
        {
            TrainPathDiagnosticItemViewModel diagnostic = SelectedDiagnostic;
            if (diagnostic?.CanRepair != true)
                return;

            toolWindow.RepairDiagnosticNode(diagnostic.NodeIndex);
            SetStatusMessage($"Repair node {diagnostic.NodeIndex} requested.", false);
        }

        private void MoveSelectedNode()
        {
            if (SelectedNode == null)
                return;

            toolWindow.BeginMoveNode(SelectedNode.Index);
            SetStatusMessage($"Select a new track location for node {SelectedNode.Index}.", false);
        }

        private void CancelMoveNode()
        {
            toolWindow.CancelMoveNode();
            SetStatusMessage("Node move canceled.", false);
        }

        private void CommitMoveNode()
        {
            toolWindow.CommitMoveNode();
            SetStatusMessage("Commit move requested.", false);
        }

        private void RepairSelectedNode()
        {
            if (SelectedNode == null)
                return;

            toolWindow.RepairSelectedNode(SelectedNode.Index);
            SetStatusMessage($"Repair selected node {SelectedNode.Index} requested.", false);
        }

        private void ToggleReversalPoint()
        {
            if (SelectedNode == null)
                return;

            int nodeIndex = SelectedNode.Index;
            if (SelectedNode.HasReversalPoint)
            {
                toolWindow.ClearReversalPoint(nodeIndex);
                SetStatusMessage($"Clear reversal point on node {nodeIndex} requested.", false);
                return;
            }

            toolWindow.SetReversalPoint(nodeIndex);
            SetStatusMessage($"Reversal point on node {nodeIndex} requested.", false);
        }

        private void AddViaPoint()
        {
            if (SelectedNode == null)
                return;

            toolWindow.BeginViaPointPlacement(SelectedNode.Index);
            SetStatusMessage($"Select a track location to insert a via point after node {SelectedNode.Index}.", false);
        }

        private void RemoveViaPoint()
        {
            if (SelectedNode == null)
                return;

            toolWindow.RemoveViaPoint(SelectedNode.Index);
            SetStatusMessage($"Remove via point {SelectedNode.Index} requested.", false);
        }

        private void BeginPassingBranch()
        {
            if (SelectedNode == null)
                return;

            toolWindow.BeginPassingBranch(SelectedNode.Index);
        }

        private void CompletePassingBranch()
        {
            if (SelectedNode == null)
                return;

            toolWindow.CompletePassingBranch(SelectedNode.Index);
        }

        private void CancelPassingBranch()
        {
            toolWindow.CancelPassingBranch();
        }

        private void RemovePassingBranch()
        {
            if (SelectedNode == null)
                return;

            toolWindow.RemovePassingBranch(SelectedNode.Index);
        }

        // Runs the forced 'validate all paths' bridge.
        // being thrown from the ICommand handler so a validation failure never crashes the UI thread.
        private async void ValidateAllPaths()
        {
            try
            {
                SetStatusMessage("Validating paths...", false);
                await toolWindow.ValidateAllPaths().ConfigureAwait(true);
                SetStatusMessage("Path validation complete.", false);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.IO.IOException)
            {
                SetStatusMessage($"Path validation failed: {ex.Message}", true);
            }
        }

        private void SyncRouteCandidates(ImmutableArray<TrainPathRouteCandidateRow> rows)
        {
            if (rows.IsDefault)
                rows = ImmutableArray<TrainPathRouteCandidateRow>.Empty;

            // Preserve the bound selection across the in-place sync without re-triggering a map preview.
            TrainPathRouteCandidateItemViewModel selected = SelectedRouteCandidate;
            for (int i = 0; i < rows.Length; i++)
            {
                if (i < RouteCandidates.Count)
                    RouteCandidates[i].Update(rows[i]);
                else
                    RouteCandidates.Add(new TrainPathRouteCandidateItemViewModel(rows[i]));
            }

            for (int i = RouteCandidates.Count - 1; i >= rows.Length; i--)
                RouteCandidates.RemoveAt(i);

            if (selected != null && !RouteCandidates.Contains(selected))
                RestoreRouteCandidateSelection(null);
        }

        private void RestoreRouteCandidateSelection(TrainPathRouteCandidateItemViewModel candidate)
        {
            suppressSelectionCommand = true;
            try
            {
                SelectedRouteCandidate = candidate;
            }
            finally
            {
                suppressSelectionCommand = false;
            }
        }

        private void AcceptRouteCandidate()
        {
            TrainPathRouteCandidateItemViewModel candidate = SelectedRouteCandidate;
            if (candidate == null)
                return;

            toolWindow.AcceptRouteCandidate(candidate.FromNodeIndex, candidate.CandidateIndex);
            SetStatusMessage($"Accept route candidate {candidate.CandidateIndex + 1} for nodes {candidate.FromNodeIndex}-{candidate.ToNodeIndex} requested.", false);
            RestoreRouteCandidateSelection(null);
        }

        private void SetStatusMessage(string message, bool isWarning)
        {
            StatusMessage = message;
            StatusMessageIsWarning = isWarning;
        }

        private static bool IsMoveGuidanceMessage(string message)
        {
            return message?.StartsWith("Select a new track location", StringComparison.Ordinal) == true
                || message?.StartsWith("Select a track location", StringComparison.Ordinal) == true;
        }

        private void SyncPaths(ImmutableArray<TrainPathListRow> rows)
        {
            // Preserve the bound selection across the in-place sync.
            string selectedId = SelectedPath?.Id;
            for (int i = 0; i < rows.Length; i++)
            {
                TrainPathListRow row = rows[i];
                if (i < Paths.Count)
                    Paths[i].Update(row.Id, row.Name, row.ValidationState, row.HasUnsavedChanges);
                else
                    Paths.Add(new TrainPathListItemViewModel(row.Id, row.Name, row.ValidationState, row.HasUnsavedChanges));
            }

            for (int i = Paths.Count - 1; i >= rows.Length; i--)
                Paths.RemoveAt(i);

            ApplyPathFilter();

            if (selectedId != null && (SelectedPath == null || !string.Equals(SelectedPath.Id, selectedId, StringComparison.Ordinal)))
                RestorePathSelection(selectedId);
        }

        private void SyncNodes(ImmutableArray<TrainPathNodeRow> rows, int selectedIndex)
        {
            int? previousSelectedWaitTime = SelectedNode?.WaitTime;

            for (int i = 0; i < rows.Length; i++)
            {
                TrainPathNodeRow row = rows[i];
                if (i < Nodes.Count)
                    Nodes[i].Update(row);
                else
                    Nodes.Add(new TrainPathNodeItemViewModel(row));
            }

            for (int i = Nodes.Count - 1; i >= rows.Length; i--)
                Nodes.RemoveAt(i);

            if (selectedIndex >= 0)
            {
                if (SelectedNode == null || SelectedNode.Index != selectedIndex)
                    RestoreNodeSelection(selectedIndex);
                else if (SelectedNode.WaitTime != previousSelectedWaitTime)
                    SyncWaitTimeFromSelectedNode();
            }
            else if (SelectedNode != null)
            {
                RestoreNodeSelection(-1);
            }
        }

        private void ApplyPathFilter()
        {
            foreach (TrainPathListItemViewModel path in Paths)
                path.IsVisible = string.IsNullOrEmpty(searchText) || (path.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private void UpdateSelectedPathFromSnapshot()
        {
            suppressSelectionCommand = true;
            try
            {
                SelectedPath = snapshotSelectedPathId == null
                    ? null
                    : Paths.FirstOrDefault(p => string.Equals(p.Id, snapshotSelectedPathId, StringComparison.Ordinal));
            }
            finally
            {
                suppressSelectionCommand = false;
            }
        }

        private void RestorePathSelection(string pathId)
        {
            suppressSelectionCommand = true;
            try
            {
                SelectedPath = Paths.FirstOrDefault(p => string.Equals(p.Id, pathId, StringComparison.Ordinal));
            }
            finally
            {
                suppressSelectionCommand = false;
            }
        }

        private void RestoreNodeSelection(int index)
        {
            suppressSelectionCommand = true;
            try
            {
                SelectedNode = Nodes.FirstOrDefault(n => n.Index == index);
                SyncWaitTimeFromSelectedNode();
                UpdateSelectedNodeDetailRows();
            }
            finally
            {
                suppressSelectionCommand = false;
            }
        }

        private void SyncWaitTimeFromSelectedNode()
        {
            suppressWaitTimeCommand = true;
            try
            {
                SelectedNodeWaitTime = SelectedNode?.WaitTime;
            }
            finally
            {
                suppressWaitTimeCommand = false;
            }
        }

        private void UpdateSelectedNodeDetailRows()
        {
            ImmutableArray<ToolWindowRow> rows = BuildSelectedNodeDetailRows(SelectedNode);
            DebugToolWindowRowViewModel.Sync(SelectedNodeDetailRows, rows);
        }

        private static ImmutableArray<ToolWindowRow> BuildSelectedNodeDetailRows(TrainPathNodeItemViewModel selectedNode)
        {
            if (selectedNode == null)
                return ImmutableArray<ToolWindowRow>.Empty;

            ImmutableArray<ToolWindowRow>.Builder builder = ImmutableArray.CreateBuilder<ToolWindowRow>();
            builder.Add(new ToolWindowRow { Name = "Index", Value = selectedNode.Index.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            builder.Add(new ToolWindowRow { Name = "Type", Value = selectedNode.NodeType.ToString() });
            builder.Add(new ToolWindowRow { Name = "Track Node", Value = selectedNode.TrackNodeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            builder.Add(new ToolWindowRow { Name = "Wait", Value = selectedNode.WaitTime?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty });
            builder.Add(new ToolWindowRow { Name = "Next Main", Value = selectedNode.NextMainNode.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            builder.Add(new ToolWindowRow { Name = "Next Siding", Value = selectedNode.NextSidingNode.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            builder.Add(new ToolWindowRow { Name = "Validation", Value = selectedNode.Validation ?? string.Empty, Bold = !selectedNode.Valid });
            builder.Add(new ToolWindowRow { Name = "Nearest Track Node", Value = selectedNode.NearestTrackNodeIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty });
            builder.Add(new ToolWindowRow { Name = "Nearest Track Section", Value = selectedNode.NearestTrackSectionIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty });
            builder.Add(new ToolWindowRow { Name = "Nearest Track Distance", Value = FormatMeters(selectedNode.NearestTrackDistanceMeters) });
            return builder.ToImmutable();
        }

        private static string FormatMeters(double? value)
        {
            return value.HasValue ? FormattableString.Invariant($"{value.Value:0.###} m") : string.Empty;
        }
    }
}
