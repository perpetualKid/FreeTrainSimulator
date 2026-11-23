using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes;

namespace Tests.FreeTrainSimulator.ActivityRunner
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class ProcessShutdownTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void UnstartedWorkerCanBeStoppedAndJoined()
        {
            using (CountingProcess process = new CountingProcess())
            {
                process.Stop();
                process.WaitForTermination();
                Assert.AreEqual(0, process.UpdateCount);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public void StopWakesIdleWorkerWithoutRunningAnotherUpdate()
        {
            using (CountingProcess process = new CountingProcess())
            {
                process.Start();
                process.Stop();
                process.WaitForTermination();
                Assert.AreEqual(0, process.UpdateCount);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task JoinWaitsForCanceledAsyncLoaderToActuallyFinish()
        {
            GameHost game = CreateGame();
            using (LoaderProcess loader = new LoaderProcess(game))
            {
                using (DelayedLoadingState state = new DelayedLoadingState(loader.CancellationToken))
                {
                    SetField(game, "<LoaderProcess>k__BackingField", loader);
                    game.PushState(state);
                    loader.Start();
                    loader.TriggerUpdate(new GameTime());
                    Task joined = null;
                    try
                    {
                        await state.Entered.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        loader.Stop();
                        TaskCompletionSource joining = Completion();
                        joined = Task.Run(() =>
                        {
                            joining.SetResult();
                            loader.WaitForTermination();
                        }, TestContext.CancellationToken);
                        await joining.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        Task delay = Task.Delay(100, TestContext.CancellationToken);
                        Assert.AreSame(delay, await Task.WhenAny(joined, delay).ConfigureAwait(false));
                        Assert.IsFalse(loader.Finished);
                        Assert.IsFalse(state.Finished);
                    }
                    finally
                    {
                        loader.Stop();
                        state.Release.SetResult();
                        if (joined != null)
                            await joined.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        else
                            loader.WaitForTermination();
                    }
                    Assert.IsTrue(state.Finished);
                    Assert.IsTrue(loader.Finished);
                    Assert.AreEqual(0, state.ViewerInitializations);
                    Assert.AreEqual(1, state.LoadCount);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task LoaderStillCompletesNormallyBeforeShutdown()
        {
            GameHost game = CreateGame();
            using (LoaderProcess loader = new LoaderProcess(game))
            {
                using (DelayedLoadingState state = new DelayedLoadingState(loader.CancellationToken))
                {
                    SetField(game, "<LoaderProcess>k__BackingField", loader);
                    game.PushState(state);
                    loader.Start();
                    loader.TriggerUpdate(new GameTime());
                    try
                    {
                        await state.Entered.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        state.Release.SetResult();
                        loader.WaitForComplection();
                        Assert.IsTrue(state.Finished);
                        Assert.AreEqual(1, state.ViewerInitializations);
                        Assert.AreEqual(1, state.LoadCount);
                    }
                    finally
                    {
                        loader.Stop();
                        state.Release.TrySetResult();
                        loader.WaitForTermination();
                    }
                }
            }
        }

        [TestMethod]
        public void NormalStateReplacementStillDisposesPreviousState()
        {
            GameHost game = CreateGame();
            using (LoaderProcess loader = new LoaderProcess(game))
            {
                using (TrackingState current = new TrackingState())
                {
                    using (TrackingState replacement = new TrackingState())
                    {
                        SetField(game, "<LoaderProcess>k__BackingField", loader);
                        game.PushState(current);
                        game.ReplaceState(replacement);
                        Assert.AreSame(replacement, game.State);
                        Assert.IsTrue(current.Disposed);
                        Assert.IsFalse(replacement.Disposed);
                    }
                }
            }
        }

        [TestMethod]
        public void ShutdownRejectsStatePublicationWithoutDisposingCurrentState()
        {
            GameHost game = CreateGame();
            using (LoaderProcess loader = new LoaderProcess(game))
            {
                using (TrackingState current = new TrackingState())
                {
                    using (TrackingState replacement = new TrackingState())
                    {
                        SetField(game, "<LoaderProcess>k__BackingField", loader);
                        game.PushState(current);
                        loader.Stop();
                        Assert.ThrowsExactly<OperationCanceledException>(() => game.ReplaceState(replacement));
                        Assert.ThrowsExactly<OperationCanceledException>(() => game.PushState(replacement));
                        Assert.AreSame(current, game.State);
                        Assert.IsFalse(current.Disposed);
                    }
                }
            }
        }

        [TestMethod]
        public void CanceledLoadingRejectsViewerCreationBeforeAccessingSimulatorOrGraphics()
        {
            GameHost game = CreateGame();
            using (LoaderProcess loader = new LoaderProcess(game))
            {
                using (GameStateRunActivity state = new GameStateRunActivity(Array.Empty<string>()) { Game = game })
                {
                    SetField(game, "<LoaderProcess>k__BackingField", loader);
                    loader.Stop();
                    MethodInfo initializeViewer = typeof(GameStateRunActivity).GetMethod("InitializeViewer", BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.IsNotNull(initializeViewer);
                    TargetInvocationException error = Assert.ThrowsExactly<TargetInvocationException>(() => initializeViewer.Invoke(state, null));
                    Assert.IsInstanceOfType<OperationCanceledException>(error.InnerException);
                }
            }
        }

        private static GameHost CreateGame()
        {
            // Only the state stack and loader are needed; never construct a window or graphics device.
            GameHost game = (GameHost)RuntimeHelpers.GetUninitializedObject(typeof(GameHost));
            SetField(game, "gameStates", new Stack<GameState>());
            return game;
        }

        private static void SetField(GameHost game, string name, object value)
        {
            FieldInfo field = typeof(GameHost).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(game, value);
        }

        private static TaskCompletionSource Completion() => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        private sealed class CountingProcess : ProcessBase
        {
            public int UpdateCount { get; private set; }

            public CountingProcess() : base(null, "ShutdownTest")
            {
            }

            protected override void Update(GameTime gameTime)
            {
                UpdateCount++;
            }
        }

        private sealed class DelayedLoadingState : GameState
        {
            private readonly CancellationToken cancellationToken;
            public TaskCompletionSource Entered { get; } = Completion();
            public TaskCompletionSource Release { get; } = Completion();
            public bool Finished { get; private set; }
            public int ViewerInitializations { get; private set; }
            public int LoadCount { get; private set; }

            public DelayedLoadingState(CancellationToken cancellationToken)
            {
                this.cancellationToken = cancellationToken;
            }

            internal override async Task Load()
            {
                LoadCount++;
                Entered.SetResult();
                try
                {
                    await Release.Task.ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    ViewerInitializations++;
                }
                finally
                {
                    Finished = true;
                }
            }
        }

        private sealed class TrackingState : GameState
        {
            public bool Disposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
