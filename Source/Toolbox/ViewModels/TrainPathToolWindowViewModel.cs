using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;

using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;

using GetText;

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
        private bool isPathFilterRowVisible;
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
        private bool canValidatePaths;
        private bool canSavePath;
        private bool canCancelNewPath;
        private bool isBuildingRoute;
        private bool canContinuePath;
        private bool canFinishPath;
        private bool canCancelMoveNode;
        private bool canCommitMoveNode;
        private bool canPlaceStartAnchor;
        private bool canPlaceEndAnchor;
        private bool canCancelPathInteraction;
        private bool canCommitPlacement;
        private PathEditorPlacementMode placementMode;
        private int? selectedNodeWaitTime;
        private int? pendingWaitNodeIndex;
        private string pendingWaitPathId;
        private int? pendingWaitTime;
        private int pendingWaitResultVersion;
        private bool suppressSelectionCommand;
        private bool suppressWaitTimeCommand;
        private int blockedSaveFeedbackVersion;
        private string blockedSaveStatusMessage;
        private bool isRepairMode;
        private bool canMoveSelectedNode;
        private bool canRepairSelectedNode;
        private bool canRemoveSelectedViaPoint;
        private bool canSetSelectedWaitPoint;
        private bool canClearSelectedWaitPoint;
        private bool canSetSelectedReversalPoint;
        private bool canClearSelectedReversalPoint;
        private int selectedNodeCapabilityIndex = -1;
        private int? pendingSelectedNodeIndex;
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
        private bool hasPendingAmbiguousSpanCommit;
        private bool isIssuesExpanded;
        private int? inspectedCandidateFromNodeIndex;
        private int? inspectedCandidateToNodeIndex;
        private TrainPathSnapshot presentationSnapshot = TrainPathSnapshot.Empty;
        private bool syncingDiagnostics;

        public const int PathsTabIndex = 0;
        public const int EditorTabIndex = 1;

        /// <summary>Whether independent issue inspection is expanded.</summary>
        public bool IsIssuesExpanded
        {
            get => isIssuesExpanded;
            set => SetProperty(ref isIssuesExpanded, value);
        }

        /// <summary>Candidate rows scoped to the current decision or explicitly inspected issue.</summary>
        public ObservableCollection<TrainPathRouteCandidateItemViewModel> VisibleRouteCandidates { get; } = new ObservableCollection<TrainPathRouteCandidateItemViewModel>();

        /// <summary>One representative candidate per span, allowing every pending span to be selected.</summary>
        public ObservableCollection<TrainPathRouteCandidateItemViewModel> CandidateSpans { get; } = new ObservableCollection<TrainPathRouteCandidateItemViewModel>();

        /// <summary>The representative of the span currently displayed in the candidate panel.</summary>
        public TrainPathRouteCandidateItemViewModel SelectedCandidateSpan => CandidateSpans.FirstOrDefault(item =>
            item.FromNodeIndex == VisibleRouteCandidates.FirstOrDefault()?.FromNodeIndex && item.ToNodeIndex == VisibleRouteCandidates.FirstOrDefault()?.ToNodeIndex);

        /// <summary>Selects a span and previews its first candidate without accepting the pending edit.</summary>
        public void UserSelectCandidateSpan(TrainPathRouteCandidateItemViewModel span)
        {
            if (suppressSelectionCommand || span == null || span == SelectedCandidateSpan || !CandidateSpans.Contains(span))
                return;

            inspectedCandidateFromNodeIndex = span.FromNodeIndex;
            inspectedCandidateToNodeIndex = span.ToNodeIndex;
            UpdateVisibleRouteCandidates();
            ActivateRouteCandidate(span);
        }

        /// <summary>Whether the authoritative editor is awaiting a tentative main-route decision.</summary>
        public bool HasPendingAmbiguousSpanCommit => hasPendingAmbiguousSpanCommit;

        /// <summary>Whether a candidate decision or explicit issue inspection is visible.</summary>
        public bool IsCandidatePanelVisible => HasPendingAmbiguousSpanCommit || HasPendingPassingBranchCandidate
            || inspectedCandidateFromNodeIndex.HasValue;

        /// <summary>Whether ordinary entry actions can be presented without interrupting a workflow.</summary>
        public bool IsIdlePanelVisible => !IsCandidatePanelVisible && PlacementMode == PathEditorPlacementMode.None
            && !IsBuildingRoute && PassingBranchPhase == PassingBranchAuthoringPhase.Idle;

        /// <summary>Whether placement confirmation and cancellation are applicable.</summary>
        public bool IsPlacementPanelVisible => !IsCandidatePanelVisible && PlacementMode != PathEditorPlacementMode.None
            && !IsBuildingRoute && PassingBranchPhase == PassingBranchAuthoringPhase.Idle;

        /// <summary>Whether progressive route actions are applicable.</summary>
        public bool IsBuildingPanelVisible => !IsCandidatePanelVisible && IsBuildingRoute
            && PassingBranchPhase == PassingBranchAuthoringPhase.Idle;

        /// <summary>Whether a passing rejoin is being selected.</summary>
        public bool IsRejoinPanelVisible => !IsCandidatePanelVisible && PassingBranchPhase == PassingBranchAuthoringPhase.SelectingRejoin;

        public TrainPathToolWindowViewModel(TrainPathToolWindow toolWindow, ToolWindowRefreshScheduler scheduler)
            : this(toolWindow, scheduler, PathsTabIndex)
        {
        }

        /// <summary>Constructs the tool with its persisted internal tab before snapshot polling starts.</summary>
        public TrainPathToolWindowViewModel(TrainPathToolWindow toolWindow, ToolWindowRefreshScheduler scheduler, int selectedTabIndex)
            : base(scheduler, ToolWindowRefreshScheduler.BaseInterval)
        {
            ArgumentNullException.ThrowIfNull(toolWindow);

            this.toolWindow = toolWindow;
            SelectedTabIndex = selectedTabIndex;
            TogglePathFilterRowCommand = new RelayCommand(_ =>
            {
                if (string.IsNullOrEmpty(SearchText))
                    IsPathFilterRowVisible = !IsPathFilterRowVisible;
            });
            UndoCommand = new RelayCommand(_ => toolWindow.Undo(), _ => CanUndo);
            RedoCommand = new RelayCommand(_ => toolWindow.Redo(), _ => CanRedo);
            SnapToTrackCommand = new RelayCommand(_ => toolWindow.SnapToTrack(), _ => CanSnapToTrack);
            MoveSelectedNodeCommand = new RelayCommand(_ => MoveSelectedNode(), _ => CanMoveSelectedNode);
            CommitMoveNodeCommand = new RelayCommand(_ => toolWindow.CommitMoveNode(), _ => CanCommitMoveNode);
            CancelMoveNodeCommand = new RelayCommand(_ => toolWindow.CancelMoveNode(), _ => CanCancelMoveNode);
            SetStartHereCommand = new RelayCommand(_ => toolWindow.BeginStartAnchorPlacement(), _ => CanPlaceStartAnchor);
            SetEndHereCommand = new RelayCommand(_ => toolWindow.BeginEndAnchorPlacement(), _ => CanPlaceEndAnchor);
            CommitPlacementCommand = new RelayCommand(_ => toolWindow.CommitPlacement(), _ => CanCommitPlacement);
            CancelPathInteractionCommand = new RelayCommand(_ => CancelPathInteraction(), _ => CanCancelPathInteraction || IsCandidatePanelVisible);
            RepairSelectedNodeCommand = new RelayCommand(_ => RepairSelectedNode(), _ => CanRepairSelectedNode);
            ClearWaitPointCommand = new RelayCommand(_ => ClearWaitPoint(), _ => CanClearSelectedWaitPoint);
            ToggleReversalPointCommand = new RelayCommand(_ => ToggleReversalPoint(), _ => CanToggleSelectedReversalPoint);
            AddViaPointCommand = new RelayCommand(_ => AddViaPoint(), _ => CanAddViaPoint);
            RemoveViaPointCommand = new RelayCommand(_ => RemoveViaPoint(), _ => CanRemoveViaPoint);
            BeginPassingBranchCommand = new RelayCommand(_ => BeginPassingBranch(), _ => CanBeginPassingBranch);
            CompletePassingBranchCommand = new RelayCommand(_ => CompletePassingBranch(), _ => CanCompletePassingBranch);
            CancelPassingBranchCommand = new RelayCommand(_ => toolWindow.CancelPassingBranch(), _ => CanCancelPassingBranch);
            RemovePassingBranchCommand = new RelayCommand(_ => RemovePassingBranch(), _ => CanRemovePassingBranch);
            NewPathCommand = new RelayCommand(_ => toolWindow.StartNewPathPlacement(), _ => CanCreatePath);
            CancelNewPathCommand = new RelayCommand(_ => toolWindow.CancelNewPath(), _ => CanCancelNewPath);
            ContinuePathCommand = new RelayCommand(_ => toolWindow.ContinuePath(), _ => CanContinuePath);
            FinishPathCommand = new RelayCommand(_ => toolWindow.FinishPath(), _ => CanFinishPath);
            SavePathCommand = new RelayCommand(_ => toolWindow.SavePath(), _ => CanSavePath);
            ValidateAllPathsCommand = new RelayCommand(_ => ValidateAllPaths(), _ => CanValidatePaths);
            AcceptRouteCandidateCommand = new RelayCommand(_ => AcceptRouteCandidate(), _ => CanAcceptRouteCandidate);
            RepairDiagnosticCommand = new RelayCommand(_ => RepairDiagnostic(), _ => CanRepairDiagnostic);
            Diagnostics.CollectionChanged += (_, _) =>
            {
                if (!syncingDiagnostics)
                    UpdateVisibleDiagnostics();
            };
            UpdateSelectedNodeDetailRows();
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

                if (suppressWaitTimeCommand)
                    return;

                if (value is int waitTime && waitTime > 0 && CanSetSelectedWaitPoint)
                {
                    TrackSubmittedWaitCommand(value);
                    toolWindow.SetWaitPoint(SelectedNode.Index, waitTime);
                }
                else if ((!value.HasValue || value.Value == 0) && CanClearSelectedWaitPoint)
                {
                    TrackSubmittedWaitCommand(value);
                    toolWindow.ClearWaitPoint(SelectedNode.Index);
                }
                else
                {
                    pendingWaitNodeIndex = null;
                    SyncWaitTimeFromSelectedNode();
                    SetStatusMessage(CatalogManager.Catalog.GetString("Wait time cannot be changed on this node. Use Clear Wait Point to remove existing wait data when available."), true);
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

        public ImmutableArray<MapContextMenuItem> GetSelectedNodeActions()
            => SelectedNode == null
                ? ImmutableArray<MapContextMenuItem>.Empty
                : toolWindow.GetNodeActions(SelectedNode.Index);

        public void ExecuteNodeAction(MapContextMenuItem action)
        {
            ArgumentNullException.ThrowIfNull(action);
            toolWindow.ExecuteNodeAction(action.Action, action.NodeIndex);
        }

        /// <summary>Equal-cost route candidates of the current path's ambiguous spans.</summary>
        public ObservableCollection<TrainPathRouteCandidateItemViewModel> RouteCandidates { get; } = new ObservableCollection<TrainPathRouteCandidateItemViewModel>();

        /// <summary>Resolver, Simulator-event, and blocked-save diagnostics of the current path.</summary>
        public ObservableCollection<TrainPathDiagnosticItemViewModel> Diagnostics { get; } = new ObservableCollection<TrainPathDiagnosticItemViewModel>();

        /// <summary>All issues when no node is selected; otherwise direct and incident-span issues for that node.</summary>
        public ObservableCollection<TrainPathDiagnosticItemViewModel> VisibleDiagnostics { get; } = new ObservableCollection<TrainPathDiagnosticItemViewModel>();

        public RelayCommand UndoCommand { get; }

        public RelayCommand RedoCommand { get; }

        public RelayCommand SnapToTrackCommand { get; }

        public RelayCommand MoveSelectedNodeCommand { get; }

        public RelayCommand CommitMoveNodeCommand { get; }

        public RelayCommand CancelMoveNodeCommand { get; }

        public RelayCommand SetStartHereCommand { get; }

        public RelayCommand SetEndHereCommand { get; }

        public RelayCommand CommitPlacementCommand { get; }

        public RelayCommand CancelPathInteractionCommand { get; }

        public RelayCommand RepairSelectedNodeCommand { get; }

        /// <summary>Clears wait flags and payload through the selected node's authoritative command.</summary>
        public RelayCommand ClearWaitPointCommand { get; }

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

        public bool CanAcceptRouteCandidate => (!IsRepairMode || HasPendingAmbiguousSpanCommit)
            && SelectedRouteCandidate != null && VisibleRouteCandidates.Contains(SelectedRouteCandidate);

        public bool CanRepairDiagnostic => SelectedDiagnostic?.CanRepair == true && !CanCancelPathInteraction;

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

        public bool CanContinuePath
        {
            get => canContinuePath;
            private set
            {
                if (SetProperty(ref canContinuePath, value))
                    ContinuePathCommand.RaiseCanExecuteChanged();
            }
        }

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

        public bool CanCancelPathInteraction
        {
            get => canCancelPathInteraction;
            private set
            {
                if (SetProperty(ref canCancelPathInteraction, value))
                {
                    CancelPathInteractionCommand.RaiseCanExecuteChanged();
                    ContinuePathCommand.RaiseCanExecuteChanged();
                    MoveSelectedNodeCommand.RaiseCanExecuteChanged();
                    RepairSelectedNodeCommand.RaiseCanExecuteChanged();
                    ToggleReversalPointCommand.RaiseCanExecuteChanged();
                    NotifySelectedNodeEventCapabilities();
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
            set => SetProperty(ref selectedTabIndex, value is PathsTabIndex or EditorTabIndex ? value : PathsTabIndex);
        }

        /// <summary>Currently selected resolver diagnostic.</summary>
        public TrainPathDiagnosticItemViewModel SelectedDiagnostic
        {
            get => selectedDiagnostic;
            private set
            {
                if (SetProperty(ref selectedDiagnostic, value))
                    RepairDiagnosticCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Handles a user-initiated diagnostic pick from the view's SelectionChanged handler. See
        /// <see cref="Views.ToolWindowSelection"/> for why selection is delivered this way rather than through a
        /// TwoWay binding.
        /// </summary>
        public void UserSelectDiagnostic(TrainPathDiagnosticItemViewModel value)
        {
            if (suppressSelectionCommand)
                return;

            if (value != null && value == SelectedDiagnostic)
                RestoreDiagnosticSelection(null);
            ActivateDiagnostic(value);
        }

        private void ActivateDiagnostic(TrainPathDiagnosticItemViewModel value)
        {
            if (EqualityComparer<TrainPathDiagnosticItemViewModel>.Default.Equals(selectedDiagnostic, value))
                return;

            SelectedDiagnostic = value;

            if (value == null)
            {
                toolWindow.HighlightDiagnosticTarget(-1, -1, -1);
                return;
            }

            toolWindow.HighlightDiagnosticTarget(value.NodeIndex, value.FromNodeIndex, value.ToNodeIndex);
            if (value.IsAmbiguousRoute)
                NavigateToRouteCandidates(value);
        }

        /// <summary>
        /// Currently selected route candidate. Selecting a candidate previews it on the map; clearing the
        /// selection discards the preview.
        /// </summary>
        public TrainPathRouteCandidateItemViewModel SelectedRouteCandidate
        {
            get => selectedRouteCandidate;
            private set
            {
                if (SetProperty(ref selectedRouteCandidate, value))
                    AcceptRouteCandidateCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>Handles a user-initiated route candidate pick; see <see cref="UserSelectDiagnostic"/>.</summary>
        public void UserSelectRouteCandidate(TrainPathRouteCandidateItemViewModel value)
        {
            if (suppressSelectionCommand)
                return;

            ActivateRouteCandidate(value);
        }

        private void ActivateRouteCandidate(TrainPathRouteCandidateItemViewModel value)
        {
            if (EqualityComparer<TrainPathRouteCandidateItemViewModel>.Default.Equals(selectedRouteCandidate, value))
                return;

            SelectedRouteCandidate = value;

            if (value == null)
            {
                toolWindow.ClearRouteCandidatePreview();
                return;
            }

            toolWindow.PreviewRouteCandidate(value.FromNodeIndex, value.CandidateIndex);
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

        public bool CanMoveSelectedNode => canMoveSelectedNode && IsSelectedCapabilityNode() && !CanCancelPathInteraction;

        public bool CanRepairSelectedNode => canRepairSelectedNode && IsSelectedCapabilityNode() && !CanCancelPathInteraction;

        /// <summary>Whether the editor permits a positive wait on the current selected node.</summary>
        public bool CanSetSelectedWaitPoint => canSetSelectedWaitPoint && CanEditSelectedNodeEvents;

        /// <summary>Whether the editor permits clearing wait flags or payload, including in repair mode.</summary>
        public bool CanClearSelectedWaitPoint => canClearSelectedWaitPoint && CanEditSelectedNodeEvents;

        /// <summary>Whether the editor permits setting a reversal on the current selected node.</summary>
        public bool CanSetSelectedReversalPoint => canSetSelectedReversalPoint && CanEditSelectedNodeEvents;

        /// <summary>Whether the editor permits clearing a reversal, including in repair mode.</summary>
        public bool CanClearSelectedReversalPoint => canClearSelectedReversalPoint && CanEditSelectedNodeEvents;

        /// <summary>Whether the applicable set or clear reversal command is available.</summary>
        public bool CanToggleSelectedReversalPoint => SelectedNode?.HasReversalPoint == true
            ? CanClearSelectedReversalPoint : CanSetSelectedReversalPoint;

        private bool CanEditSelectedNodeEvents => IsSelectedCapabilityNode() && !CanCancelPathInteraction
            && !HasPendingAmbiguousSpanCommit && !HasPendingPassingBranchCandidate
            && PassingBranchPhase == PassingBranchAuthoringPhase.Idle;

        /// <summary>Whether positive wait editing is available; clearing has a separate command.</summary>
        public bool CanEditSelectedNodeWaitTime => CanSetSelectedWaitPoint;

        public bool CanAddViaPoint => !IsRepairMode && SelectedNode != null && !CanCancelPathInteraction;

        public bool CanRemoveViaPoint => canRemoveSelectedViaPoint && IsSelectedCapabilityNode() && !CanCancelPathInteraction;

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

        public bool CanValidatePaths
        {
            get => canValidatePaths;
            private set
            {
                if (SetProperty(ref canValidatePaths, value))
                    ValidateAllPathsCommand.RaiseCanExecuteChanged();
            }
        }

        public bool CanSavePath
        {
            get => canSavePath;
            private set
            {
                if (SetProperty(ref canSavePath, value))
                    SavePathCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>Toggles the lower header row only when no path-name filter is active.</summary>
        public RelayCommand TogglePathFilterRowCommand { get; }

        /// <summary>List-wide filter-row visibility, retained across focus changes and refreshes.</summary>
        public bool IsPathFilterRowVisible
        {
            get => isPathFilterRowVisible;
            private set => SetProperty(ref isPathFilterRowVisible, value);
        }

        public string SearchText
        {
            get => searchText;
            set
            {
                if (SetProperty(ref searchText, value))
                {
                    if (!string.IsNullOrEmpty(searchText))
                        IsPathFilterRowVisible = true;
                    ApplyPathFilter();
                }
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
            private set => SetProperty(ref selectedPath, value);
        }

        /// <summary>Whether row actions still target the selected path published by the editor snapshot.</summary>
        public bool IsActivePathActionTarget(TrainPathListItemViewModel path)
        {
            return path != null && string.Equals(path.Id, SelectedPath?.Id, StringComparison.Ordinal)
                && string.Equals(path.Id, snapshotSelectedPathId, StringComparison.Ordinal);
        }

        /// <summary>Handles a user-initiated path pick; see <see cref="UserSelectDiagnostic"/>.</summary>
        public void UserSelectPath(TrainPathListItemViewModel value)
        {
            if (suppressSelectionCommand || EqualityComparer<TrainPathListItemViewModel>.Default.Equals(selectedPath, value))
                return;

            SelectedPath = value;
            SetStatusMessage(string.Empty, false);
            // Switching path reloads the node list, so drop any pending node target from the old path.
            pendingSelectedNodeIndex = null;
            toolWindow.SelectPath(value?.Id);
        }

        public TrainPathNodeItemViewModel SelectedNode
        {
            get => selectedNode;
            private set
            {
                if (SetProperty(ref selectedNode, value))
                {
                    NotifySelectedNodeEventCapabilities();
                    UpdateVisibleDiagnostics();
                }
            }
        }

        /// <summary>Handles a user-initiated node pick; see <see cref="UserSelectDiagnostic"/>.</summary>
        public void UserSelectNode(TrainPathNodeItemViewModel value)
        {
            if (suppressSelectionCommand || EqualityComparer<TrainPathNodeItemViewModel>.Default.Equals(selectedNode, value))
                return;

            // Record the pending target so an in-flight refresh snapshot (which still carries the bridge's
            // previous SelectedNodeIndex until the highlight below is processed on the game thread) does not
            // briefly restore the old node and cause the selection to flicker back and forth.
            pendingSelectedNodeIndex = value?.Index ?? -1;

            pendingWaitNodeIndex = null;
            selectedNodeCapabilityIndex = -1;
            SelectedNode = value;
            toolWindow.HighlightNode(value?.Index ?? -1);
            SyncWaitTimeFromSelectedNode();
            UpdateSelectedNodeDetailRows();
            MoveSelectedNodeCommand.RaiseCanExecuteChanged();
            RepairSelectedNodeCommand.RaiseCanExecuteChanged();
            ToggleReversalPointCommand.RaiseCanExecuteChanged();
            AddViaPointCommand.RaiseCanExecuteChanged();
            RemoveViaPointCommand.RaiseCanExecuteChanged();
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
            ApplyPresentationSnapshot(snapshot);
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
            bool hadCancelablePathInteraction = CanCancelPathInteraction;
            CanCancelPathInteraction = snapshot.CanCancelPathInteraction;
            CanCommitPlacement = snapshot.CanCommitPlacement;
            CanCreatePath = toolWindow.CanCreatePath;
            CanValidatePaths = toolWindow.CanValidatePaths;
            CanSavePath = toolWindow.CanSavePath;
            CanCancelNewPath = snapshot.CanCancelNewPath;
            IsBuildingRoute = snapshot.IsBuildingRoute;
            CanContinuePath = snapshot.CanContinuePath;
            CanFinishPath = snapshot.CanFinishPath;
            canBeginPassingBranch = snapshot.CanBeginPassingBranch;
            canCompletePassingBranch = snapshot.CanCompletePassingBranch;
            canCancelPassingBranch = snapshot.CanCancelPassingBranch;
            canRemovePassingBranch = snapshot.CanRemovePassingBranch;
            HasPendingPassingBranchCandidate = snapshot.HasPendingPassingBranchCandidate;
            PassingBranchPhase = snapshot.PassingBranchPhase;
            UpdateVisibleRouteCandidates();
            NotifySelectedNodeEventCapabilities();
            BeginPassingBranchCommand.RaiseCanExecuteChanged();
            CompletePassingBranchCommand.RaiseCanExecuteChanged();
            CancelPassingBranchCommand.RaiseCanExecuteChanged();
            RemovePassingBranchCommand.RaiseCanExecuteChanged();

            if (wasMovingNode && !CanCancelMoveNode && IsMoveGuidanceMessage(StatusMessage))
                SetStatusMessage(string.Empty, false);
            if (hadCancelablePathInteraction && !CanCancelPathInteraction && IsMoveGuidanceMessage(StatusMessage))
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
            canSetSelectedWaitPoint = snapshot.CanSetSelectedWaitPoint;
            canClearSelectedWaitPoint = snapshot.CanClearSelectedWaitPoint;
            canSetSelectedReversalPoint = snapshot.CanSetSelectedReversalPoint;
            canClearSelectedReversalPoint = snapshot.CanClearSelectedReversalPoint;
            MoveSelectedNodeCommand.RaiseCanExecuteChanged();
            RepairSelectedNodeCommand.RaiseCanExecuteChanged();
            RemoveViaPointCommand.RaiseCanExecuteChanged();
            BeginPassingBranchCommand.RaiseCanExecuteChanged();
            CompletePassingBranchCommand.RaiseCanExecuteChanged();
            RemovePassingBranchCommand.RaiseCanExecuteChanged();
        }

        private void NotifySelectedNodeEventCapabilities()
        {
            OnPropertyChanged(nameof(CanSetSelectedWaitPoint));
            OnPropertyChanged(nameof(CanClearSelectedWaitPoint));
            OnPropertyChanged(nameof(CanSetSelectedReversalPoint));
            OnPropertyChanged(nameof(CanClearSelectedReversalPoint));
            OnPropertyChanged(nameof(CanToggleSelectedReversalPoint));
            OnPropertyChanged(nameof(CanEditSelectedNodeWaitTime));
            ClearWaitPointCommand.RaiseCanExecuteChanged();
            ToggleReversalPointCommand.RaiseCanExecuteChanged();
        }

        private void TrackSubmittedWaitCommand(int? value)
        {
            pendingWaitNodeIndex = SelectedNode.Index;
            pendingWaitPathId = snapshotSelectedPathId;
            pendingWaitTime = value;
            pendingWaitResultVersion = toolWindow.CaptureTrainPathSnapshot().CommandResultVersion;
        }

        private void ApplyCommandResultFeedback(TrainPathSnapshot snapshot)
        {
            if (snapshot.CommandResultVersion == commandResultVersion)
                return;

            commandResultVersion = snapshot.CommandResultVersion;
            if (pendingWaitNodeIndex.HasValue && snapshot.CommandResultVersion != pendingWaitResultVersion)
            {
                // A rejected command need not change the row's WaitTime, so SyncNodes alone cannot acknowledge
                // the submission. Do not replace a later selection or an edit that has not been submitted.
                if (SelectedNode?.Index == pendingWaitNodeIndex.Value
                    && string.Equals(snapshot.SelectedPathId, pendingWaitPathId, StringComparison.Ordinal)
                    && SelectedNodeWaitTime == pendingWaitTime)
                    SyncWaitTimeFromSelectedNode();
                pendingWaitNodeIndex = null;
            }
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
            SelectedTabIndex = EditorTabIndex;
            IsIssuesExpanded = true;

            if (snapshot.BlockedSaveDiagnostic is not TrainPathDiagnosticRow diagnostic)
                return;

            RestoreNodeSelection(diagnostic.NodeIndex >= 0 ? diagnostic.NodeIndex : diagnostic.FromNodeIndex);
            RestoreDiagnosticSelection(null);
            ActivateDiagnostic(Diagnostics.FirstOrDefault(item => item.Code == diagnostic.Code
                && item.NodeIndex == diagnostic.NodeIndex
                && item.FromNodeIndex == diagnostic.FromNodeIndex
                && item.ToNodeIndex == diagnostic.ToNodeIndex));
        }

        private void SyncDiagnostics(ImmutableArray<TrainPathDiagnosticRow> rows)
        {
            if (rows.IsDefault)
                rows = ImmutableArray<TrainPathDiagnosticRow>.Empty;

            syncingDiagnostics = true;
            try
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    if (i < Diagnostics.Count)
                        Diagnostics[i].Update(rows[i]);
                    else
                        Diagnostics.Add(new TrainPathDiagnosticItemViewModel(rows[i]));
                }

                for (int i = Diagnostics.Count - 1; i >= rows.Length; i--)
                    Diagnostics.RemoveAt(i);
            }
            finally
            {
                syncingDiagnostics = false;
            }

            UpdateVisibleDiagnostics();
        }

        private void UpdateVisibleDiagnostics()
        {
            TrainPathDiagnosticItemViewModel[] visible = Diagnostics.Where(item => SelectedNode == null
                || item.NodeIndex == SelectedNode.Index || item.FromNodeIndex == SelectedNode.Index
                || item.ToNodeIndex == SelectedNode.Index).ToArray();

            bool previousSuppression = suppressSelectionCommand;
            suppressSelectionCommand = true;
            try
            {
                for (int i = VisibleDiagnostics.Count - 1; i >= 0; i--)
                {
                    if (!visible.Contains(VisibleDiagnostics[i]))
                        VisibleDiagnostics.RemoveAt(i);
                }
                for (int i = 0; i < visible.Length; i++)
                {
                    if (i >= VisibleDiagnostics.Count || VisibleDiagnostics[i] != visible[i])
                        VisibleDiagnostics.Insert(i, visible[i]);
                }

                if (SelectedDiagnostic == null || !visible.Contains(SelectedDiagnostic))
                    SelectedDiagnostic = SelectedNode == null ? null : visible.FirstOrDefault();
            }
            finally
            {
                suppressSelectionCommand = previousSuppression;
            }
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

            inspectedCandidateFromNodeIndex = diagnostic.FromNodeIndex;
            inspectedCandidateToNodeIndex = diagnostic.ToNodeIndex;
            SelectedTabIndex = EditorTabIndex;
            UpdateVisibleRouteCandidates();
            ActivateRouteCandidate(candidate);
        }

        private void ApplyPresentationSnapshot(TrainPathSnapshot snapshot)
        {
            bool newWorkflow = snapshot.HasPendingAmbiguousSpanCommit && !presentationSnapshot.HasPendingAmbiguousSpanCommit
                || snapshot.HasPendingPassingBranchCandidate && !presentationSnapshot.HasPendingPassingBranchCandidate
                || snapshot.PlacementMode != PathEditorPlacementMode.None && snapshot.PlacementMode != presentationSnapshot.PlacementMode
                || snapshot.IsBuildingRoute && !presentationSnapshot.IsBuildingRoute
                || snapshot.PassingBranchPhase != PassingBranchAuthoringPhase.Idle && snapshot.PassingBranchPhase != presentationSnapshot.PassingBranchPhase;
            bool changedPath = !string.Equals(snapshot.SelectedPathId, presentationSnapshot.SelectedPathId, StringComparison.Ordinal);

            if (changedPath || presentationSnapshot.HasPendingAmbiguousSpanCommit && !snapshot.HasPendingAmbiguousSpanCommit
                || presentationSnapshot.HasPendingPassingBranchCandidate && !snapshot.HasPendingPassingBranchCandidate)
            {
                inspectedCandidateFromNodeIndex = null;
                inspectedCandidateToNodeIndex = null;
            }

            hasPendingAmbiguousSpanCommit = snapshot.HasPendingAmbiguousSpanCommit;
            
            bool previewChanged = changedPath || newWorkflow
                || snapshot.PreviewedRouteCandidateFromNodeIndex != presentationSnapshot.PreviewedRouteCandidateFromNodeIndex
                || snapshot.PreviewedRouteCandidateIndex != presentationSnapshot.PreviewedRouteCandidateIndex;
            if (previewChanged && snapshot.PreviewedRouteCandidateIndex >= 0)
            {
                TrainPathRouteCandidateItemViewModel preview = RouteCandidates.FirstOrDefault(item => item.FromNodeIndex == snapshot.PreviewedRouteCandidateFromNodeIndex
                    && item.CandidateIndex == snapshot.PreviewedRouteCandidateIndex);

                if (preview != null)
                {
                    inspectedCandidateFromNodeIndex = preview.FromNodeIndex;
                    inspectedCandidateToNodeIndex = preview.ToNodeIndex;
                }
                RestoreRouteCandidateSelection(preview);
            }
            else if (previewChanged)
            {
                RestoreRouteCandidateSelection(null);
            }

            if (newWorkflow)
                SelectedTabIndex = EditorTabIndex;

            presentationSnapshot = snapshot;
            OnPropertyChanged(nameof(HasPendingAmbiguousSpanCommit));
        }

        private void UpdateVisibleRouteCandidates()
        {
            if (inspectedCandidateFromNodeIndex.HasValue && !RouteCandidates.Any(item =>
                item.FromNodeIndex == inspectedCandidateFromNodeIndex && item.ToNodeIndex == inspectedCandidateToNodeIndex))
            {
                inspectedCandidateFromNodeIndex = null;
                inspectedCandidateToNodeIndex = null;
            }

            TrainPathRouteCandidateItemViewModel first = RouteCandidates.FirstOrDefault();
            int? from = inspectedCandidateFromNodeIndex;
            int? to = inspectedCandidateToNodeIndex;
            
            if ((HasPendingAmbiguousSpanCommit || HasPendingPassingBranchCandidate) && !RouteCandidates.Any(item => item.FromNodeIndex == from && item.ToNodeIndex == to))
            {
                from = first?.FromNodeIndex;
                to = first?.ToNodeIndex;
            }
            
            TrainPathRouteCandidateItemViewModel[] visible = RouteCandidates.Where(item => item.FromNodeIndex == from && item.ToNodeIndex == to).ToArray();
            
            suppressSelectionCommand = true;
            try
            {
                TrainPathRouteCandidateItemViewModel[] spans = RouteCandidates
                    .GroupBy(item => (item.FromNodeIndex, item.ToNodeIndex)).Select(group => group.First()).ToArray();
                for (int i = 0; i < spans.Length; i++)
                {
                    if (i >= CandidateSpans.Count)
                        CandidateSpans.Add(spans[i]);
                    else if (CandidateSpans[i] != spans[i])
                        CandidateSpans[i] = spans[i];
                }
                while (CandidateSpans.Count > spans.Length)
                    CandidateSpans.RemoveAt(CandidateSpans.Count - 1);

                if (!VisibleRouteCandidates.SequenceEqual(visible))
                {
                    VisibleRouteCandidates.Clear();
                    foreach (TrainPathRouteCandidateItemViewModel item in visible)
                        VisibleRouteCandidates.Add(item);
                }
                OnPropertyChanged(nameof(SelectedCandidateSpan));
            }
            finally
            {
                suppressSelectionCommand = false;
            }

            OnPropertyChanged(nameof(IsCandidatePanelVisible));
            OnPropertyChanged(nameof(IsIdlePanelVisible));
            OnPropertyChanged(nameof(IsPlacementPanelVisible));
            OnPropertyChanged(nameof(IsBuildingPanelVisible));
            OnPropertyChanged(nameof(IsRejoinPanelVisible));
            AcceptRouteCandidateCommand.RaiseCanExecuteChanged();
            CancelPathInteractionCommand.RaiseCanExecuteChanged();
        }

        private void CancelPathInteraction()
        {
            if (CanCancelPathInteraction)
                toolWindow.CancelPathInteraction();
            else if (HasPendingPassingBranchCandidate)
                toolWindow.CancelPassingBranch();
            else
                toolWindow.ClearRouteCandidatePreview();

            // Clearing the issue selection allows its next mouse or keyboard selection to reopen the panel.
            if (inspectedCandidateFromNodeIndex.HasValue)
                RestoreDiagnosticSelection(null);
            inspectedCandidateFromNodeIndex = null;
            inspectedCandidateToNodeIndex = null;
            RestoreRouteCandidateSelection(null);
            UpdateVisibleRouteCandidates();
        }

        private void RepairDiagnostic()
        {
            TrainPathDiagnosticItemViewModel diagnostic = SelectedDiagnostic;

            if (diagnostic?.CanRepair != true)
                return;

            toolWindow.RepairDiagnosticNode(diagnostic.NodeIndex);
        }

        private void MoveSelectedNode()
        {
            if (SelectedNode == null)
                return;

            toolWindow.BeginMoveNode(SelectedNode.Index);
        }

        private void RepairSelectedNode()
        {
            if (SelectedNode == null)
                return;

            toolWindow.RepairSelectedNode(SelectedNode.Index);
        }

        private void ClearWaitPoint()
        {
            if (!CanClearSelectedWaitPoint)
                return;

            toolWindow.ClearWaitPoint(SelectedNode.Index);
        }

        private void ToggleReversalPoint()
        {
            if (!CanToggleSelectedReversalPoint)
                return;

            int nodeIndex = SelectedNode.Index;
            if (SelectedNode.HasReversalPoint)
            {
                toolWindow.ClearReversalPoint(nodeIndex);
                return;
            }

            toolWindow.SetReversalPoint(nodeIndex);
        }

        private void AddViaPoint()
        {
            if (SelectedNode == null)
                return;

            toolWindow.BeginViaPointPlacement(SelectedNode.Index);
        }

        private void RemoveViaPoint()
        {
            if (SelectedNode == null)
                return;

            toolWindow.RemoveViaPoint(SelectedNode.Index);
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
                bool validationRan = await toolWindow.ValidateAllPaths().ConfigureAwait(true);
                SetStatusMessage(validationRan ? "Path validation complete." : "Path validation is unavailable until a route is loaded.", !validationRan);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.IO.IOException || ex is UnauthorizedAccessException)
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

            // While a user pick is still propagating to the bridge, the snapshot's selectedIndex lags behind
            // (it still reports the previous node). Ignore snapshots that disagree with the pending target so
            // the list does not flicker back to the old node and then forward again. Clear the pending marker
            // once the bridge has caught up to it.
            if (pendingSelectedNodeIndex.HasValue)
            {
                if (selectedIndex == pendingSelectedNodeIndex.Value)
                    pendingSelectedNodeIndex = null;
                else
                    return;
            }

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
                pendingWaitNodeIndex = null;
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
            ImmutableArray<ToolWindowRow>.Builder builder = ImmutableArray.CreateBuilder<ToolWindowRow>();
            builder.Add(new ToolWindowRow { Name = "Index", Value = selectedNode?.Index.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty });
            builder.Add(new ToolWindowRow { Name = "Type", Value = selectedNode?.NodeType.ToString() ?? string.Empty });
            builder.Add(new ToolWindowRow { Name = "Track Node", Value = selectedNode?.TrackNodeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty });
            builder.Add(new ToolWindowRow { Name = "Wait", Value = selectedNode?.WaitTime?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty });
            builder.Add(new ToolWindowRow { Name = "Next Main", Value = selectedNode?.NextMainNode.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty });
            builder.Add(new ToolWindowRow { Name = "Next Siding", Value = selectedNode?.NextSidingNode.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty });
            builder.Add(new ToolWindowRow { Name = "Validation", Value = selectedNode?.Validation ?? string.Empty, Bold = selectedNode?.Valid == false });
            builder.Add(new ToolWindowRow { Name = "Nearest Track Node", Value = selectedNode?.NearestTrackNodeIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty });
            builder.Add(new ToolWindowRow { Name = "Nearest Track Section", Value = FormatNearestTrackSection(selectedNode?.NearestTrackSectionIndex, selectedNode?.NearestTrackDistanceMeters) });
            return builder.ToImmutable();
        }

        private static string FormatNearestTrackSection(int? sectionIndex, double? distanceMeters)
        {
            return !sectionIndex.HasValue
                ? string.Empty
                : distanceMeters.HasValue
                ? FormattableString.Invariant($"{sectionIndex.Value} ({distanceMeters.Value:0.###} m)")
                : sectionIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
