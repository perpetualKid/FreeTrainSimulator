using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Toolbox;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class GameThreadDispatcherTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenCalledFromForeignThreadThenActionRunsOnOwner()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                GameWindow game = CreateAdapter(host);

                int threadId = await game.InvokeOnGameThreadAsync(() => Task.FromResult(Environment.CurrentManagedThreadId))
                    .WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreEqual(host.OwnerThreadId, threadId);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenCalledOnOwnerThenActionBeginsInline()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                GameWindow game = CreateAdapter(host);
                bool beganInline = false;

                await host.OnOwnerAsync(() =>
                {
                    bool invoked = false;
                    _ = game.InvokeOnGameThreadAsync(() =>
                    {
                        invoked = true;
                        return Task.CompletedTask;
                    });
                    beganInline = invoked;
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                Assert.IsTrue(beganInline);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenControlIsDisposedBeforeSubmissionThenOperationIsRejected()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                GameWindow game = CreateAdapter(host);
                await host.OnOwnerAsync(host.DispatchControl.Dispose).ConfigureAwait(false);

                _ = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                    () => game.InvokeOnGameThreadAsync(() => Task.FromResult(7))).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenQueuedOperationLosesItsControlThenItFaultsWithoutExecution()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                GameWindow game = CreateAdapter(host);
                TaskCompletionSource entered = Completion();
                using (ManualResetEventSlim release = new ManualResetEventSlim())
                {
                    Task blocker = host.OnOwnerAsync(() =>
                    {
                        entered.SetResult();
                        release.Wait(TestContext.CancellationToken);
                        host.DispatchControl.Dispose();
                    });
                    try
                    {
                        await entered.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        bool executed = false;
                        Task<int> queued = game.InvokeOnGameThreadAsync(() =>
                        {
                            executed = true;
                            return Task.FromResult(7);
                        });
                        release.Set();

                        _ = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => queued.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                        Assert.IsFalse(executed);
                    }
                    finally
                    {
                        release.Set();
                        await blocker.ConfigureAwait(false);
                    }
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenRunningOperationLosesItsControlThenCompletionWaitsForActualResult()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                GameWindow game = CreateAdapter(host);
                TaskCompletionSource entered = Completion();
                TaskCompletionSource<int> result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                Task<int> operation = game.InvokeOnGameThreadAsync(() =>
                {
                    entered.SetResult();
                    return result.Task;
                });
                try
                {
                    await entered.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await host.OnOwnerAsync(host.DispatchControl.Dispose).ConfigureAwait(false);
                    bool completedBeforeResult = operation.IsCompleted;
                    result.SetResult(42);
                    int actual = await operation.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                    Assert.AreEqual((false, 42), (completedBeforeResult, actual));
                }
                finally
                {
                    result.TrySetResult(42);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenRunningOperationFaultsAfterDisposalThenActualFailureIsReported()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                GameWindow game = CreateAdapter(host);
                TaskCompletionSource entered = Completion();
                TaskCompletionSource result = Completion();
                InvalidOperationException expected = new InvalidOperationException();
                Task operation = game.InvokeOnGameThreadAsync(() =>
            {
                entered.SetResult();
                return result.Task;
            });
                try
                {
                    await entered.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await host.OnOwnerAsync(host.DispatchControl.Dispose).ConfigureAwait(false);
                    result.SetException(expected);

                    InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                        () => operation.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);

                    Assert.AreSame(expected, actual);
                }
                finally
                {
                    result.TrySetResult();
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenRunningOperationIsCanceledAfterDisposalThenActualCancellationIsReported()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                GameWindow game = CreateAdapter(host);
                TaskCompletionSource entered = Completion();
                TaskCompletionSource result = Completion();
                Task operation = game.InvokeOnGameThreadAsync(() =>
                {
                    entered.SetResult();
                    return result.Task;
                });
                try
                {
                    await entered.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await host.OnOwnerAsync(host.DispatchControl.Dispose).ConfigureAwait(false);
                    result.SetCanceled();

                    await Assert.ThrowsAsync<OperationCanceledException>(
                        () => operation.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                }
                finally
                {
                    result.TrySetResult();
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenActionThrowsSynchronouslyThenReturnedTaskFaults()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                GameWindow game = CreateAdapter(host);
                InvalidOperationException expected = new InvalidOperationException();

                InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => game.InvokeOnGameThreadAsync((Func<Task>)(() => throw expected))).ConfigureAwait(false);

                Assert.AreSame(expected, actual);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenActionIsCanceledThenReturnedTaskIsCanceled()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                GameWindow game = CreateAdapter(host);
                using (CancellationTokenSource cancellation = new CancellationTokenSource())
                {
                    await cancellation.CancelAsync().ConfigureAwait(false);
                    Task operation = game.InvokeOnGameThreadAsync(() => Task.FromCanceled(cancellation.Token));

                    _ = await Assert.ThrowsAsync<OperationCanceledException>(() => operation).ConfigureAwait(false);
                }
            }
        }

        private static TaskCompletionSource Completion() => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        private static GameWindow CreateAdapter(StaControlHost host)
        {
            // Exercise production dispatch without constructing MonoGame or a GraphicsDevice.
            GameWindow game = (GameWindow)RuntimeHelpers.GetUninitializedObject(typeof(GameWindow));
#pragma warning disable CA1816 // Intentional: uninitialized test object must not be finalized.
            GC.SuppressFinalize(game);
#pragma warning restore CA1816
            typeof(GameWindow).GetField("windowForm", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, host.Target);
            typeof(GameWindow).GetField("ownerControl", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, host.DispatchControl);
            return game;
        }
    }
}
