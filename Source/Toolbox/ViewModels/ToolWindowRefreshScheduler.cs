using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Shared refresh pump for the hosted pull-model tool-window view models. Owns the single
    /// <see cref="DispatcherTimer"/> for the whole shell and drives every registered view model from one
    /// coalesced tick, instead of each view model running its own timer. The timer ticks at
    /// <see cref="BaseInterval"/> and each target is invoked at its own cadence (rounded to a whole multiple of
    /// the base tick). The timer only runs while at least one target is registered, so a shell with no visible
    /// tool windows costs nothing on the dispatcher.
    /// <para>
    /// All members are expected to be used on the dispatcher thread the scheduler was created with, so no
    /// locking is required.
    /// </para>
    /// </summary>
    internal sealed class ToolWindowRefreshScheduler : IDisposable
    {
        /// <summary>Base cadence of the shared timer; every target interval is rounded to a multiple of this.</summary>
        public static readonly TimeSpan BaseInterval = TimeSpan.FromMilliseconds(50);

        private readonly DispatcherTimer timer;
        private readonly List<Target> targets = new List<Target>();
        private bool disposed;

        public ToolWindowRefreshScheduler(Dispatcher dispatcher)
        {
            ArgumentNullException.ThrowIfNull(dispatcher);

            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = BaseInterval,
            };
            timer.Tick += Timer_Tick;
        }

        /// <summary>
        /// Registers <paramref name="onRefresh"/> to be invoked every <paramref name="interval"/> on the shared
        /// timer. The timer starts on the first registration. Registering an already-registered callback is a
        /// no-op.
        /// </summary>
        public void Register(Action onRefresh, TimeSpan interval)
        {
            ArgumentNullException.ThrowIfNull(onRefresh);
            ObjectDisposedException.ThrowIf(disposed, nameof(ToolWindowRefreshScheduler));

            if (FindIndex(onRefresh) >= 0)
                return;

            targets.Add(new Target(onRefresh, ToCadenceTicks(interval)));

            if (!timer.IsEnabled)
                timer.Start();
        }

        /// <summary>
        /// Removes a previously registered callback. The timer stops once the last target is removed.
        /// </summary>
        public void Unregister(Action onRefresh)
        {
            ArgumentNullException.ThrowIfNull(onRefresh);

            int index = FindIndex(onRefresh);
            if (index < 0)
                return;

            targets.RemoveAt(index);

            if (targets.Count == 0)
                timer.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Iterate by index so a target that unregisters itself during its own refresh does not break the loop.
            for (int i = 0; i < targets.Count; i++)
            {
                Target target = targets[i];
                if (++target.Counter < target.CadenceTicks)
                    continue;

                target.Counter = 0;
                target.OnRefresh();
            }
        }

        private int FindIndex(Action onRefresh)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].OnRefresh.Equals(onRefresh))
                    return i;
            }

            return -1;
        }

        private static int ToCadenceTicks(TimeSpan interval)
        {
            int ticks = (int)Math.Round(interval.TotalMilliseconds / BaseInterval.TotalMilliseconds);
            return ticks < 1 ? 1 : ticks;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            timer.Stop();
            timer.Tick -= Timer_Tick;
            targets.Clear();
        }

        // Single registered refresh callback plus the per-target tick bookkeeping used to throttle it down to
        // the target's cadence.
        private sealed class Target
        {
            public Target(Action onRefresh, int cadenceTicks)
            {
                OnRefresh = onRefresh;
                CadenceTicks = cadenceTicks;
            }

            public Action OnRefresh { get; }

            public int CadenceTicks { get; }

            public int Counter { get; set; }
        }
    }
}
