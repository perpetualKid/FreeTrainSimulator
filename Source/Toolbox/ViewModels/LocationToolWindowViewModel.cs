using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted location dockable tool window.
    /// </summary>
    internal sealed class LocationToolWindowViewModel : ObservableObject, IDisposable
    {
        private readonly LocationToolWindow toolWindow;
        private readonly DispatcherTimer refreshTimer;
        private bool disposed;

        public LocationToolWindowViewModel(LocationToolWindow toolWindow, Dispatcher dispatcher)
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

        public ObservableCollection<DebugToolWindowRowViewModel> Rows { get; } = new ObservableCollection<DebugToolWindowRowViewModel>();

        public void Start()
        {
            ObjectDisposedException.ThrowIf(disposed, nameof(DebugToolWindowViewModel));

            toolWindow.Active = true;
            refreshTimer.Start();
            RefreshRows();
        }

        public void Stop()
        {
            refreshTimer.Stop();
            toolWindow.Active = false;
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
