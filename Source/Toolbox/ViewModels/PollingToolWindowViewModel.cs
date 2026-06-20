using System;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Base class for the hosted, pull-model tool-window view models. Encapsulates the lifecycle every
    /// snapshot-based tool window shares: registering a <see cref="Refresh"/> callback with the shared
    /// <see cref="ToolWindowRefreshScheduler"/> while the pane is shown, pulling an immediate snapshot on show,
    /// and unregistering on hide/dispose. Derived view models implement only <see cref="Refresh"/> (the snapshot
    /// pull) and may override <see cref="OnStarted"/>/<see cref="OnStopped"/> to toggle their bridge's active
    /// flag or run other show/hide logic.
    /// </summary>
    internal abstract class PollingToolWindowViewModel : ObservableObject, IDisposable
    {
        private readonly ToolWindowRefreshScheduler scheduler;
        private readonly TimeSpan interval;
        private readonly Action refreshCallback;
        private bool started;
        private bool disposed;

        protected PollingToolWindowViewModel(ToolWindowRefreshScheduler scheduler, TimeSpan interval)
        {
            ArgumentNullException.ThrowIfNull(scheduler);

            this.scheduler = scheduler;
            this.interval = interval;
            refreshCallback = Refresh;
        }

        /// <summary>Shows the tool window: registers with the shared timer (once) and pulls the first snapshot.</summary>
        public void Start()
        {
            ObjectDisposedException.ThrowIf(disposed, GetType().Name);

            if (!started)
            {
                started = true;
                OnStarted();
                scheduler.Register(refreshCallback, interval);
            }

            Refresh();
        }

        /// <summary>Hides the tool window: unregisters from the shared timer so it stops being refreshed.</summary>
        public void Stop()
        {
            if (!started)
                return;

            started = false;
            scheduler.Unregister(refreshCallback);
            OnStopped();
        }

        /// <summary>Pulls the latest snapshot from the bridge and syncs the bound state. Runs on the dispatcher thread.</summary>
        protected abstract void Refresh();

        /// <summary>Invoked once when the pane is shown, before the first refresh. Default does nothing.</summary>
        protected virtual void OnStarted()
        {
        }

        /// <summary>Invoked once when the pane is hidden, after unregistering. Default does nothing.</summary>
        protected virtual void OnStopped()
        {
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Stop();
        }
    }
}
