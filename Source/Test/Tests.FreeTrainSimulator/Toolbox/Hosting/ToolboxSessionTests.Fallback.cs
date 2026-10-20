using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Game = Microsoft.Xna.Framework.Game;
using GameComponentCollection = Microsoft.Xna.Framework.GameComponentCollection;
using GameTime = Microsoft.Xna.Framework.GameTime;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
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
                using (Form renderForm = new Form())
                {
                    ownerThread = Environment.CurrentManagedThreadId;
                    _ = transport.Handle;
                    _ = renderForm.Handle;
                    Assert.IsNull(transport.Parent);
                    transport.Disposed += (_, _) => transportReleasedOn = Environment.CurrentManagedThreadId;
                    GameWindow game = CreateSessionGame(renderForm, transport);
                    using GameWindow.ToolboxSession session = new GameWindow.ToolboxSession(game);
                    SetField(game, "session", session);
                    SetField(game, "hostedPointerGesture", new HostedPointerGestureState());

                    GameComponentCollection components = new GameComponentCollection();
                    typeof(Game).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                        .Single(field => field.FieldType == typeof(GameComponentCollection)).SetValue(game, components);

                    // Construct the component normally without allocating rendering resources.
                    using Game probeGame = new Game();
                    using CleanupProbeMap map = new CleanupProbeMap(probeGame);
                    map.OnRelease = () =>
                    {
                        mapReleasedOn = Environment.CurrentManagedThreadId;
                        editorReleasedBeforeMap = game.Session.ExistingPathEditor == null && editorReleasedOn == ownerThread;
                        transportAliveDuringRelease = !transport.IsDisposed && transport.IsHandleCreated;
                    };
                    components.Add(map);
                    SetField(game, "contentArea", map);
                    SetField(game.Session, "pathEditor", CreateEditor());
                    game.OnPathEditorChanged += (_, _) => editorReleasedOn = Environment.CurrentManagedThreadId;
                    SetField(game, "hostedTitleUpdateCallback", (Action<string>)(_ => Assert.Fail("Dead render window must not receive title updates.")));

                    Task job = game.Session.RunAsync(async () =>
                    {
                        try
                        {
                            await releaseJob.Task.ConfigureAwait(false);
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
                    Assert.AreEqual(0, components.Count);
                    Assert.IsFalse(transport.IsDisposed, "Transport must outlive session resource disposal.");
                }
            });

            Thread owner = new Thread(() =>
            {
                ownerWork.RunSynchronously(TaskScheduler.Default);
                if (ownerWork.Exception is AggregateException failure)
                {
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
                Assert.ThrowsExactly<InvalidOperationException>(game.Session.Dispose);
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
            }
            finally
            {
                releaseJob.TrySetResult();
                releaseCleanup.TrySetResult();
                await completed.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }

        private sealed class CleanupProbeMap : ContentArea
        {
            private bool released;

            internal Action OnRelease { get; set; }

            internal CleanupProbeMap(Game game) :
                base(game, (ToolboxContent)RuntimeHelpers.GetUninitializedObject(typeof(ToolboxContent)),
                    null, new ProbeSessionFactory())
            {
            }

            protected override void OnEnabledChanged(object sender, EventArgs args)
            {
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
            public void RegisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler) { }
            public void UnregisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler) { }
            public void AttachClientSizeChanged(EventHandler handler) { }
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
