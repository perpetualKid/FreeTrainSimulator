using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ExitingEventArgs = Microsoft.Xna.Framework.ExitingEventArgs;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenHostStopIsRepeatedThenItSharesTheSameTask()
        {
            TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource terminated = Completion();
            ToolboxGameHostControl host = CreateShutdownHost(ready, terminated);

            Task first = host.StopAsync();
            Task second = host.StopAsync();
            ready.SetException(new InvalidOperationException("Startup failed."));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => first.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
            terminated.SetResult();

            Assert.AreSame(first, second);
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenHostClosesBeforeReadyThenStopWaitsForReadinessAndActualOwnerCompletion()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource terminated = Completion();
                ToolboxGameHostControl host = CreateShutdownHost(ready, terminated);

                Task stop = host.StopAsync();
                bool waitsForStartup = !stop.IsCompleted;
                ready.SetResult(scope.Game);
                await scope.Game.StopForExitAsync().WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                bool waitsForOwnerTeardown = !stop.IsCompleted;
                terminated.SetResult();
                await stop.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreEqual((true, true, true), (waitsForStartup, waitsForOwnerTeardown, scope.Session.IsDisposed));
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenStartupFailsBeforeReadyThenHostStopReportsFailureWithoutWaitingForever()
        {
            TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource terminated = Completion();
            ToolboxGameHostControl host = CreateShutdownHost(ready, terminated);
            InvalidOperationException expected = new InvalidOperationException("Constructor failed.");
            Task stop = host.StopAsync();

            ready.SetException(expected);
            terminated.SetException(expected);
            InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => stop.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);

            Assert.AreSame(expected, actual);
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenOwnerDisposalFailsThenHostStopReportsItsFailure()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource terminated = Completion();
                ToolboxGameHostControl host = CreateShutdownHost(ready, terminated);
                ready.SetResult(scope.Game);
                Task stop = host.StopAsync();
                await scope.Game.StopForExitAsync().WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                InvalidOperationException expected = new InvalidOperationException("Owner disposal failed.");

                terminated.SetException(expected);
                InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => stop.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);

                Assert.AreSame(expected, actual);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenNativeExitHasLiveWorkThenHostWaitsForCleanupAndOwnerResourceRelease()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource terminated = Completion();
                ToolboxGameHostControl host = CreateShutdownHost(ready, terminated);
                ready.SetResult(scope.Game);
                TaskCompletionSource started = Completion();
                TaskCompletionSource releaseWork = Completion();
                TaskCompletionSource cleanupEntered = Completion();
                TaskCompletionSource releaseCleanup = Completion();
                int editorReleasedOn = 0;
                int cleanupThread = 0;
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetField(scope.Session, "pathEditor", CreateEditor());
                    scope.Game.OnPathEditorChanged += (_, _) => editorReleasedOn = Environment.CurrentManagedThreadId;
                }).ConfigureAwait(false);
                Task job = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.RunAsync(async () =>
                {
                    started.SetResult();
                    await releaseWork.Task.ConfigureAwait(false);
                    await scope.Game.InvokeOnGameThreadAsync(async () =>
                    {
                        cleanupThread = Environment.CurrentManagedThreadId;
                        cleanupEntered.SetResult();
                        await releaseCleanup.Task.ConfigureAwait(true);
                    }).ConfigureAwait(false);
                }));
                try
                {
                    await started.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    Task stop = host.StopAsync();
                    ExitingEventArgs nativeExit = new ExitingEventArgs();
                    await scope.Host.OnOwnerAsync(() => RaiseNativeExit(scope.Game, nativeExit)).ConfigureAwait(false);
                    bool retainedWhileWorking = nativeExit.Cancel && !stop.IsCompleted && !scope.Session.IsDisposed;
                    releaseWork.SetResult();
                    await cleanupEntered.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    bool retainedWhileCleaning = !stop.IsCompleted && !scope.Session.IsDisposed;
                    releaseCleanup.SetResult();
                    await scope.Game.StopForExitAsync().WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    bool waitsForTermination = !stop.IsCompleted;
                    ExitingEventArgs drainedExit = new ExitingEventArgs();
                    await scope.Host.OnOwnerAsync(() => RaiseNativeExit(scope.Game, drainedExit)).ConfigureAwait(false);
                    terminated.SetResult();
                    await stop.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                    Assert.AreEqual((true, true, true, false, scope.Host.OwnerThreadId, scope.Host.OwnerThreadId),
                        (retainedWhileWorking, retainedWhileCleaning, waitsForTermination, drainedExit.Cancel, cleanupThread, editorReleasedOn));
                }
                finally
                {
                    releaseWork.TrySetResult();
                    releaseCleanup.TrySetResult();
                    await job.ConfigureAwait(false);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenSessionDrainFailsThenHostKeepsTheLiveOwnerAndReportsFailure()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource terminated = Completion();
                ToolboxGameHostControl host = CreateShutdownHost(ready, terminated);
                ready.SetResult(scope.Game);
                TaskCompletionSource result = Completion();
                Task job = null;
                Task preparation = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    job = scope.Session.RunAsync(() => result.Task);
                    preparation = scope.Game.StopForExitAsync();
                }).ConfigureAwait(false);
                Task stop = host.StopAsync();
                InvalidOperationException expected = new InvalidOperationException("Save failed.");
                result.SetException(expected);
                GameWindow.ObserveGameThreadOperation(job);
                GameWindow.ObserveGameThreadOperation(preparation);

                InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => stop.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);

                Assert.AreEqual((expected, false, false), (actual, terminated.Task.IsCompleted, scope.Session.IsDisposed));
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenSessionIsNotDrainedThenOwnerDisposalIsRejected()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                TaskCompletionSource release = Completion();
                Task job = null;
                await scope.Host.OnOwnerAsync(() => job = scope.Session.RunAsync(() => release.Task)).ConfigureAwait(false);
                try
                {
                    await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                        () => scope.Host.OnOwnerAsync(scope.Session.Dispose)).ConfigureAwait(false);
                }
                finally
                {
                    release.TrySetResult();
                    await job.ConfigureAwait(false);
                    await scope.Game.StopSessionAsync().ConfigureAwait(false);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenSessionDisposalIsOffOwnerThenItIsRejected()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                _ = Assert.ThrowsExactly<InvalidOperationException>(scope.Session.Dispose);
            }
        }

        [TestMethod]
        public void WhenNativeOwnerIsLiveThenHostDisposalIsRejected()
        {
            ToolboxGameHostControl host = CreateShutdownHost(
                new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously), Completion());

            _ = Assert.ThrowsExactly<InvalidOperationException>(host.Dispose);
        }

        [TestMethod]
        public void WhenNativeOwnerFaultsThenHostDisposalIsRejected()
        {
            TaskCompletionSource terminated = Completion();
            ToolboxGameHostControl host = CreateShutdownHost(
                new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously), terminated);
            terminated.SetException(new InvalidOperationException("Owner failed."));

            _ = Assert.ThrowsExactly<InvalidOperationException>(host.Dispose);
        }

        [TestMethod]
        public void WhenShellIsClosedDuringConfirmationThenTheNestedCloseIsCanceled()
        {
            MainWindow shell = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            // No constructor-owned resources were created for this closing-guard assertion.
            SetField(shell, "isShuttingDown", true);
            CancelEventArgs closing = new CancelEventArgs();

            typeof(MainWindow).GetMethod("OnClosing", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, new object[] { closing });

            Assert.IsTrue(closing.Cancel);
        }

        [TestMethod]
        public void WhenShellHasNotCompletedShutdownThenExplicitDisposalIsRejected()
        {
            MainWindow shell = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
#pragma warning disable CA1816 // Intentional: uninitialized test object must not be finalized.
            GC.SuppressFinalize(shell);
#pragma warning restore CA1816

            _ = Assert.ThrowsExactly<InvalidOperationException>(shell.Dispose);
        }

        private static ToolboxGameHostControl CreateShutdownHost(TaskCompletionSource<GameWindow> ready, TaskCompletionSource terminated)
        {
            // Only replace hardware startup/termination signals; stop uses the real session and STA transport.
            ToolboxGameHostControl host = (ToolboxGameHostControl)RuntimeHelpers.GetUninitializedObject(typeof(ToolboxGameHostControl));
#pragma warning disable CA1816 // Intentional: uninitialized test object must not be finalized.
            GC.SuppressFinalize(host);
#pragma warning restore CA1816
#pragma warning disable CS9216 // Reflection injection only; the host locks its strongly typed Lock field.
            SetField(host, "syncLock", new Lock());
#pragma warning restore CS9216
            SetField(host, "ready", ready);
            SetField(host, "gameThreadCompletion", terminated);
            GameWindow.ObserveGameThreadOperation(ready.Task);
            GameWindow.ObserveGameThreadOperation(terminated.Task);
            return host;
        }

        private static void RaiseNativeExit(GameWindow game, ExitingEventArgs args)
            => typeof(GameWindow).GetMethod("GameWindow_Exiting", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, new object[] { game, args });
    }
}
