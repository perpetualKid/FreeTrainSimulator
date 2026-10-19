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
        private readonly SemaphoreSlim mapPhase = new SemaphoreSlim(1, 1);
        private long selection;

        /// <summary>The accepted route request; identity remains available after its load scope completes.</summary>
        public Request Current { get; private set; }

        public RouteLoadCoordinator(Func<Func<Task>, Task> dispatchAsync)
        {
            this.dispatchAsync = dispatchAsync ?? throw new ArgumentNullException(nameof(dispatchAsync));
        }

        /// <summary>Records intent without canceling a load until unsaved-change confirmation succeeds.</summary>
        public long BeginSelection() => ++selection;

        public bool IsLatestSelection(long intent) => intent == selection;

        /// <summary>Accepts a selection synchronously, before starting any asynchronous route work.</summary>
        public Request BeginRequest(long intent)
        {
            if (!IsLatestSelection(intent))
                return null;

            CancelCurrent();
            return Current = new Request();
        }

        public bool IsCurrent(Request request) => request != null && ReferenceEquals(Current, request) && !request.Token.IsCancellationRequested;

        /// <summary>Invalidates loads and pending confirmations/discovery without clearing runtime globals.</summary>
        public void Invalidate()
        {
            _ = BeginSelection();
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
        /// Prepares native data without publication, then publishes and initializes a local host candidate.
        /// Rejected candidates are drained and discarded on the game thread before another publication.
        /// Initialization includes any validation required before commit, within the serialized map phase.
        /// The caller disposes the request after the load and all awaited startup followups complete.
        /// </summary>
        public async Task<bool> LoadAsync(Request request, RouteModel route, bool metricUnits,
            Func<RuntimeDataResolver, Func<Action, Task>, Task> initializeMapAsync, Action commit, Action discard)
        {
            CancellationToken token = request.Token;
            bool entered = false;
            bool committed = false;
            try
            {
                RuntimeDataResolver prepared = await RuntimeDataResolver.PrepareAsync(route, metricUnits, null, token).ConfigureAwait(false);
                // Map widgets still read runtime globals; serialize the full map phase until
                // those readers can use a route-local runtime instead.
                await mapPhase.WaitAsync(token).ConfigureAwait(false);
                entered = true;
                await dispatchAsync(async () =>
                {
                    if (!IsCurrent(request))
                        return;

                    prepared.Publish();
                    await initializeMapAsync(prepared, async action =>
                    {
                        if (!await CommitAsync(request, action).ConfigureAwait(false))
                            throw new OperationCanceledException(token);
                    }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                committed = await CommitAsync(request, commit).ConfigureAwait(false);
                return committed;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return false;
            }
            finally
            {
                if (entered)
                {
                    try
                    {
                        if (!committed)
                            await dispatchAsync(() =>
                            {
                                discard();
                                return Task.CompletedTask;
                            }).ConfigureAwait(false);
                    }
                    finally
                    {
                        mapPhase.Release();
                    }
                }
            }
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
