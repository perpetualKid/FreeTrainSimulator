using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.ViewModels;

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
            terminated.SetResult();
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => first.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);

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
                    scope.Session.OnPathEditorChanged += (_, _) => editorReleasedOn = Environment.CurrentManagedThreadId;
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
        public async Task WhenSessionDrainFailsThenResourcesAreReleasedAndNativeExitCanComplete()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            try
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

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => preparation.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                Assert.IsTrue(scope.Session.IsDisposed, "Finished failed jobs no longer borrow session resources.");
                Assert.IsFalse(stop.IsCompleted, "Stop must still await actual owner teardown.");
                ExitingEventArgs subsequentExit = new ExitingEventArgs();
                await scope.Host.OnOwnerAsync(() => RaiseNativeExit(scope.Game, subsequentExit)).ConfigureAwait(false);
                Assert.IsFalse(subsequentExit.Cancel);
                terminated.SetResult();
                InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => stop.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);

                Assert.AreSame(expected, actual);
                Assert.AreSame(stop, host.StopAsync());
            }
            finally
            {
                // The deliberately failed drain remains diagnostic even after successful release.
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask()).ConfigureAwait(false);
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
        public void WhenOwnerFinishesWithoutResourceReleaseEvidenceThenHostDisposalIsRejected()
        {
            TaskCompletionSource terminated = Completion();
            ToolboxGameHostControl host = CreateShutdownHost(
                new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously), terminated);
            terminated.SetResult();

            Assert.IsFalse(host.IsStoppedSafely);
            Assert.ThrowsExactly<InvalidOperationException>(host.Dispose);
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

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenRunFailsAfterConfirmedOwnerReleaseThenShellReleasesOnceAndReportsFailure(bool lateOwnerRelease)
        {
            StaControlHost shellOwner = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (shellOwner.ConfigureAwait(false))
            {
                await shellOwner.OnOwnerAsync(() =>
                {
                    TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
                    TaskCompletionSource terminated = Completion();
                    ToolboxGameHostControl host = CreateShutdownHost(ready, terminated);
                    InvalidOperationException expected = new InvalidOperationException("Run failed after startup.");
                    ready.SetException(expected);
                    SetField(host, "ownerResourcesReleased", !lateOwnerRelease);
                    terminated.SetException(expected);
                    Assert.AreEqual(!lateOwnerRelease, host.IsStoppedSafely);

                    System.Windows.Forms.Panel panel = (System.Windows.Forms.Panel)Activator.CreateInstance(
                        typeof(ToolboxGameHostControl).GetNestedType("HostPanel", BindingFlags.NonPublic),
                        new object[] { (Action)(() => { }) });
                    SetField(host, "hostPanel", panel);
                    int panelReleases = 0;
                    panel.Disposed += (_, _) => ++panelReleases;

                    MainWindow shell = CreateShutdownShell(host);
                    MethodInfo shutdown = typeof(MainWindow).GetMethod("ShutdownAsync", BindingFlags.Instance | BindingFlags.NonPublic);
                    Task first = (Task)shutdown.Invoke(shell, null);
                    InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => first.GetAwaiter().GetResult());
                    Assert.AreSame(expected, actual);
                    Assert.AreEqual(!lateOwnerRelease, (bool)typeof(MainWindow).GetField("shutdownCompleted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell));
                    if (lateOwnerRelease)
                    {
                        Assert.AreEqual(0, panelReleases);
                        Assert.ThrowsExactly<InvalidOperationException>(host.Dispose);
                        // A later attempt may release only once new safe-termination evidence exists.
                        SetField(host, "ownerResourcesReleased", true);
                    }
                    Task second = (Task)shutdown.Invoke(shell, null);
                    Assert.AreSame(first, second);
                    Assert.ThrowsExactly<InvalidOperationException>(() => second.GetAwaiter().GetResult());
                    Assert.IsTrue((bool)typeof(MainWindow).GetField("shutdownCompleted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell));
                    host.Dispose();
                    shell.Dispose();
                    shell.Dispose();
                    Assert.AreEqual(1, panelReleases);
                }).ConfigureAwait(false);
            }
        }

        private static MainWindow CreateShutdownShell(ToolboxGameHostControl host)
        {
            // Exercise the real WPF cleanup without constructing the XAML host's graphics startup.
            MainWindow shell = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
#pragma warning disable CA1816 // Intentional: uninitialized test object must not be finalized.
            GC.SuppressFinalize(shell);
#pragma warning restore CA1816
            SetField(shell, "MapHost", host);
            SetField(shell, "DockingManager", new AvalonDock.DockingManager());
            SetField(shell, "viewModel", new MainWindowViewModel());
            SetField(shell, "shortcutInputManager", System.Windows.Input.InputManager.Current);
            SetField(shell, "shortcutPressTracker", new ToolWindowShortcutPressTracker());
            Type descriptor = typeof(MainWindow).GetNestedType("ToolWindowDescriptor", BindingFlags.NonPublic);
            SetField(shell, "toolWindowDescriptors", Array.CreateInstance(descriptor, 0));
            return shell;
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenResourceCleanupFailsThenDiagnosticsArePreservedAndShellTeardownIsBlocked(bool failJob)
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            TaskCompletionSource release = Completion();
            InvalidOperationException jobFailure = new InvalidOperationException("Save failed.");
            InvalidOperationException cleanupFailure = new InvalidOperationException("Owner cleanup failed.");
            try
            {
                TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource terminated = Completion();
                ToolboxGameHostControl host = CreateShutdownHost(ready, terminated);
                ready.SetResult(scope.Game);
                Task preparation = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetField(scope.Session, "pathEditor", CreateEditor());
                    scope.Session.OnPathEditorChanged += (_, _) => throw cleanupFailure;
                    GameWindow.ObserveGameThreadOperation(scope.Session.RunAsync(() => release.Task));
                    preparation = scope.Game.StopForExitAsync();
                }).ConfigureAwait(false);
                Task stop = host.StopAsync();
                if (failJob)
                    release.SetException(jobFailure);
                else
                    release.SetResult();
                GameWindow.ObserveGameThreadOperation(preparation);
                Exception diagnostic = await Assert.ThrowsAsync<Exception>(() => stop.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                if (failJob)
                    CollectionAssert.AreEquivalent(new Exception[] { jobFailure, cleanupFailure }, ((AggregateException)diagnostic).Flatten().InnerExceptions);
                else
                    Assert.AreSame(cleanupFailure, diagnostic);
                Assert.IsFalse(scope.Session.IsDisposed);
                ExitingEventArgs subsequentExit = new ExitingEventArgs();
                await scope.Host.OnOwnerAsync(() => RaiseNativeExit(scope.Game, subsequentExit)).ConfigureAwait(false);
                Assert.IsTrue(subsequentExit.Cancel);
                terminated.SetException(cleanupFailure);
                Assert.IsFalse(host.IsStoppedSafely);
                Assert.ThrowsExactly<InvalidOperationException>(host.Dispose);

                // Even a finished owner with failed/unconfirmed disposal cannot release the shell.
                await scope.Host.OnOwnerAsync(() =>
                {
                    MainWindow shell = CreateShutdownShell(host);
                    Task shutdown = (Task)typeof(MainWindow).GetMethod("ShutdownAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
                    Assert.Throws<Exception>(() => shutdown.GetAwaiter().GetResult());
                    Assert.IsFalse((bool)typeof(MainWindow).GetField("shutdownCompleted", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shell));
                    Assert.ThrowsExactly<InvalidOperationException>(shell.Dispose);
                }).ConfigureAwait(false);
            }
            finally
            {
                release.TrySetResult();
                if (failJob)
                    await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask()).ConfigureAwait(false);
                else
                    await scope.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static void RaiseNativeExit(GameWindow game, ExitingEventArgs args)
            => typeof(GameWindow).GetMethod("GameWindow_Exiting", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, new object[] { game, args });
    }
}
