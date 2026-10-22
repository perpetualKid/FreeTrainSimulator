using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime;

namespace FreeTrainSimulator.Toolbox.Hosting
{
    /// <summary>
    /// Owns Toolbox route requests. Selection and request changes belong to the game thread;
    /// dispatched completions recheck ownership on that thread, not before they are queued.
    /// </summary>
    internal sealed class RouteLoadCoordinator
    {
        private readonly Func<Func<Task>, Task> dispatchAsync;
        private readonly object mapPhaseLock = new object();
        private Task mapTail = Task.CompletedTask;
        private volatile bool stopped;
        private long selection;

        /// <summary>The accepted route request; identity remains available after its load scope completes.</summary>
        public Request Current { get; private set; }

        public RouteLoadCoordinator(Func<Func<Task>, Task> dispatchAsync)
        {
            this.dispatchAsync = dispatchAsync ?? throw new ArgumentNullException(nameof(dispatchAsync));
        }

        /// <summary>Records intent without canceling a load until unsaved-change confirmation succeeds.</summary>
        public long BeginSelection()
        {
            return stopped ? throw new InvalidOperationException("Route loading has stopped.") : ++selection;
        }

        public bool IsLatestSelection(long intent) => !stopped && intent == selection;

        /// <summary>Accepts a selection synchronously, before starting any asynchronous route work.</summary>
        public Request BeginRequest(long intent)
        {
            if (!IsLatestSelection(intent))
                return null;

            CancelCurrent();
            return Current = new Request();
        }

        public bool IsCurrent(Request request) => !stopped && request != null && ReferenceEquals(Current, request) && !request.Token.IsCancellationRequested;

        /// <summary>Invalidates loads and pending confirmations/discovery without clearing runtime globals.</summary>
        public void Invalidate()
        {
            ++selection;
            CancelCurrent();
        }

        /// <summary>Cancels the accepted route while preserving the current selection/discovery intent.</summary>
        public void CancelCurrent()
        {
            Request previous = Current;
            Current = null;
            previous?.Cancel();
        }

        /// <summary>Runs a route-owned UI completion only if the request still owns the game thread.</summary>
        public async Task<bool> CommitAsync(Request request, Action action)
        {
            // Followups begun after shutdown must not enqueue new UI work.
            if (stopped)
                return false;

            bool committed = false;
            await dispatchAsync(() =>
            {
                if (IsCurrent(request))
                {
                    action();
                    committed = true;
                }
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            return committed;
        }

        /// <summary>
        /// Prepares runtime topology and initializes a local host candidate without publication.
        /// Only a fully initialized, successfully committed, still-current candidate is published.
        /// Rejected candidates are drained and discarded on the game thread before another map tail.
        /// Initialization includes any validation required before commit, within the serialized map phase.
        /// The caller disposes the request after the load and all awaited startup followups complete.
        /// </summary>
        public async Task<bool> LoadAsync(Request request, RouteModel route, bool metricUnits,
            Func<RuntimeDataResolver, Func<Action, Task>, Task> initializeMapAsync, Action commit, Action discard)
        {
            CancellationToken token = request.Token;
            if (stopped)
                return false;

            TaskCompletionSource mapCompleted = null;
            bool entered = false;
            bool committed = false;
            try
            {
                RuntimeDataResolver prepared = await RuntimeDataResolver.PrepareAsync(route, metricUnits, null, token).ConfigureAwait(false);
                // Retain serialization: owner-side rendering still touches shared host/cache state.
                // Route-local runtimes alone do not prove overlapping map tails are safe.
                mapCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Task previous;
                lock (mapPhaseLock)
                {
                    previous = mapTail;
                    // Canceling this waiter must not let its successor overtake the previous map.
                    mapTail = Task.WhenAll(previous, mapCompleted.Task);
                }
                await previous.WaitAsync(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                entered = true;
                await dispatchAsync(async () =>
                {
                    if (!IsCurrent(request))
                        return;

                    await initializeMapAsync(prepared, async action =>
                    {
                        if (!await CommitAsync(request, action).ConfigureAwait(false))
                            throw new OperationCanceledException(token);
                    }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                _ = await CommitAsync(request, () =>
                {
                    commit();
                    // Owner callbacks can re-enter selection/stop. Never publish the superseded route.
                    if (IsCurrent(request))
                    {
                        prepared.Publish();
                        committed = true;
                    }
                }).ConfigureAwait(false);
                return committed;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested || stopped)
            {
                return false;
            }
            finally
            {
                try
                {
                    if (entered && !committed)
                        await dispatchAsync(() =>
                        {
                            discard();
                            return Task.CompletedTask;
                        }).ConfigureAwait(false);
                }
                finally
                {
                    // Only successful completion signals enter the tail; load failures cannot poison it.
                    mapCompleted?.SetResult();
                }
            }
        }

        /// <summary>Permanently rejects new work and cancels ownership; already-started maps still drain and discard.</summary>
        public void Stop()
        {
            if (stopped)
                return;

            stopped = true;
            Invalidate();
        }

        /// <summary>Owns cancellation for one route load and its awaited startup followups.</summary>
        internal sealed class Request : IDisposable
        {
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
            private readonly object lifetime = new object();
            private bool disposed;

            /// <summary>The creation-time token; cancellation state remains readable after source disposal.</summary>
            public CancellationToken Token { get; }

            internal Request()
            {
                Token = cancellation.Token;
            }

            internal void Cancel()
            {
                lock (lifetime)
                {
                    if (!disposed)
                        cancellation.Cancel();
                }
            }

            /// <summary>Releases load-scoped resources without canceling or clearing the accepted request.</summary>
            public void Dispose()
            {
                lock (lifetime)
                {
                    if (disposed)
                        return;
                    cancellation.Dispose();
                    disposed = true;
                }
            }
        }
    }
}
