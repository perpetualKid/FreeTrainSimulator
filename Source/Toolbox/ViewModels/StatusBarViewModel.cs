using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Bindable view model for the main-window status bar. Uses the same pull model as
    /// <see cref="DebugToolWindowViewModel"/>: the shared <see cref="ToolWindowRefreshScheduler"/> periodically
    /// captures an immutable <see cref="StatusBarSnapshot"/> from the hosted <see cref="StatusBarToolWindow"/>
    /// bridge and syncs the bound field collection on the WPF UI thread. The fields are rendered generically, so
    /// new status-bar content added on the game side flows through without changes here.
    /// </summary>
    internal sealed class StatusBarViewModel : PollingToolWindowViewModel
    {
        private readonly StatusBarToolWindow toolWindow;

        public StatusBarViewModel(StatusBarToolWindow toolWindow, ToolWindowRefreshScheduler scheduler)
            : base(scheduler, TimeSpan.FromMilliseconds(100))
        {
            ArgumentNullException.ThrowIfNull(toolWindow);

            this.toolWindow = toolWindow;
        }

        public ObservableCollection<StatusBarFieldViewModel> Fields { get; } = new ObservableCollection<StatusBarFieldViewModel>();

        protected override void Refresh()
        {
            StatusBarSnapshot snapshot = toolWindow.CaptureSnapshot();
            StatusBarFieldViewModel.Sync(Fields, snapshot.Fields);
        }
    }

    /// <summary>
    /// Single field rendered in the status bar. <see cref="Label"/> is an optional caption shown before the
    /// value (hidden when null/empty via the bound converter); <see cref="Value"/> is the display text.
    /// </summary>
    internal sealed class StatusBarFieldViewModel : ObservableObject
    {
        private string label;
        private string value;

        public StatusBarFieldViewModel(string key, string label, string value)
        {
            Key = key;
            this.label = label;
            this.value = value;
        }

        /// <summary>Stable identifier of the field, kept for diagnostics/extensibility.</summary>
        public string Key { get; }

        public string Label
        {
            get => label;
            private set => SetProperty(ref label, value);
        }

        public string Value
        {
            get => value;
            private set => SetProperty(ref this.value, value);
        }

        public void Update(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public static void Sync(ObservableCollection<StatusBarFieldViewModel> target, ImmutableArray<StatusBarField> fields)
        {
            ArgumentNullException.ThrowIfNull(target);

            for (int i = 0; i < fields.Length; i++)
            {
                StatusBarField field = fields[i];
                if (i < target.Count && string.Equals(target[i].Key, field.Key, StringComparison.Ordinal))
                {
                    target[i].Update(field.Label, field.Value);
                }
                else if (i < target.Count)
                {
                    target[i] = new StatusBarFieldViewModel(field.Key, field.Label, field.Value);
                }
                else
                {
                    target.Add(new StatusBarFieldViewModel(field.Key, field.Label, field.Value));
                }
            }

            for (int i = target.Count - 1; i >= fields.Length; i--)
                target.RemoveAt(i);
        }
    }
}
