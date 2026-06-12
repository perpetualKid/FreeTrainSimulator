using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Threading;

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

    /// <summary>
    /// Single render row for the debug tool-window grid.
    /// </summary>
    internal sealed class DebugToolWindowRowViewModel : ObservableObject
    {
        private string name;
        private string value;
        private Color? color;
        private bool bold;

        public DebugToolWindowRowViewModel(string name, string value, Color? color, bool bold)
        {
            this.name = name;
            this.value = value;
            this.color = color;
            this.bold = bold;
        }

        public string Name
        {
            get => name;
            private set => SetProperty(ref name, value);
        }

        public string Value
        {
            get => value;
            private set => SetProperty(ref this.value, value);
        }

        public Color? Color
        {
            get => color;
            private set => SetProperty(ref color, value);
        }

        public bool Bold
        {
            get => bold;
            private set => SetProperty(ref bold, value);
        }

        public void Update(string name, string value, Color? color, bool bold)
        {
            Name = name;
            Value = value;
            Color = color;
            Bold = bold;
        }

        public static void Sync(ObservableCollection<DebugToolWindowRowViewModel> target, ImmutableArray<ToolWindowRow> rows)
        {
            ArgumentNullException.ThrowIfNull(target);

            for (int i = 0; i < rows.Length; i++)
            {
                ToolWindowRow row = rows[i];
                if (i < target.Count)
                    target[i].Update(row.Name, row.Value, row.Color, row.Bold);
                else
                    target.Add(new DebugToolWindowRowViewModel(row.Name, row.Value, row.Color, row.Bold));
            }

            for (int i = target.Count - 1; i >= rows.Length; i--)
                target.RemoveAt(i);
        }
    }
}
