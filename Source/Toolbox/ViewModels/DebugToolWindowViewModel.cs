using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Drawing;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted debug dockable tool window. Uses a pull model driven by the shared
    /// <see cref="ToolWindowRefreshScheduler"/>: it periodically captures immutable snapshots from
    /// <see cref="DebugToolWindow"/> and updates the bound rows on the WPF UI thread.
    /// </summary>
    internal sealed class DebugToolWindowViewModel : PollingToolWindowViewModel
    {
        private readonly DebugToolWindow toolWindow;

        public DebugToolWindowViewModel(DebugToolWindow toolWindow, ToolWindowRefreshScheduler scheduler)
            : base(scheduler, TimeSpan.FromMilliseconds(250))
        {
            ArgumentNullException.ThrowIfNull(toolWindow);

            this.toolWindow = toolWindow;
            Title = toolWindow.Title;
        }

        public string Title { get; }

        public ObservableCollection<DebugToolWindowRowViewModel> Rows { get; } = new ObservableCollection<DebugToolWindowRowViewModel>();

        protected override void OnStarted() => toolWindow.Active = true;

        protected override void OnStopped() => toolWindow.Active = false;

        protected override void Refresh()
        {
            ToolWindowSnapshot snapshot = toolWindow.CaptureSnapshot();
            DebugToolWindowRowViewModel.Sync(Rows, snapshot.Rows);
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
