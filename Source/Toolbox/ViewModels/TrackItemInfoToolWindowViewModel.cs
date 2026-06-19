using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted track item information dockable tool window. Uses the same
    /// pull model as <see cref="DebugToolWindowViewModel"/> for the read-only info rows, and exposes a
    /// search box plus command that navigates the map to a track item by index.
    /// </summary>
    internal sealed class TrackItemInfoToolWindowViewModel : ObservableObject, IDisposable
    {
        private readonly TrackItemInfoToolWindow toolWindow;
        private readonly DispatcherTimer refreshTimer;
        private string searchText = string.Empty;
        private bool disposed;

        public TrackItemInfoToolWindowViewModel(TrackItemInfoToolWindow toolWindow, Dispatcher dispatcher)
        {
            ArgumentNullException.ThrowIfNull(toolWindow);
            ArgumentNullException.ThrowIfNull(dispatcher);

            this.toolWindow = toolWindow;
            SearchCommand = new RelayCommand(_ => Search());

            refreshTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(250),
            };
            refreshTimer.Tick += RefreshTimer_Tick;
        }

        public string Title => toolWindow.Title;

        public ObservableCollection<DebugToolWindowRowViewModel> Rows { get; } = new ObservableCollection<DebugToolWindowRowViewModel>();

        public RelayCommand SearchCommand { get; }

        public string SearchText
        {
            get => searchText;
            set => SetProperty(ref searchText, value);
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(disposed, nameof(TrackItemInfoToolWindowViewModel));

            toolWindow.Active = true;
            refreshTimer.Start();
            RefreshRows();
        }

        public void Stop()
        {
            refreshTimer.Stop();
            toolWindow.Active = false;
        }

        private void Search()
        {
            toolWindow.SearchByIndex(SearchText);
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshRows();
        }

        private void RefreshRows()
        {
            ToolWindowSnapshot snapshot = toolWindow.CaptureSnapshot();
            DebugToolWindowRowViewModel.Sync(Rows, snapshot.Rows);
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
}
