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
        private TrainPathListItemViewModel selectedPath;
        private TrainPathNodeItemViewModel selectedNode;
        private string snapshotSelectedPathId;
        private bool canUndo;
        private bool canRedo;
        private bool suppressSelectionCommand;

        public TrainPathToolWindowViewModel(TrainPathToolWindow toolWindow, ToolWindowRefreshScheduler scheduler)
            : base(scheduler, ToolWindowRefreshScheduler.BaseInterval)
        {
            ArgumentNullException.ThrowIfNull(toolWindow);

            this.toolWindow = toolWindow;
            UndoCommand = new RelayCommand(_ => toolWindow.Undo(), _ => CanUndo);
            RedoCommand = new RelayCommand(_ => toolWindow.Redo(), _ => CanRedo);
        }

        public string Title => toolWindow.Title;

        public ObservableCollection<TrainPathListItemViewModel> Paths { get; } = new ObservableCollection<TrainPathListItemViewModel>();

        public ObservableCollection<TrainPathNodeItemViewModel> Nodes { get; } = new ObservableCollection<TrainPathNodeItemViewModel>();

        public ObservableCollection<DebugToolWindowRowViewModel> SelectedNodeDetailRows { get; } = new ObservableCollection<DebugToolWindowRowViewModel>();

        public ObservableCollection<DebugToolWindowRowViewModel> Metadata { get; } = new ObservableCollection<DebugToolWindowRowViewModel>();

        public RelayCommand UndoCommand { get; }

        public RelayCommand RedoCommand { get; }

        public bool CanUndo
        {
            get => canUndo;
            private set
            {
                if (SetProperty(ref canUndo, value))
                    UndoCommand.RaiseCanExecuteChanged();
            }
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

        public TrainPathListItemViewModel SelectedPath
        {
            get => selectedPath;
            set
            {
                if (!SetProperty(ref selectedPath, value))
                    return;

                if (suppressSelectionCommand)
                    return;

                StatusMessage = string.Empty;
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
                UpdateSelectedNodeDetailRows();
            }
        }

        protected override void OnStarted() => toolWindow.Active = true;

        protected override void OnStopped() => toolWindow.Active = false;

        protected override void Refresh()
        {
            TrainPathSnapshot snapshot = toolWindow.CaptureTrainPathSnapshot();

            SyncPaths(snapshot.Paths);
            SyncNodes(snapshot.Nodes);
            UpdateSelectedNodeDetailRows();
            DebugToolWindowRowViewModel.Sync(Metadata, snapshot.Metadata);
            CanUndo = snapshot.CanUndo;
            CanRedo = snapshot.CanRedo;

            if (!string.Equals(snapshotSelectedPathId, snapshot.SelectedPathId, StringComparison.Ordinal))
            {
                snapshotSelectedPathId = snapshot.SelectedPathId;
                UpdateSelectedPathFromSnapshot();
            }
        }

        private void SyncPaths(ImmutableArray<TrainPathListRow> rows)
        {
            // Preserve the bound selection across the in-place sync.
            string selectedId = SelectedPath?.Id;

            for (int i = 0; i < rows.Length; i++)
            {
                TrainPathListRow row = rows[i];
                if (i < Paths.Count)
                    Paths[i].Update(row.Id, row.Name);
                else
                    Paths.Add(new TrainPathListItemViewModel(row.Id, row.Name));
            }

            for (int i = Paths.Count - 1; i >= rows.Length; i--)
                Paths.RemoveAt(i);

            ApplyPathFilter();

            if (selectedId != null && (SelectedPath == null || !string.Equals(SelectedPath.Id, selectedId, StringComparison.Ordinal)))
                RestorePathSelection(selectedId);
        }

        private void SyncNodes(ImmutableArray<TrainPathNodeRow> rows)
        {
            int selectedIndex = SelectedNode?.Index ?? -1;

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

            if (selectedIndex >= 0 && (SelectedNode == null || SelectedNode.Index != selectedIndex))
                RestoreNodeSelection(selectedIndex);
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
                UpdateSelectedNodeDetailRows();
            }
            finally
            {
                suppressSelectionCommand = false;
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
            builder.Add(new ToolWindowRow("Index", selectedNode.Index.ToString(System.Globalization.CultureInfo.InvariantCulture), null, false));
            builder.Add(new ToolWindowRow("Type", selectedNode.NodeType, null, false));
            builder.Add(new ToolWindowRow("Track Node", selectedNode.TrackNodeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), null, false));
            builder.Add(new ToolWindowRow("Wait", selectedNode.WaitTime?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, null, false));
            builder.Add(new ToolWindowRow("Next Main", selectedNode.NextMainNode.ToString(System.Globalization.CultureInfo.InvariantCulture), null, false));
            builder.Add(new ToolWindowRow("Next Siding", selectedNode.NextSidingNode.ToString(System.Globalization.CultureInfo.InvariantCulture), null, false));
            builder.Add(new ToolWindowRow("Validation", selectedNode.Validation ?? string.Empty, null, !selectedNode.Valid));
            builder.Add(new ToolWindowRow("Nearest Track Node", selectedNode.NearestTrackNodeIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, null, false));
            builder.Add(new ToolWindowRow("Nearest Track Section", selectedNode.NearestTrackSectionIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, null, false));
            builder.Add(new ToolWindowRow("Nearest Track Distance", FormatMeters(selectedNode.NearestTrackDistanceMeters), null, false));
            return builder.ToImmutable();
        }

        private static string FormatMeters(double? value)
        {
            return value.HasValue ? FormattableString.Invariant($"{value.Value:0.###} m") : string.Empty;
        }

        }

        /// <summary>Bindable row for the available-paths list. Observable so it can be updated in place.</summary>
    internal sealed class TrainPathListItemViewModel : ObservableObject
    {
        private string id;
        private string name;
        private bool isVisible = true;

        public TrainPathListItemViewModel(string id, string name)
        {
            this.id = id;
            this.name = name;
        }

        public string Id
        {
            get => id;
            private set => SetProperty(ref id, value);
        }

        public string Name
        {
            get => name;
            private set => SetProperty(ref name, value);
        }

        public bool IsVisible
        {
            get => isVisible;
            set => SetProperty(ref isVisible, value);
        }

        public void Update(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    /// <summary>Bindable row for the path-node list. Observable so it can be updated in place.</summary>
    internal sealed class TrainPathNodeItemViewModel : ObservableObject
    {
        private int index;
        private string nodeType;
        private bool valid;
        private int trackNodeIndex;
        private int nextMainNode;
        private int nextSidingNode;
        private int? waitTime;
        private string validation;
        private int? nearestTrackNodeIndex;
        private int? nearestTrackSectionIndex;
        private double? nearestTrackDistanceMeters;

        public TrainPathNodeItemViewModel(int index, string nodeType, bool valid)
        {
            this.index = index;
            this.nodeType = nodeType;
            this.valid = valid;
            nextMainNode = -1;
            nextSidingNode = -1;
        }

        public TrainPathNodeItemViewModel(TrainPathNodeRow row)
            : this(row.Index, row.NodeType, row.Valid)
        {
            Update(row);
        }

        public int Index
        {
            get => index;
            private set => SetProperty(ref index, value);
        }

        public string NodeType
        {
            get => nodeType;
            private set => SetProperty(ref nodeType, value);
        }

        public bool Valid
        {
            get => valid;
            private set => SetProperty(ref valid, value);
        }

        public int TrackNodeIndex
        {
            get => trackNodeIndex;
            private set => SetProperty(ref trackNodeIndex, value);
        }

        public int NextMainNode
        {
            get => nextMainNode;
            private set => SetProperty(ref nextMainNode, value);
        }

        public int NextSidingNode
        {
            get => nextSidingNode;
            private set => SetProperty(ref nextSidingNode, value);
        }

        public int? WaitTime
        {
            get => waitTime;
            private set => SetProperty(ref waitTime, value);
        }

        public string Validation
        {
            get => validation;
            private set => SetProperty(ref validation, value);
        }

        public int? NearestTrackNodeIndex
        {
            get => nearestTrackNodeIndex;
            private set => SetProperty(ref nearestTrackNodeIndex, value);
        }

        public int? NearestTrackSectionIndex
        {
            get => nearestTrackSectionIndex;
            private set => SetProperty(ref nearestTrackSectionIndex, value);
        }

        public double? NearestTrackDistanceMeters
        {
            get => nearestTrackDistanceMeters;
            private set => SetProperty(ref nearestTrackDistanceMeters, value);
        }

        public void Update(int index, string nodeType, bool valid)
        {
            Index = index;
            NodeType = nodeType;
            Valid = valid;
        }

        public void Update(TrainPathNodeRow row)
        {
            Update(row.Index, row.NodeType, row.Valid);
            TrackNodeIndex = row.TrackNodeIndex;
            NextMainNode = row.NextMainNode;
            NextSidingNode = row.NextSidingNode;
            WaitTime = row.WaitTime;
            Validation = row.Validation;
            NearestTrackNodeIndex = row.NearestTrackNodeIndex;
            NearestTrackSectionIndex = row.NearestTrackSectionIndex;
            NearestTrackDistanceMeters = row.NearestTrackDistanceMeters;
        }
    }
}
