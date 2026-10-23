using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.PopupWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using GameWindow = FreeTrainSimulator.Toolbox.GameWindow;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    [TestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public sealed class GameWindowDisposalTests
    {
        private static readonly string[] componentManagerReleases = { "component", "manager" };
        private static readonly string[] managerComponentTransportReleases = { "manager", "component", "transport" };
        private static readonly string[] failedComponentReleases = { "first", "later", "manager", "transport" };
        private static readonly string[] failedRendererReleases = { "component", "sentinel", "manager", "transport" };

        public TestContext TestContext { get; set; }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenBaseComponentsAreReleasedThenDispatchTransportRemainsAliveUntilAfterCleanup()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                bool transportAliveDuringCleanup = false;
                int cleanupThread = 0;
                await host.OnOwnerAsync(() =>
                {
                    GameWindow game = CreateAdapter(host, () =>
                    {
                        transportAliveDuringCleanup = host.DispatchControl.IsHandleCreated && !host.DispatchControl.IsDisposed;
                        cleanupThread = Environment.CurrentManagedThreadId;
                    });

                    game.Dispose();
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreEqual((true, host.OwnerThreadId, true),
                    (transportAliveDuringCleanup, cleanupThread, host.DispatchControl.IsDisposed));
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenBaseComponentCleanupFailsThenDispatchTransportIsReleasedAndFailureIsPreserved()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                InvalidOperationException expected = new InvalidOperationException("Base component cleanup failed.");
                InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                    host.OnOwnerAsync(() =>
                    {
                        GameWindow game = CreateAdapter(host, () => throw expected);
                        game.Dispose();
                    }).WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);

                Assert.AreEqual((expected, true), (actual, host.DispatchControl.IsDisposed));
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task NoncontinuableCleanupFailureSurvivesRetryWithoutReleasingTransportOrRepeatingCleanup()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    FatalException expected = new FatalException("Noncontinuable component cleanup.");
                    int attempts = 0;
                    GameWindow game = CreateAdapter(host, () =>
                    {
                        attempts++;
                        throw expected;
                    });

                    Assert.AreSame(expected, Assert.ThrowsExactly<FatalException>(game.Dispose));
                    Assert.AreSame(expected, Assert.ThrowsExactly<FatalException>(game.Dispose));

                    Assert.AreEqual(1, attempts);
                    Assert.IsFalse(host.DispatchControl.IsDisposed);
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task ComponentCleanupPrecedesGraphicsManagerDisposal()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    List<string> releases = new List<string>();
                    using (Game managerHost = new Game())
                    {
                        ProbeGraphicsDeviceManager manager = new ProbeGraphicsDeviceManager(managerHost, releases);
                        bool managerAliveDuringCleanup = false;
                        using (GameWindow game = CreateAdapter(host, () =>
                        {
                            managerAliveDuringCleanup = !manager.IsReleased;
                            releases.Add("component");
                        }))
                        {
                            // The adapter borrows the authoritative service; only MonoGame owns the manager.
                            SetField(game, typeof(Game), "_services", managerHost.Services);
                            SetField(game, typeof(Game), "_graphicsDeviceManager", manager);
                            SetField(game, typeof(GameWindow), "<GraphicsManager>k__BackingField", manager);
                            // Transfer before assertions so fixture cleanup never has two manager owners.
                            SetField(managerHost, typeof(Game), "_graphicsDeviceManager", null);
                            Assert.AreSame(manager, typeof(GameWindow).GetProperty("GraphicsManager", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game));
                            Assert.AreSame(manager, game.Services.GetService<IGraphicsDeviceManager>());

                            game.Dispose();
                            game.Dispose();

                            Assert.IsTrue(managerAliveDuringCleanup);
                            Assert.IsTrue(manager.IsReleased);
                            Assert.AreEqual(1, manager.ReleaseCount);
                            Assert.AreSequenceEqual(componentManagerReleases, releases);
                        }
                    }
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task FailedComponentDoesNotSkipLaterComponentManagerOrTransportAndSurvivesRetry()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    List<string> releases = new List<string>();
                    InvalidOperationException expected = new InvalidOperationException("First component release failed.");
                    using (Game managerHost = new Game())
                    {
                        ProbeGraphicsDeviceManager manager = new ProbeGraphicsDeviceManager(managerHost, releases);
                        GameWindow game = CreateAdapter(host, () =>
                        {
                            Assert.IsFalse(manager.IsReleased);
                            releases.Add("first");
                            throw expected;
                        });
                        game.Components.Add(new CleanupProbe(() =>
                        {
                            Assert.IsFalse(manager.IsReleased);
                            releases.Add("later");
                        }));
                        GameComponentCollection components = game.Components;
                        SetField(game, typeof(Game), "_services", managerHost.Services);
                        SetField(game, typeof(Game), "_graphicsDeviceManager", manager);
                        SetField(game, typeof(GameWindow), "<GraphicsManager>k__BackingField", manager);
                        SetField(managerHost, typeof(Game), "_graphicsDeviceManager", null);
                        host.DispatchControl.Disposed += (_, _) => releases.Add("transport");

                        Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(game.Dispose));
                        Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(game.Dispose));

                        Assert.AreSequenceEqual(failedComponentReleases, releases);
                        Assert.AreEqual(1, manager.ReleaseCount);
                        Assert.IsEmpty(components);
                    }
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task SharedRendererFailureSurvivesGameRetryAndStillReleasesManagerBeforeTransport()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    List<string> releases = new List<string>();
                    InvalidOperationException expected = new InvalidOperationException("Shared renderer release failed.");
                    using (Game managerHost = new Game())
                    {
                        ProbeGraphicsDeviceManager manager = new ProbeGraphicsDeviceManager(managerHost, releases);
                        using Texture2D sentinel = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
                        int sentinelReleases = 0;
                        void FailSentinel(object sender, EventArgs args)
                        {
                            sentinelReleases++;
                            releases.Add("sentinel");
                            throw expected;
                        }
                        sentinel.Disposing += FailSentinel;
                        try
                        {
                            GameWindow game = CreateAdapter(host, () =>
                            {
                                Assert.AreEqual(0, sentinelReleases);
                                releases.Add("component");
                            });
                            SetField(game, typeof(Game), "_services", managerHost.Services);
                            SetField(game, typeof(Game), "_graphicsDeviceManager", manager);
                            SetField(game, typeof(GameWindow), "<GraphicsManager>k__BackingField", manager);
                            SetField(managerHost, typeof(Game), "_graphicsDeviceManager", null);
                            TextTextureRenderer renderer = (TextTextureRenderer)RuntimeHelpers.GetUninitializedObject(typeof(TextTextureRenderer));
                            SetField(renderer, typeof(TextTextureRenderer), "<EmptyTexture>k__BackingField", sentinel);
                            SetField(renderer, typeof(TextTextureRenderer), "measureGraphicsHolder",
                                new ConcurrentQueue<(System.Drawing.Graphics, System.Drawing.StringFormat)>());
                            SetField(renderer, typeof(TextTextureRenderer), "whiteBrushHolder", new ConcurrentQueue<System.Drawing.Brush>());
                            game.Services.AddService(renderer);
                            Assert.AreSame(renderer, TextTextureRenderer.Instance(game));
                            host.DispatchControl.Disposed += (_, _) => releases.Add("transport");

                            Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(game.Dispose));
                            Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(game.Dispose));

                            Assert.AreSequenceEqual(failedRendererReleases, releases);
                            Assert.AreEqual((1, 1), (sentinelReleases, manager.ReleaseCount));
                        }
                        finally
                        {
                            sentinel.Disposing -= FailSentinel;
                        }
                    }
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [DataRow("registration")]
        [DataRow("activation")]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task FailedAttachmentRemovesAndReleasesTheRegisteredCandidate(string failureStage)
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    using (System.Drawing.Font font = new System.Drawing.Font("Arial", 12))
                    using (GameWindow game = CreateAttachmentAdapter(host))
                    {
                        AttachmentSession session = new AttachmentSession(font, null);
                        using (ContentArea candidate = CreateCandidate(game, session))
                        {
                            InvalidOperationException expected = new InvalidOperationException(failureStage);
                            FailAttachmentAt(game, candidate, failureStage, expected);
                            int changes = 0;
                            game.OnContentAreaChanged += (_, _) => changes++;

                            InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(() => game.ContentArea = candidate);

                            Assert.AreSame(expected, actual);
                            Assert.IsNull(game.ContentArea);
                            Assert.DoesNotContain(candidate, game.Components);
                            Assert.AreEqual(0, changes);
                            Assert.AreEqual(1, session.ReleaseCount);
                            Assert.AreEqual((0, 0), (session.MouseHandlerCount, session.ResizeHandlerCount));
                            candidate.Dispose();
                            game.Dispose();
                            Assert.AreEqual(1, session.ReleaseCount);
                        }
                    }
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [DataRow("registration")]
        [DataRow("activation")]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task FailedAttachmentPreservesBothAttachmentAndCleanupErrors(string failureStage)
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    using (System.Drawing.Font font = new System.Drawing.Font("Arial", 12))
                    using (GameWindow game = CreateAttachmentAdapter(host))
                    {
                        InvalidOperationException attachmentError = new InvalidOperationException(failureStage);
                        InvalidOperationException cleanupError = new InvalidOperationException("Candidate cleanup failed.");
                        AttachmentSession session = new AttachmentSession(font, cleanupError);
                        ContentArea candidate = CreateCandidate(game, session);
                        {
                            FailAttachmentAt(game, candidate, failureStage, attachmentError);

                            AggregateException actual = Assert.ThrowsExactly<AggregateException>(() => game.ContentArea = candidate);

                            Assert.AreSequenceEqual(new Exception[] { attachmentError, cleanupError }, actual.InnerExceptions);
                            Assert.IsNull(game.ContentArea);
                            Assert.DoesNotContain(candidate, game.Components);
                            Assert.AreEqual((0, 0), (session.MouseHandlerCount, session.ResizeHandlerCount));
                            Assert.AreSame(cleanupError, Assert.ThrowsExactly<InvalidOperationException>(candidate.Dispose));
                            game.Dispose();
                            Assert.AreEqual(1, session.ReleaseCount);
                        }
                    }
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task PostEnableTitleFailureDetachesHelpersAndReleasesCandidateEvenWhenDisableFails(bool failDisable)
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    using (System.Drawing.Font font = new System.Drawing.Font("Arial", 12))
                    using (GameWindow game = CreateAttachmentAdapter(host))
                    {
                        InvalidOperationException titleFailure = new InvalidOperationException("title callback");
                        InvalidOperationException disableFailure = new InvalidOperationException("disable callback");
                        AttachmentSession session = new AttachmentSession(font, null);
                        using (ContentArea candidate = CreateCandidate(game, session))
                        {
                            // Actual helper Enable/Disable and overlay wiring, with no GPU acquisition/draw.
                            InsetComponent helper = (InsetComponent)RuntimeHelpers.GetUninitializedObject(typeof(InsetComponent));
                            SetField(candidate.Content, typeof(ContentBase), "<TextureHelperHost>k__BackingField",
                                new XnaMapTextureHelperHost(new[] { helper }));
                            SetField(game, typeof(GameWindow), "<Catalog>k__BackingField", new GetText.Catalog());
                            int changes = 0;
                            int titleCalls = 0;
                            game.OnContentAreaChanged += (_, _) => changes++;
                            candidate.EnabledChanged += (_, _) =>
                            {
                                if (candidate.Enabled)
                                {
                                    // Sizing was skipped using a disposed sentinel, but title publication is live.
                                    SetField(game, typeof(GameWindow), "windowForm", host.Target);
                                }
                                else if (failDisable)
                                {
                                    Assert.IsFalse(helper.Enabled);
                                    throw disableFailure;
                                }
                            };
                            SetField(game, typeof(GameWindow), "hostedTitleUpdateCallback", (Action<string>)(_ =>
                            {
                                titleCalls++;
                                Assert.IsTrue(candidate.Enabled && helper.Enabled && helper.Visible);
                                Assert.IsNotNull(typeof(TextureContentComponent).GetField("content", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(helper));
                                throw titleFailure;
                            }));

                            if (failDisable)
                            {
                                AggregateException failure = Assert.ThrowsExactly<AggregateException>(() => game.ContentArea = candidate);
                                Assert.AreSequenceEqual(new Exception[] { titleFailure, disableFailure }, failure.InnerExceptions);
                            }
                            else
                            {
                                Assert.AreSame(titleFailure, Assert.ThrowsExactly<InvalidOperationException>(() => game.ContentArea = candidate));
                            }

                            Assert.IsFalse(candidate.Enabled);
                            Assert.IsFalse(helper.Enabled || helper.Visible);
                            Assert.IsNull(typeof(TextureContentComponent).GetField("content", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(helper));
                            Assert.DoesNotContain(candidate, game.Components);
                            Assert.IsNull(game.ContentArea);
                            Assert.AreEqual((1, 0, 1, 0, 0), (titleCalls, changes, session.ReleaseCount, session.MouseHandlerCount, session.ResizeHandlerCount));
                            candidate.Dispose();
                            Assert.AreEqual(1, session.ReleaseCount);
                        }
                    }
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task RemovedWindowManagerFailureSurvivesGameDisposalRetryAndTransportRemainsLast()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    List<string> releases = new List<string>();
                    InvalidOperationException expected = new InvalidOperationException("manager cleanup");
                    GameWindow game = CreateAdapter(host, () =>
                    {
                        Assert.IsFalse(host.DispatchControl.IsDisposed);
                        releases.Add("component");
                    });
                    WindowManager<ToolboxWindowType> manager = (WindowManager<ToolboxWindowType>)RuntimeHelpers.GetUninitializedObject(typeof(WindowManager<ToolboxWindowType>));
                    SetField(manager, typeof(WindowManager<ToolboxWindowType>), "windows", new EnumArray<FormBase, ToolboxWindowType>());
                    SetField(manager, typeof(WindowManager<ToolboxWindowType>), "lazyWindows", new EnumArray<Lazy<FormBase>, ToolboxWindowType>());
                    SetField(manager, typeof(WindowManager), "<RoundedShadows>k__BackingField", new Dictionary<int, Microsoft.Xna.Framework.Graphics.Texture2D>());
                    SetField(manager, typeof(WindowManager), "removeUserCommandEvents", (Action)(() =>
                    {
                        releases.Add("manager");
                        throw expected;
                    }));
                    game.Components.Add(manager);
                    GameComponentCollection components = game.Components;
                    SetField(game, typeof(GameWindow), "windowManager", manager);
                    host.DispatchControl.Disposed += (_, _) => releases.Add("transport");

                    Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(game.Dispose));
                    Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(game.Dispose));
                    Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(manager.Dispose));
                    Assert.DoesNotContain(manager, components);
                    Assert.AreSequenceEqual(managerComponentTransportReleases, releases);
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task FailedAttachmentRetainsOriginalDisableRemovalAndReleaseFailures()
        {
            StaControlHost host = await StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                await host.OnOwnerAsync(() =>
                {
                    using (System.Drawing.Font font = new System.Drawing.Font("Arial", 12))
                    using (GameWindow game = CreateAttachmentAdapter(host))
                    {
                        InvalidOperationException attachmentFailure = new InvalidOperationException("activation");
                        InvalidOperationException disableFailure = new InvalidOperationException("disable");
                        InvalidOperationException removalFailure = new InvalidOperationException("removal");
                        InvalidOperationException releaseFailure = new InvalidOperationException("release");
                        AttachmentSession session = new AttachmentSession(font, releaseFailure);
                        ContentArea candidate = CreateCandidate(game, session);
                        FailAttachmentAt(game, candidate, "activation", attachmentFailure);
                        candidate.EnabledChanged += (_, _) =>
                        {
                            if (!candidate.Enabled)
                                throw disableFailure;
                        };
                        game.Components.ComponentRemoved += (_, args) =>
                        {
                            if (ReferenceEquals(args.GameComponent, candidate))
                                throw removalFailure;
                        };

                        AggregateException failure = Assert.ThrowsExactly<AggregateException>(() => game.ContentArea = candidate);

                        Assert.AreSequenceEqual(new Exception[] { attachmentFailure, disableFailure, removalFailure, releaseFailure }, failure.InnerExceptions);
                        Assert.IsFalse(candidate.Enabled);
                        Assert.DoesNotContain(candidate, game.Components);
                        Assert.IsNull(game.ContentArea);
                        Assert.AreEqual((1, 0, 0), (session.ReleaseCount, session.MouseHandlerCount, session.ResizeHandlerCount));
                        Assert.AreSame(releaseFailure, Assert.ThrowsExactly<InvalidOperationException>(candidate.Dispose));
                        Assert.AreEqual(1, session.ReleaseCount);
                    }
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }

        private static GameWindow CreateAttachmentAdapter(StaControlHost host)
        {
            GameWindow game = CreateAdapter(host, () => { });
            // Skip only render-window sizing: registration, enablement and rollback use the real setter.
            System.Windows.Forms.Form sizingSentinel = new System.Windows.Forms.Form();
            sizingSentinel.Dispose();
            SetField(game, typeof(GameWindow), "windowForm", sizingSentinel);
            return game;
        }

        private static ContentArea CreateCandidate(GameWindow game, AttachmentSession session)
        {
            ContentBase content = (ContentBase)RuntimeHelpers.GetUninitializedObject(typeof(ToolboxContent));
            ContentArea candidate = new ContentArea(game, content, null, session);
            Assert.AreEqual((1, 1), (session.MouseHandlerCount, session.ResizeHandlerCount));
            Assert.IsFalse(candidate.Enabled);
            Assert.DoesNotContain(candidate, game.Components);
            return candidate;
        }

        private static void FailAttachmentAt(GameWindow game, ContentArea candidate, string failureStage, Exception failure)
        {
            if (failureStage == "registration")
            {
                game.Components.ComponentAdded += (_, e) =>
                {
                    if (!ReferenceEquals(e.GameComponent, candidate))
                        return;
                    Assert.Contains(candidate, game.Components);
                    Assert.IsFalse(candidate.Enabled);
                    throw failure;
                };
            }
            else if (failureStage == "activation")
            {
                candidate.EnabledChanged += (_, _) =>
                {
                    if (!candidate.Enabled)
                        return;
                    Assert.Contains(candidate, game.Components);
                    Assert.IsTrue(candidate.Enabled);
                    throw failure;
                };
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(failureStage));
            }
        }

        private static void SetField(object target, Type declaringType, string name, object value)
        {
            declaringType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static GameWindow CreateAdapter(StaControlHost host, Action cleanup)
        {
            GameWindow game = (GameWindow)RuntimeHelpers.GetUninitializedObject(typeof(GameWindow));
            typeof(GameWindow).GetField("ownerControl", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, host.DispatchControl);
            typeof(GameWindow).GetField("hostedPointerGesture", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, new HostedPointerGestureState());
            GameComponentCollection components = new GameComponentCollection
            {
                new CleanupProbe(cleanup),
            };
            typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, components);
            return game;
        }

        // No GameWindow construction, device initialization, rendering or GPU release is exercised here.
        private sealed class ProbeGraphicsDeviceManager : GraphicsDeviceManager
        {
            private readonly List<string> releases;

            /// <summary>Whether the graphics-manager ownership slot has been released.</summary>
            public bool IsReleased { get; private set; }

            /// <summary>Number of managed manager-disposal calls, without a graphics device.</summary>
            public int ReleaseCount { get; private set; }

            public ProbeGraphicsDeviceManager(Game game, List<string> releases) : base(game)
            {
                this.releases = releases;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    ReleaseCount++;
                    IsReleased = true;
                    releases.Add("manager");
                }
                base.Dispose(disposing);
            }
        }

        private sealed class AttachmentSession : IMapHostSession, IMapHostSessionFactory, IMapHostEnvironment
        {
            private readonly Exception cleanupError;
            private MouseInputGameComponent.MouseMoveEvent mouseHandlers;
            private EventHandler resizeHandlers;

            public IMapViewStateAdapter ViewStateAdapter { get; }
            public IMapRenderAdapter RenderAdapter => null;
            public IMapInteractionAdapter InteractionAdapter { get; }
            public IMapOverlayShapeAdapter OverlayShapeAdapter => null;
            public Point ClientSize => new Point(800, 600);
            public Point PointerPosition => Point.Zero;

            /// <summary>Number of candidate-owned session cleanup attempts.</summary>
            public int ReleaseCount { get; private set; }

            /// <summary>Number of candidate mouse handlers retained by the host.</summary>
            public int MouseHandlerCount => mouseHandlers?.GetInvocationList().Length ?? 0;

            /// <summary>Number of candidate resize handlers retained by the host.</summary>
            public int ResizeHandlerCount => resizeHandlers?.GetInvocationList().Length ?? 0;

            public AttachmentSession(System.Drawing.Font font, Exception cleanupError)
            {
                this.cleanupError = cleanupError;
                MapViewController controller = new MapViewController(new MapViewportBounds(0, 0, 100, 100));
                MapViewStateAdapter viewState = new MapViewStateAdapter(controller, font);
                ViewStateAdapter = viewState;
                InteractionAdapter = new MapInteractionAdapter(new FontManagerInstance("Arial", System.Drawing.FontStyle.Regular, 1),
                    controller, this, viewState);
            }

            public event EventHandler ClientSizeChanged
            {
                add => resizeHandlers += value;
                remove => resizeHandlers -= value;
            }

            public IMapHostSession Create(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent) => this;

            public void RegisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler) => mouseHandlers += handler;

            public void UnregisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler) => mouseHandlers -= handler;

            public void Dispose()
            {
                ReleaseCount++;
                mouseHandlers = null;
                resizeHandlers = null;
                if (cleanupError != null)
                    throw cleanupError;
            }
        }

        private sealed class CleanupProbe : IGameComponent, IDisposable
        {
            private readonly Action cleanup;

            internal CleanupProbe(Action cleanup)
            {
                this.cleanup = cleanup;
            }

            public void Initialize()
            {
            }

            public void Dispose() => cleanup();
        }
    }
}
