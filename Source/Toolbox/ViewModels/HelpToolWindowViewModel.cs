using System;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted help dockable tool window. Exposes command/key rows and
    /// command/key text filtering.
    /// </summary>
    internal sealed class HelpToolWindowViewModel : PollingToolWindowViewModel
    {
        private readonly HelpToolWindow toolWindow;
        private string searchText = string.Empty;
        private bool searchByKey;

        public HelpToolWindowViewModel(HelpToolWindow toolWindow, ToolWindowRefreshScheduler scheduler)
            : base(scheduler, TimeSpan.FromMilliseconds(250))
        {
            ArgumentNullException.ThrowIfNull(toolWindow);

            this.toolWindow = toolWindow;
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

        protected override void OnStarted()
        {
            toolWindow.Active = true;
            UpdateSearch();
        }

        protected override void OnStopped() => toolWindow.Active = false;

        private void UpdateSearch()
        {
            HelpToolWindow.HelpSearchColumn searchColumn = SearchByKey
                ? HelpToolWindow.HelpSearchColumn.Key
                : HelpToolWindow.HelpSearchColumn.Command;

            toolWindow.SetSearch(SearchText ?? string.Empty, searchColumn);
        }

        protected override void Refresh()
        {
            ToolWindowSnapshot snapshot = toolWindow.CaptureSnapshot();
            DebugToolWindowRowViewModel.Sync(Rows, snapshot.Rows);
        }
    }
}
