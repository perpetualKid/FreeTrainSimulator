using System;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.Wpf.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted log dockable tool window. Exposes the log content as a single
    /// string for rendering in a read-only text box.
    /// </summary>
    internal sealed class LogToolWindowViewModel : ObservableObject, IDisposable
    {
        private readonly LogToolWindow toolWindow;
        private readonly DispatcherTimer refreshTimer;
        private string logText = string.Empty;
        private bool disposed;

        public LogToolWindowViewModel(LogToolWindow toolWindow, Dispatcher dispatcher)
        {
            ArgumentNullException.ThrowIfNull(toolWindow);
            ArgumentNullException.ThrowIfNull(dispatcher);

            this.toolWindow = toolWindow;

            refreshTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(500),
            };
            refreshTimer.Tick += RefreshTimer_Tick;
        }

        public string Title => toolWindow.Title;

        public string LogText
        {
            get => logText;
            private set => SetProperty(ref logText, value);
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(disposed, nameof(LogToolWindowViewModel));

            toolWindow.Active = true;
            refreshTimer.Start();
            RefreshText();
        }

        public void Stop()
        {
            refreshTimer.Stop();
            toolWindow.Active = false;
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshText();
        }

        private void RefreshText()
        {
            ToolWindowSnapshot snapshot = toolWindow.CaptureSnapshot();
            LogText = snapshot.Rows.IsDefaultOrEmpty ? string.Empty : snapshot.Rows[0].Value;
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
