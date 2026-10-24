using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Game = Microsoft.Xna.Framework.Game;
using GameComponentCollection = Microsoft.Xna.Framework.GameComponentCollection;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests : IDisposable
    {
        private FailingMapEnvironment emergencyMapEnvironment;
        private InvalidOperationException expectedMapEnvironmentFailure;

        [TestMethod]
        [DataRow("discard", true)]
        [DataRow("registration", true)]
        [DataRow("activation", true)]
        [DataRow("discard", false)]
        [DataRow("registration", false)]
        [DataRow("activation", false)]
        [DataRow("construction", true)]
        [DataRow("construction", false)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task CompletedCandidateFailureCannotLoseOwnerReleaseEvidence(string stage, bool failRelease)
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            try
            {
                InvalidOperationException operationFailure = new InvalidOperationException(stage);
                InvalidOperationException releaseFailure = new InvalidOperationException("Detached candidate release failed.");
                Exception retainedReleaseFailure = null;
                TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource terminated = Completion();
                ToolboxGameHostControl host = CreateShutdownHost(ready, terminated);
                ready.SetResult(scope.Game);
                await scope.Host.OnOwnerAsync(() =>
                {
                    GameWindow game = scope.Game;
                    SetField(game, "hostedPointerGesture", new HostedPointerGestureState());
                    GameComponentCollection components = new GameComponentCollection();
                    typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, components);
                    // No render sizing/device access; attachment registration and rollback remain real.
                    scope.Host.Target.Dispose();
                    FailingMapEnvironment pending = new FailingMapEnvironment(failRelease ? releaseFailure : null);
                    FailingMapEnvironment environment = pending;
                    ResourceSessionFactory factory = new ResourceSessionFactory(ref pending);
                    ContentArea candidate = null;
                    environment.OnRelease = () =>
                    {
                        Assert.AreEqual(scope.Host.OwnerThreadId, Environment.CurrentManagedThreadId);
                        Assert.IsFalse(scope.Host.DispatchControl.IsDisposed);
                    };
                    if (stage == "construction")
                        ((ProbeInteraction)factory.InteractionAdapter).AttachFailure = operationFailure;
                    else
                    {
                        candidate = new CleanupProbeMap(game, factory);
                        typeof(ContentBase).GetField("<Session>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(candidate.Content, new XnaMapSession(candidate));
                    }
                    if (stage == "registration")
                        components.ComponentAdded += (_, args) =>
                        {
                            if (ReferenceEquals(args.GameComponent, candidate))
                                throw operationFailure;
                        };
                    else if (stage == "activation")
                        ((CleanupProbeMap)candidate).OnActivation = () => throw operationFailure;

                    // This admitted operation and its completion bookkeeping finish BEFORE stop.
                    Task operation = scope.Session.RunAsync(() =>
                    {
                        if (stage == "construction")
                        {
                            using ContentArea unexpected = new ContentArea(game,
                                (ToolboxContent)RuntimeHelpers.GetUninitializedObject(typeof(ToolboxContent)), null, factory);
                            Assert.Fail("Construction must fail before a candidate returns.");
                        }
                        if (stage == "discard")
                        {
                            try
                            {
                                throw operationFailure;
                            }
                            catch (InvalidOperationException error)
                            {
                                try
                                {
                                    typeof(GameWindow).GetMethod("DiscardRouteContent", BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
                                        .Invoke(game, new object[] { candidate.Content });
                                }
                                catch (TargetInvocationException cleanupError)
                                {
                                    throw new AggregateException(error, cleanupError.InnerException);
                                }
                                throw;
                            }
                        }
                        game.ContentArea = candidate;
                        return Task.CompletedTask;
                    });
                    Exception diagnostic = Assert.Throws<Exception>(() => operation.GetAwaiter().GetResult());
                    Exception[] expected = failRelease ? new Exception[] { operationFailure, releaseFailure } : new Exception[] { operationFailure };
                    Assert.AreSequenceEqual(expected, diagnostic is AggregateException aggregate ? aggregate.Flatten().InnerExceptions.ToArray() : new[] { diagnostic });
                    if (failRelease)
                        retainedReleaseFailure = stage == "construction" ? diagnostic : Assert.Throws<Exception>(candidate.Dispose);
                    if (stage == "construction")
                        Assert.IsNull(candidate);
                    Assert.IsTrue(operation.IsCompleted);
                    Assert.IsFalse(scope.Session.IsStopping);
                    Assert.IsEmpty((System.Collections.Generic.HashSet<Task>)typeof(ToolboxSession)
                        .GetField("jobs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope.Session));
                    Assert.IsNull(game.ContentArea);
                    Assert.IsEmpty(components);

                    int laterReleases = 0;
                    int laterThread = 0;
                    components.Add(new OwnerCleanupProbe(() =>
                    {
                        laterReleases++;
                        laterThread = Environment.CurrentManagedThreadId;
                        Assert.IsFalse(scope.Host.DispatchControl.IsDisposed);
                    }));
                    ToolboxGameHostControl.DrainSessionOnOwner(game);
                    scope.Session.Dispose();
                    Assert.IsTrue(scope.Session.IsDisposed);
                    // Mirror GameThreadStart: ONLY successful game disposal can publish release proof.
                    try
                    {
                        using (game)
                        {
                        }
                        SetField(host, "ownerResourcesReleased", true);
                        terminated.SetResult();
                    }
                    catch (Exception error) when (failRelease)
                    {
                        Assert.AreSame(retainedReleaseFailure, error);
                        terminated.SetException(error);
                    }
                    Assert.AreEqual(!failRelease, host.IsStoppedSafely);
                    if (failRelease)
                        Assert.AreSame(retainedReleaseFailure, Assert.Throws<Exception>(game.Dispose));
                    else
                        game.Dispose();
                    Assert.AreEqual((1, 1, scope.Host.OwnerThreadId, true),
                        (environment.ReleaseCount, laterReleases, laterThread, scope.Host.DispatchControl.IsDisposed));
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                if (failRelease)
                {
                    Assert.AreSame(retainedReleaseFailure, await Assert.ThrowsAsync<Exception>(
                        () => host.StopAsync().WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false));
                    Assert.IsFalse(host.IsStoppedSafely);
                    _ = Assert.ThrowsExactly<InvalidOperationException>(host.Dispose);
                }
                else
                {
                    await host.StopAsync().WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    Assert.IsTrue(host.IsStoppedSafely);
                }
            }
            finally
            {
                try
                {
                    await scope.Host.OnOwnerAsync(scope.Session.Dispose).ConfigureAwait(false);
                }
                finally
                {
                    await scope.Host.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task PartialConstructionFailureRetainsOwnerEvidenceBeforeAsynchronousJobCompletion()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                TaskCompletionSource releaseOperation = Completion();
                Task operation = null;
                Task admittedCompletion = null;
                InvalidOperationException constructionFailure = new InvalidOperationException("Deferred construction.");
                InvalidOperationException releaseFailure = new InvalidOperationException("Deferred rollback.");
                FailingMapEnvironment environment = null;
                try
                {
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        FailingMapEnvironment pending = new FailingMapEnvironment(releaseFailure);
                        environment = pending;
                        environment.OnRelease = () =>
                        {
                            Assert.AreEqual(scope.Host.OwnerThreadId, Environment.CurrentManagedThreadId);
                            Assert.IsFalse(scope.Host.DispatchControl.IsDisposed);
                        };
                        ResourceSessionFactory factory = new ResourceSessionFactory(ref pending);
                        ((ProbeInteraction)factory.InteractionAdapter).AttachFailure = constructionFailure;
                        operation = scope.Session.RunAsync(async () =>
                        {
                            await releaseOperation.Task.ConfigureAwait(false);
                            Assert.AreNotEqual(scope.Host.OwnerThreadId, Environment.CurrentManagedThreadId);
                            await scope.Game.InvokeOnGameThreadAsync(() =>
                            {
                                using ContentArea unexpected = new ContentArea(scope.Game,
                                    (ToolboxContent)RuntimeHelpers.GetUninitializedObject(typeof(ToolboxContent)), null, factory);
                                Assert.Fail("Construction must fail before returning a candidate.");
                                return Task.CompletedTask;
                            }).ConfigureAwait(false);
                        });
                        System.Collections.Generic.HashSet<Task> jobs = (System.Collections.Generic.HashSet<Task>)typeof(ToolboxSession)
                            .GetField("jobs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope.Session);
                        lock (jobs)
                            admittedCompletion = jobs.Single();
                    }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                    releaseOperation.SetResult();
                    MapContentRollbackException diagnostic = await Assert.ThrowsExactlyAsync<MapContentRollbackException>(
                        () => operation.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                    Assert.AreSequenceEqual(new Exception[] { constructionFailure, releaseFailure }, diagnostic.Flatten().InnerExceptions);
                    Assert.AreSame(diagnostic, await Assert.ThrowsExactlyAsync<MapContentRollbackException>(
                        () => admittedCompletion.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false));
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        ExceptionDispatchInfo retained = (ExceptionDispatchInfo)typeof(GameWindow)
                            .GetField("detachedContentReleaseFailure", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope.Game);
                        Assert.AreSame(diagnostic, retained.SourceException);
                        System.Collections.Generic.HashSet<Task> jobs = (System.Collections.Generic.HashSet<Task>)typeof(ToolboxSession)
                            .GetField("jobs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope.Session);
                        lock (jobs)
                            Assert.IsEmpty(jobs);
                        Assert.IsFalse(scope.Session.IsStopping);
                        Assert.IsTrue(scope.Session.StopAndDrainAsync().IsCompletedSuccessfully);
                        scope.Session.Dispose();
                        scope.Session.Dispose();
                        Assert.AreEqual(1, environment.ReleaseCount);
                    }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    releaseOperation.TrySetResult();
                }
            }
        }

        private sealed class OwnerCleanupProbe : Microsoft.Xna.Framework.IGameComponent, IDisposable
        {
            private readonly Action cleanup;

            internal OwnerCleanupProbe(Action cleanup)
            {
                this.cleanup = cleanup;
            }

            public void Initialize() { }
            public void Dispose() => cleanup();
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenMapResourceReleaseFailsThenFallbackGameDisposalRetryCannotConfirmSafeHostStop()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            InvalidOperationException expected = expectedMapEnvironmentFailure = new InvalidOperationException("Map environment release failed.");
            FailingMapEnvironment pendingEnvironment = null;
            FailingMapEnvironment environment = null;
            InvalidOperationException drainFailure = null;
            InvalidOperationException retryFailure = null;
            try
            {
                emergencyMapEnvironment = new FailingMapEnvironment(expected);
                pendingEnvironment = emergencyMapEnvironment;
                environment = pendingEnvironment;
                TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource terminated = Completion();
                ToolboxGameHostControl host = CreateShutdownHost(ready, terminated);
                ready.SetResult(scope.Game);
                await scope.Host.OnOwnerAsync(() =>
                {
                    using (Game probeGame = new Game())
                    {
                        CleanupProbeMap map = new CleanupProbeMap(probeGame, new ResourceSessionFactory(ref pendingEnvironment));
                        GameComponentCollection components = new GameComponentCollection { map };
                        typeof(Game).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                            .Single(field => field.FieldType == typeof(GameComponentCollection)).SetValue(scope.Game, components);
                        SetField(scope.Game, "hostedPointerGesture", new HostedPointerGestureState());
                        SetField(scope.Game, "contentArea", map);
                        SetField(scope.Session, "pathEditor", CreateEditor());
                        bool editorReleased = false;
                        scope.Session.OnPathEditorChanged += (_, _) => editorReleased = true;
                        map.OnRelease = () =>
                        {
                            Assert.IsTrue(editorReleased);
                            Assert.IsNull(scope.Session.ExistingPathEditor);
                            Assert.IsFalse(scope.Host.DispatchControl.IsDisposed);
                        };
                        // Match GameThreadStart's fallback topology: the dead render form, failed drain,
                        // enclosing using's actual GameWindow.Dispose retry, then success-only publication.
                        scope.Host.Target.Dispose();
                        try
                        {
                            using (GameWindow game = scope.Game)
                            {
                                drainFailure = Assert.ThrowsExactly<InvalidOperationException>(() => ToolboxGameHostControl.DrainSessionOnOwner(game));
                            }
                            SetField(host, "ownerResourcesReleased", true);
                            Assert.Fail("A failed map release cannot make the enclosing game disposal succeed.");
                        }
                        catch (InvalidOperationException error)
                        {
                            retryFailure = error;
                            terminated.SetException(new AggregateException(drainFailure, error));
                        }

                        Assert.AreSame(drainFailure, retryFailure);
                        Assert.AreSame(expected, retryFailure);
                        Assert.AreEqual(1, environment.ReleaseCount);
                        Assert.IsFalse(scope.Session.IsDisposed);
                        Assert.AreSame(map, scope.Game.ContentArea);
                        Assert.DoesNotContain(map, components);
                        Assert.IsFalse(scope.Host.DispatchControl.IsDisposed);
                        Assert.IsFalse(host.IsStoppedSafely);
                        _ = Assert.ThrowsExactly<InvalidOperationException>(host.Dispose);
                    }
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                AggregateException stopFailure = await Assert.ThrowsExactlyAsync<AggregateException>(
                    () => host.StopAsync().WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                Assert.Contains(expected, stopFailure.Flatten().InnerExceptions);
                Assert.IsFalse(host.IsStoppedSafely);
                _ = Assert.ThrowsExactly<InvalidOperationException>(host.Dispose);
            }
            finally
            {
                try
                {
                    InvalidOperationException finalFailure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask()).ConfigureAwait(false);
                    Assert.AreSame(expected, finalFailure);
                    Assert.AreSame(drainFailure, finalFailure);
                    Assert.AreEqual(1, environment.ReleaseCount);
                }
                finally
                {
                    try
                    {
                        pendingEnvironment?.Dispose();
                    }
                    catch (InvalidOperationException failure) when (ReferenceEquals(failure, expected))
                    {
                        // An untransferred fixture must not replace an earlier setup/assertion failure.
                        Assert.AreEqual(1, pendingEnvironment.ReleaseCount);
                    }
                }
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenRenderLoopUnwindsThenFallbackDrainsAndReleasesOnOwner(bool failJob)
        {
            TaskCompletionSource<GameWindow> pumping = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource releaseJob = Completion();
            TaskCompletionSource cleanupEntered = Completion();
            TaskCompletionSource releaseCleanup = Completion();
            TaskCompletionSource completed = Completion();
            TaskCompletionSource terminated = Completion();
            ToolboxGameHostControl host = CreateShutdownHost(pumping, terminated);
            Task stop = host.StopAsync();
            InvalidOperationException runFailure = new InvalidOperationException("Render loop failed.");
            InvalidOperationException jobFailure = new InvalidOperationException("Drained job failed.");
            int ownerThread = 0;
            int cleanupThread = 0;
            int editorReleasedOn = 0;
            int mapReleasedOn = 0;
            int transportReleasedOn = 0;
            bool editorReleasedBeforeMap = false;
            bool transportAliveDuringRelease = false;
            Exception reportedDrainFailure = null;

            Task ownerWork = new Task(() =>
            {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, true);
                using (GameThreadControl transport = new GameThreadControl())
                {
                    using (Form renderForm = new Form())
                    {
                        ownerThread = Environment.CurrentManagedThreadId;
                        _ = transport.Handle;
                        _ = renderForm.Handle;
                        Assert.IsNull(transport.Parent);
                        transport.Disposed += (_, _) => transportReleasedOn = Environment.CurrentManagedThreadId;
                        GameWindow game = CreateSessionGame(renderForm, transport);
                        using (ToolboxSession session = new ToolboxSession(game))
                        {
                            SetField(game, "session", session);
                            SetField(game, "hostedPointerGesture", new HostedPointerGestureState());

                            GameComponentCollection components = new GameComponentCollection();
                            typeof(Game).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                                .Single(field => field.FieldType == typeof(GameComponentCollection)).SetValue(game, components);

                            // Construct the component normally without allocating rendering resources.
                            using (Game probeGame = new Game())
                            {
                                using (CleanupProbeMap map = new CleanupProbeMap(probeGame))
                                {
                                    map.OnRelease = () =>
                                    {
                                        mapReleasedOn = Environment.CurrentManagedThreadId;
                                        editorReleasedBeforeMap = game.Session.ExistingPathEditor == null && editorReleasedOn == ownerThread;
                                        transportAliveDuringRelease = !transport.IsDisposed && transport.IsHandleCreated;
                                    };
                                    components.Add(map);
                                    SetField(game, "contentArea", map);
                                    SetField(game.Session, "pathEditor", CreateEditor());
                                    session.OnPathEditorChanged += (_, _) => editorReleasedOn = Environment.CurrentManagedThreadId;
                                    SetField(game, "hostedTitleUpdateCallback", (Action<string>)(_ => Assert.Fail("Dead render window must not receive title updates.")));

                                    Task job = game.Session.RunAsync(async () =>
                                    {
                                        try
                                        {
                                            // Capture the original WinForms context before its render loop unwinds.
                                            _ = Assert.IsInstanceOfType<WindowsFormsSynchronizationContext>(SynchronizationContext.Current);
                                            await releaseJob.Task.ConfigureAwait(true);
                                            Assert.AreEqual(ownerThread, Environment.CurrentManagedThreadId);
                                        }
                                        finally
                                        {
                                            await game.InvokeOnGameThreadAsync(async () =>
                                            {
                                                cleanupThread = Environment.CurrentManagedThreadId;
                                                cleanupEntered.SetResult();
                                                await releaseCleanup.Task.ConfigureAwait(true);
                                                Assert.AreEqual(ownerThread, Environment.CurrentManagedThreadId);
                                            }).ConfigureAwait(false);
                                        }
                                    });
                                    GameWindow.ObserveGameThreadOperation(job);
                                    renderForm.Shown += (_, _) => transport.BeginInvoke((Action)(() => throw runFailure));
                                    InvalidOperationException actualRunFailure = Assert.ThrowsExactly<InvalidOperationException>(() => Application.Run(renderForm));
                                    Assert.AreSame(runFailure, actualRunFailure);
                                    Assert.IsTrue(renderForm.IsDisposed, "Application.Run(Form) must destroy its render form on unwind.");
                                    Assert.IsTrue(transport.IsHandleCreated && !transport.IsDisposed);
                                    transport.BeginInvoke((Action)(() => pumping.SetResult(game)));
                                    try
                                    {
                                        ToolboxGameHostControl.DrainSessionOnOwner(game);
                                    }
                                    catch (InvalidOperationException ex) when (ReferenceEquals(ex, jobFailure))
                                    {
                                        reportedDrainFailure = ex;
                                    }
                                    Assert.IsTrue(game.Session.IsDisposed);
                                    Assert.IsNull(game.ContentArea);
                                    Assert.IsEmpty(components);
                                    Assert.IsFalse(transport.IsDisposed, "Transport must outlive session resource disposal.");
                                }
                            }
                            // The production completion signal is published only after all owner disposal has returned.
                            SetField(host, "ownerResourcesReleased", true);
                            terminated.SetException(reportedDrainFailure == null ? runFailure : new AggregateException(runFailure, reportedDrainFailure));
                        }
                    }
                }
            });
            Thread owner = new Thread(() =>
            {
                ownerWork.RunSynchronously(TaskScheduler.Default);
                if (ownerWork.Exception is AggregateException failure)
                {
                    terminated.TrySetException(failure.InnerException);
                    pumping.TrySetException(failure.InnerException);
                    completed.TrySetException(failure.InnerException);
                }
                else
                {
                    completed.SetResult();
                }
            })
            {
                IsBackground = true,
                Name = "Toolbox.Tests.FallbackOwner",
            };
            owner.SetApartmentState(ApartmentState.STA);
            owner.Start();
            try
            {
                GameWindow game = await pumping.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                Assert.IsTrue(game.Session.IsStopping);
                Assert.IsFalse(game.Session.IsDisposed);
                Assert.IsFalse(completed.Task.IsCompleted);
                Assert.IsFalse(host.IsStoppedSafely);
                _ = Assert.ThrowsExactly<InvalidOperationException>(game.Session.Dispose);
                if (failJob)
                    releaseJob.SetException(jobFailure);
                else
                    releaseJob.SetResult();
                await cleanupEntered.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                Assert.IsFalse(game.Session.IsDisposed);
                Assert.IsFalse(completed.Task.IsCompleted);
                releaseCleanup.SetResult();
                await completed.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreEqual((ownerThread, ownerThread, ownerThread, ownerThread),
                    (cleanupThread, editorReleasedOn, mapReleasedOn, transportReleasedOn));
                Assert.IsTrue(editorReleasedBeforeMap);
                Assert.IsTrue(transportAliveDuringRelease);
                Assert.AreSame(failJob ? jobFailure : null, reportedDrainFailure);
                Exception diagnostic = await Assert.ThrowsAsync<Exception>(() => stop.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                if (failJob)
                    Assert.AreSequenceEqual(new Exception[] { runFailure, jobFailure }, ((AggregateException)diagnostic).Flatten().InnerExceptions.ToArray(), Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
                else
                    Assert.AreSame(runFailure, diagnostic);
                Assert.IsTrue(host.IsStoppedSafely);
                Assert.AreSame(stop, host.StopAsync());
            }
            finally
            {
                releaseJob.TrySetResult();
                releaseCleanup.TrySetResult();
                await completed.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>Releases the failing fixture only if setup or owner cleanup never attempted its release.</summary>
        public void Dispose()
        {
            if (emergencyMapEnvironment == null || emergencyMapEnvironment.ReleaseCount != 0)
                return;

            try
            {
                emergencyMapEnvironment.Dispose();
            }
            catch (InvalidOperationException failure) when (ReferenceEquals(failure, expectedMapEnvironmentFailure))
            {
                Assert.AreSame(expectedMapEnvironmentFailure, failure);
                Assert.AreEqual("Map environment release failed.", failure.Message);
                Assert.AreEqual(1, emergencyMapEnvironment.ReleaseCount);
            }
        }

        private sealed class CleanupProbeMap : ContentArea
        {
            private bool released;

            internal Action OnRelease { get; set; }
            internal Action OnActivation { get; set; }

            internal CleanupProbeMap(Game game) :
                this(game, new ProbeSessionFactory())
            {
            }

            internal CleanupProbeMap(Game game, IMapHostSessionFactory factory) :
                base(game, (ToolboxContent)RuntimeHelpers.GetUninitializedObject(typeof(ToolboxContent)),
                    null, factory)
            {
            }

            protected override void OnEnabledChanged(object sender, EventArgs args)
            {
                if (Enabled)
                    OnActivation?.Invoke();
            }

            protected override void Dispose(bool disposing)
            {
                try
                {
                    if (disposing && !released)
                    {
                        released = true;
                        OnRelease?.Invoke();
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
        }

        private sealed class FailingMapEnvironment : IDisposable
        {
            private readonly Exception failure;

            /// <summary>Number of real XnaMapHostResources environment-release attempts.</summary>
            public int ReleaseCount { get; private set; }

            /// <summary>Checks owner affinity and transport lifetime during resource release.</summary>
            internal Action OnRelease { get; set; }

            public FailingMapEnvironment(Exception failure)
            {
                this.failure = failure;
            }

            public void Dispose()
            {
                ReleaseCount++;
                OnRelease?.Invoke();
                if (failure != null)
                    throw failure;
            }
        }

        private sealed class ResourceSessionFactory : IMapHostSessionFactory, IMapHostAdapterBundle
        {
            public IMapViewStateAdapter ViewStateAdapter => null;
            public IMapRenderAdapter RenderAdapter => null;
            public IMapInteractionAdapter InteractionAdapter { get; } = new ProbeInteraction();
            public IMapOverlayShapeAdapter OverlayShapeAdapter => null;
            public IMapHostResources HostResources { get; }

            public ResourceSessionFactory(ref FailingMapEnvironment environment)
            {
                HostResources = new XnaMapHostResources(null, null, null, environment);
                environment = null;
            }

            public IMapHostSession Create(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent)
                => new XnaMapHostSession(this);
        }

        private sealed class ProbeSessionFactory : IMapHostSessionFactory
        {
            public IMapHostSession Create(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent)
                => new ProbeSession();
        }

        private sealed class ProbeSession : IMapHostSession
        {
            public IMapViewStateAdapter ViewStateAdapter => null;
            public IMapRenderAdapter RenderAdapter => null;
            public IMapInteractionAdapter InteractionAdapter { get; } = new ProbeInteraction();
            public IMapOverlayShapeAdapter OverlayShapeAdapter => null;

            public void Dispose()
            {
            }
        }

        // Only component construction and disposal are supported by this probe.
        private sealed class ProbeInteraction : IMapInteractionAdapter
        {
            /// <summary>Injects an error after the host session has been acquired but before construction returns.</summary>
            internal Exception AttachFailure { get; set; }

            public void RegisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler) { }
            public void UnregisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler) { }
            public void AttachClientSizeChanged(EventHandler handler)
            {
                if (AttachFailure != null)
                    throw AttachFailure;
            }
            public void SyncViewport(in Rectangle bounds) => throw new NotSupportedException();
            public void SyncViewportForContent(ContentBase content) => throw new NotSupportedException();
            public void ResetSize(in Point windowSize, int screenDelta) => throw new NotSupportedException();
            public void PresetPosition(in PointD centerPoint, double scale) => throw new NotSupportedException();
            public void SetTrackingPosition(in WorldLocation location) => throw new NotSupportedException();
            public void SetTrackingPosition(in PointD location) => throw new NotSupportedException();
            public void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight) => throw new NotSupportedException();
            public void UpdateScaleAt(in Point scaleAt, int steps) => throw new NotSupportedException();
            public void UpdateScale(int steps) => throw new NotSupportedException();
            public void UpdateScaleAbsolute(double scale) => throw new NotSupportedException();
            public void UpdatePosition(in Vector2 delta) => throw new NotSupportedException();
            public void MouseMove(bool enabled, in Point position, ContentBase content) => throw new NotSupportedException();
            public void MouseDragging(UserCommandArgs userCommandArgs) => throw new NotSupportedException();
            public void MouseWheelAt(UserCommandArgs userCommandArgs, KeyModifiers modifiers) => throw new NotSupportedException();
            public void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers) => throw new NotSupportedException();
            public void MoveByKeyLeft(UserCommandArgs commandArgs) => throw new NotSupportedException();
            public void MoveByKeyRight(UserCommandArgs commandArgs) => throw new NotSupportedException();
            public void MoveByKeyUp(UserCommandArgs commandArgs) => throw new NotSupportedException();
            public void MoveByKeyDown(UserCommandArgs commandArgs) => throw new NotSupportedException();
            public void ZoomIn(UserCommandArgs commandArgs) => throw new NotSupportedException();
            public void ZoomOut(UserCommandArgs commandArgs) => throw new NotSupportedException();
            public void UpdateFrameState(ref bool suppressDrawing) => throw new NotSupportedException();
            public void HandleEnabledChanged(bool enabled, ContentArea contentArea, IMapTextureHelperHost textureHelperHost, IMapBaseOverlayContext overlayContext)
                => throw new NotSupportedException();
            public void RefreshFonts(ref System.Drawing.Font currentFont) => throw new NotSupportedException();
            public void RefreshAfterReset(ContentBase content, in Point windowSize, int screenDelta, ref System.Drawing.Font currentFont)
                => throw new NotSupportedException();
            public void RefreshAfterScaleChange(ref System.Drawing.Font currentFont) => throw new NotSupportedException();
            public void RefreshAfterPreset(ref System.Drawing.Font currentFont, ref bool suppressDrawing) => throw new NotSupportedException();
        }
    }
}
