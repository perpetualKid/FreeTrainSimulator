using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox;

namespace FreeTrainSimulator.Toolbox.Wpf.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted train-path dockable tool window. Pulls an immutable
    /// <see cref="TrainPathSnapshot"/> from the <see cref="TrainPathToolWindow"/> bridge on a dispatcher timer
    /// and exposes three views (available paths, the selected path's nodes, and its metadata). Path selection
    /// and node highlight are forwarded back to the bridge (which marshals them onto the game thread).
    /// </summary>
    internal sealed class TrainPathToolWindowViewModel : ObservableObject, IDisposable
    {
        private readonly TrainPathToolWindow toolWindow;
        private readonly DispatcherTimer refreshTimer;
        private string searchText = string.Empty;
        private string statusMessage = string.Empty;
        private TrainPathListItemViewModel selectedPath;
        private TrainPathNodeItemViewModel selectedNode;
        private string snapshotSelectedPathId;
        private bool suppressSelectionCommand;
        private bool disposed;

        public TrainPathToolWindowViewModel(TrainPathToolWindow toolWindow, Dispatcher dispatcher)
        {
            ArgumentNullException.ThrowIfNull(toolWindow);
            ArgumentNullException.ThrowIfNull(dispatcher);

            this.toolWindow = toolWindow;

            refreshTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(250),
            };
            refreshTimer.Tick += RefreshTimer_Tick;
        }

        public string Title => toolWindow.Title;

        public ObservableCollection<TrainPathListItemViewModel> Paths { get; } = new ObservableCollection<TrainPathListItemViewModel>();

        public ObservableCollection<TrainPathNodeItemViewModel> Nodes { get; } = new ObservableCollection<TrainPathNodeItemViewModel>();

        public ObservableCollection<DebugToolWindowRowViewModel> Metadata { get; } = new ObservableCollection<DebugToolWindowRowViewModel>();

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
            }
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(disposed, nameof(TrainPathToolWindowViewModel));

            toolWindow.Active = true;
            refreshTimer.Start();
            Refresh();
        }

        public void Stop()
        {
            refreshTimer.Stop();
            toolWindow.Active = false;
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            Refresh();
        }

        private void Refresh()
        {
            TrainPathSnapshot snapshot = toolWindow.CaptureTrainPathSnapshot();

            SyncPaths(snapshot.Paths);
            SyncNodes(snapshot.Nodes);
            DebugToolWindowRowViewModel.Sync(Metadata, snapshot.Metadata);

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
                    Nodes[i].Update(row.Index, row.NodeType, row.Valid);
                else
                    Nodes.Add(new TrainPathNodeItemViewModel(row.Index, row.NodeType, row.Valid));
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
            }
            finally
            {
                suppressSelectionCommand = false;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Stop();
            refreshTimer.Tick -= RefreshTimer_Tick;
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

        public TrainPathNodeItemViewModel(int index, string nodeType, bool valid)
        {
            this.index = index;
            this.nodeType = nodeType;
            this.valid = valid;
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

        public void Update(int index, string nodeType, bool valid)
        {
            Index = index;
            NodeType = nodeType;
            Valid = valid;
        }
    }
}
