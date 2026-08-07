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
        private string snapshotSelectedPathId;
        private bool canUndo;
        private bool canRedo;
        private bool canSnapToTrack;
        private bool canCreatePath;
        private bool canSavePath;
        private bool canCancelMoveNode;
        private bool canCommitMoveNode;
        private int? selectedNodeWaitTime;
        private bool suppressSelectionCommand;
        private bool suppressWaitTimeCommand;

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
            RepairSelectedNodeCommand = new RelayCommand(_ => RepairSelectedNode(), _ => CanRepairSelectedNode);
            ToggleReversalPointCommand = new RelayCommand(_ => ToggleReversalPoint(), _ => CanAnnotateSelectedNode);
            AddViaPointCommand = new RelayCommand(_ => AddViaPoint(), _ => CanAnnotateSelectedNode);
            RemoveViaPointCommand = new RelayCommand(_ => RemoveViaPoint(), _ => CanAnnotateSelectedNode);
            NewPathCommand = new RelayCommand(_ => toolWindow.CreatePath(), _ => CanCreatePath);
            SavePathCommand = new RelayCommand(_ => toolWindow.SavePath(), _ => CanSavePath);
            ValidateAllPathsCommand = new RelayCommand(_ => ValidateAllPaths());
            AcceptRouteCandidateCommand = new RelayCommand(_ => AcceptRouteCandidate(), _ => CanAcceptRouteCandidate);
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

        /// <summary>Equal-cost route candidates of the current path's ambiguous spans.</summary>
        public ObservableCollection<TrainPathRouteCandidateItemViewModel> RouteCandidates { get; } = new ObservableCollection<TrainPathRouteCandidateItemViewModel>();

        public RelayCommand UndoCommand { get; }

        public RelayCommand RedoCommand { get; }

        public RelayCommand SnapToTrackCommand { get; }

        public RelayCommand MoveSelectedNodeCommand { get; }

        public RelayCommand CommitMoveNodeCommand { get; }

        public RelayCommand CancelMoveNodeCommand { get; }

        public RelayCommand RepairSelectedNodeCommand { get; }

        public RelayCommand ToggleReversalPointCommand { get; }

        public RelayCommand AddViaPointCommand { get; }

        public RelayCommand RemoveViaPointCommand { get; }

        public RelayCommand NewPathCommand { get; }

        public RelayCommand SavePathCommand { get; }

        public RelayCommand ValidateAllPathsCommand { get; }

        public RelayCommand AcceptRouteCandidateCommand { get; }

        public bool CanAcceptRouteCandidate => SelectedRouteCandidate != null && !CanCancelMoveNode;

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

        public bool CanMoveSelectedNode => SelectedNode != null && !CanCancelMoveNode;

        public bool CanRepairSelectedNode => SelectedNode != null && !CanCancelMoveNode;

        public bool CanAnnotateSelectedNode => SelectedNode != null && !CanCancelMoveNode;

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
                    SavePathCommand.RaiseCanExecuteChanged();
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
            SyncRouteCandidates(snapshot.RouteCandidates);
            CanUndo = snapshot.CanUndo;
            CanRedo = snapshot.CanRedo;
            CanSnapToTrack = snapshot.CanSnapToTrack;
            bool wasMovingNode = CanCancelMoveNode;
            CanCancelMoveNode = snapshot.CanCancelMoveNode;
            CanCommitMoveNode = snapshot.CanCommitMoveNode;
            CanCreatePath = toolWindow.CanCreatePath;
            CanSavePath = toolWindow.CanSavePath;

            if (wasMovingNode && !CanCancelMoveNode && IsMoveGuidanceMessage(StatusMessage))
                SetStatusMessage(string.Empty, false);

            if (!string.Equals(snapshotSelectedPathId, snapshot.SelectedPathId, StringComparison.Ordinal))
            {
                snapshotSelectedPathId = snapshot.SelectedPathId;
                UpdateSelectedPathFromSnapshot();
            }
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
            SetStatusMessage($"Select a track location for the new via point after node {SelectedNode.Index}.", false);
        }

        private void RemoveViaPoint()
        {
            if (SelectedNode == null)
                return;

            toolWindow.RemoveViaPoint(SelectedNode.Index);
            SetStatusMessage($"Remove via point {SelectedNode.Index} requested.", false);
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
