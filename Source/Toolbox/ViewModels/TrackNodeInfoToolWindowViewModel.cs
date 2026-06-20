using System;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted track node information dockable tool window. Uses the same
    /// pull model as <see cref="DebugToolWindowViewModel"/> for the read-only info rows, and exposes a
    /// search box plus command that navigates and highlights a track node by index. The
    /// <see cref="SearchRoads"/> flag selects between rail and road nodes.
    /// </summary>
    internal sealed class TrackNodeInfoToolWindowViewModel : PollingToolWindowViewModel
    {
        private readonly TrackNodeInfoToolWindow toolWindow;
        private string searchText = string.Empty;
        private bool searchRoads;

        public TrackNodeInfoToolWindowViewModel(TrackNodeInfoToolWindow toolWindow, ToolWindowRefreshScheduler scheduler)
            : base(scheduler, TimeSpan.FromMilliseconds(250))
        {
            ArgumentNullException.ThrowIfNull(toolWindow);

            this.toolWindow = toolWindow;
            SearchCommand = new RelayCommand(_ => Search());
        }

        public string Title => toolWindow.Title;

        public ObservableCollection<DebugToolWindowRowViewModel> Rows { get; } = new ObservableCollection<DebugToolWindowRowViewModel>();

        public RelayCommand SearchCommand { get; }

        public string SearchText
        {
            get => searchText;
            set => SetProperty(ref searchText, value);
        }

        /// <summary>When true the search targets road nodes; otherwise rail nodes.</summary>
        public bool SearchRoads
        {
            get => searchRoads;
            set => SetProperty(ref searchRoads, value);
        }

        protected override void OnStarted() => toolWindow.Active = true;

        protected override void OnStopped() => toolWindow.Active = false;

        private void Search()
        {
            toolWindow.SearchByIndex(SearchText, SearchRoads);
        }

        protected override void Refresh()
        {
            ToolWindowSnapshot snapshot = toolWindow.CaptureSnapshot();
            DebugToolWindowRowViewModel.Sync(Rows, snapshot.Rows);
        }
    }
}
