using System;

using FreeTrainSimulator.Toolbox;

namespace FreeTrainSimulator.Toolbox.Wpf.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted settings dockable tool window. Unlike the read-only snapshot tool
    /// windows this is interactive/two-way. The bound properties use optimistic local backing fields so the
    /// checkbox reflects the user's choice immediately; the actual write to the hosted
    /// <see cref="SettingsToolWindow"/> bridge is marshaled to the game thread and applied asynchronously.
    /// Reading the bridge synchronously right after a write would return the stale (not-yet-applied) value and
    /// make WPF revert the checkbox, which is why a local field is used instead of reading the bridge live.
    /// </summary>
    internal sealed class SettingsToolWindowViewModel : ObservableObject, IDisposable
    {
        private readonly SettingsToolWindow toolWindow;
        private bool enableLogging;
        private bool restoreLastView;
        private bool fontOutline;
        private bool realTrackWidth;
        private bool disposed;

        public SettingsToolWindowViewModel(SettingsToolWindow toolWindow)
        {
            ArgumentNullException.ThrowIfNull(toolWindow);
            this.toolWindow = toolWindow;

            enableLogging = toolWindow.EnableLogging;
            restoreLastView = toolWindow.RestoreLastView;
            fontOutline = toolWindow.FontOutline;
            realTrackWidth = toolWindow.RealTrackWidth;
        }

        public string Title => toolWindow.Title;

        public bool EnableLogging
        {
            get => enableLogging;
            set
            {
                if (SetProperty(ref enableLogging, value))
                    toolWindow.SetEnableLogging(value);
            }
        }

        public bool RestoreLastView
        {
            get => restoreLastView;
            set
            {
                if (SetProperty(ref restoreLastView, value))
                    toolWindow.SetRestoreLastView(value);
            }
        }

        public bool FontOutline
        {
            get => fontOutline;
            set
            {
                if (SetProperty(ref fontOutline, value))
                    toolWindow.SetFontOutline(value);
            }
        }

        public bool RealTrackWidth
        {
            get => realTrackWidth;
            set
            {
                if (SetProperty(ref realTrackWidth, value))
                    toolWindow.SetRealTrackWidth(value);
            }
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(disposed, nameof(SettingsToolWindowViewModel));

            // Re-sync the local fields from the bridge in case settings changed elsewhere while the pane was
            // hidden, so the checkboxes reflect the live state when shown. SetProperty only raises a change
            // notification when the value actually differs.
            SetProperty(ref enableLogging, toolWindow.EnableLogging, nameof(EnableLogging));
            SetProperty(ref restoreLastView, toolWindow.RestoreLastView, nameof(RestoreLastView));
            SetProperty(ref fontOutline, toolWindow.FontOutline, nameof(FontOutline));
            SetProperty(ref realTrackWidth, toolWindow.RealTrackWidth, nameof(RealTrackWidth));
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
            disposed = true;
        }
    }
}