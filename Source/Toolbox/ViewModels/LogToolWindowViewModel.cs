using System;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted log dockable tool window. Exposes the log content as a single
    /// string for rendering in a read-only text box.
    /// </summary>
    internal sealed class LogToolWindowViewModel : PollingToolWindowViewModel
    {
        private readonly LogToolWindow toolWindow;
        private string logText = string.Empty;

        public LogToolWindowViewModel(LogToolWindow toolWindow, ToolWindowRefreshScheduler scheduler)
            : base(scheduler, TimeSpan.FromMilliseconds(500))
        {
            ArgumentNullException.ThrowIfNull(toolWindow);

            this.toolWindow = toolWindow;
        }

        public string Title => toolWindow.Title;

        public string LogText
        {
            get => logText;
            private set => SetProperty(ref logText, value);
        }

        protected override void OnStarted() => toolWindow.Active = true;

        protected override void OnStopped() => toolWindow.Active = false;

        protected override void Refresh()
        {
            ToolWindowSnapshot snapshot = toolWindow.CaptureSnapshot();
            LogText = snapshot.Rows.IsDefaultOrEmpty ? string.Empty : snapshot.Rows[0].Value;
        }
    }
}
