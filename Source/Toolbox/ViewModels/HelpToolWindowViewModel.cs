using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.Wpf.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted help dockable tool window. Exposes command/key rows and
    /// command/key text filtering.
    /// </summary>
    internal sealed class HelpToolWindowViewModel : ObservableObject, IDisposable
    {
        private readonly HelpToolWindow toolWindow;
        private readonly DispatcherTimer refreshTimer;
        private string searchText = string.Empty;
        private bool searchByKey;
        private bool disposed;

        public HelpToolWindowViewModel(HelpToolWindow toolWindow, Dispatcher dispatcher)
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

        public string SearchText
        {
            get => searchText;
            set
            {
                if (!SetProperty(ref searchText, value))
                    return;

                UpdateSearch();
            }
        }

        public bool SearchByKey
        {
            get => searchByKey;
            set
            {
                if (!SetProperty(ref searchByKey, value))
                    return;

                UpdateSearch();
            }
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(disposed, nameof(HelpToolWindowViewModel));

            toolWindow.Active = true;
            refreshTimer.Start();
            UpdateSearch();
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

        private void UpdateSearch()
        {
            HelpToolWindow.HelpSearchColumn searchColumn = SearchByKey
                ? HelpToolWindow.HelpSearchColumn.Key
                : HelpToolWindow.HelpSearchColumn.Command;

            toolWindow.SetSearch(SearchText ?? string.Empty, searchColumn);
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
