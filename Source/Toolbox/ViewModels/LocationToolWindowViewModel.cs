using System;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted location dockable tool window.
    /// </summary>
    internal sealed class LocationToolWindowViewModel : PollingToolWindowViewModel
    {
        private readonly LocationToolWindow toolWindow;

        public LocationToolWindowViewModel(LocationToolWindow toolWindow, ToolWindowRefreshScheduler scheduler)
            : base(scheduler, ToolWindowRefreshScheduler.BaseInterval)
        {
            ArgumentNullException.ThrowIfNull(toolWindow);

            this.toolWindow = toolWindow;
        }

        public string Title => toolWindow.Title;

        public ObservableCollection<DebugToolWindowRowViewModel> Rows { get; } = new ObservableCollection<DebugToolWindowRowViewModel>();

        protected override void OnStarted() => toolWindow.Active = true;

        protected override void OnStopped() => toolWindow.Active = false;

        protected override void Refresh()
        {
            ToolWindowSnapshot snapshot = toolWindow.CaptureSnapshot();
            DebugToolWindowRowViewModel.Sync(Rows, snapshot.Rows);
        }
    }
}
