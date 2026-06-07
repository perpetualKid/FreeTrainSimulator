using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Threading;

using FreeTrainSimulator.Toolbox;

namespace FreeTrainSimulator.Toolbox.Wpf.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted debug dockable tool window. Uses a pull model: a dispatcher timer
    /// periodically captures immutable snapshots from <see cref="DebugToolWindow"/> and updates the bound rows
    /// on the WPF UI thread.
    /// </summary>
    internal sealed class DebugToolWindowViewModel : ObservableObject, IDisposable
    {
        private readonly DebugToolWindow toolWindow;
        private readonly DispatcherTimer refreshTimer;
        private bool disposed;

        public DebugToolWindowViewModel(DebugToolWindow toolWindow, Dispatcher dispatcher)
        {
            ArgumentNullException.ThrowIfNull(toolWindow);
            ArgumentNullException.ThrowIfNull(dispatcher);

            this.toolWindow = toolWindow;
            Title = toolWindow.Title;

            refreshTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(250),
            };
            refreshTimer.Tick += RefreshTimer_Tick;
        }

        public string Title { get; }

        public ObservableCollection<DebugToolWindowRowViewModel> Rows { get; } = new ObservableCollection<DebugToolWindowRowViewModel>();

        public void Start()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(DebugToolWindowViewModel));

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

            Rows.Clear();
            foreach (ToolWindowRow row in snapshot.Rows)
                Rows.Add(new DebugToolWindowRowViewModel(row.Name, row.Value, row.Color, row.Bold));
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

    /// <summary>
    /// Single render row for the debug tool-window grid.
    /// </summary>
    internal sealed class DebugToolWindowRowViewModel
    {
        public DebugToolWindowRowViewModel(string name, string value, Color? color, bool bold)
        {
            Name = name;
            Value = value;
            Color = color;
            Bold = bold;
        }

        public string Name { get; }

        public string Value { get; }

        public Color? Color { get; }

        public bool Bold { get; }
    }
}
