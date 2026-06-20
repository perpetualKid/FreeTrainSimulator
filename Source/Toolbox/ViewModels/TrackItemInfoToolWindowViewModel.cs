using System;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted track item information dockable tool window. Uses the same
    /// pull model as <see cref="DebugToolWindowViewModel"/> for the read-only info rows, and exposes a
    /// search box plus command that navigates the map to a track item by index.
    /// </summary>
    internal sealed class TrackItemInfoToolWindowViewModel : PollingToolWindowViewModel
    {
        private readonly TrackItemInfoToolWindow toolWindow;
        private string searchText = string.Empty;

        public TrackItemInfoToolWindowViewModel(TrackItemInfoToolWindow toolWindow, ToolWindowRefreshScheduler scheduler)
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

        protected override void OnStarted() => toolWindow.Active = true;

        protected override void OnStopped() => toolWindow.Active = false;

        private void Search()
        {
            toolWindow.SearchByIndex(SearchText);
        }

        protected override void Refresh()
        {
            ToolWindowSnapshot snapshot = toolWindow.CaptureSnapshot();
            DebugToolWindowRowViewModel.Sync(Rows, snapshot.Rows);
        }
    }
}
