using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Shaders;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Window.Controls;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Tests.FreeTrainSimulator.Graphics.Window
{
    [TestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public sealed class SharedWindowResourceOwnershipTests : IDisposable
    {
        private static readonly string[] managerFailureReleases = { "eager", "later", "unsubscribe", "texture", "white", "shadow", "spriteBatch" };
        private static readonly string[] lazyFailureReleases = { "eager", "lazy", "white" };
        private static readonly string[] sharedRendererReleases = { "shape text", "control text", "sentinel" };
        private static readonly string[] textureManagerReleases = { "cached", "missing" };
        private static readonly string[] cacheFailureReleases = { "first", "previous", "sweep" };
        private static readonly string[] cacheGenerations = { "currentResources", "previousResources", "sweepResources" };
        private Bitmap pendingRendererBitmap;

        private enum WindowKind
        {
            Eager,
            Alias,
            Lazy,
            Unused
        }

        [TestMethod]
        public void TextInputDisposalLeavesCachedCaretAliveUntilCacheDisposal()
        {
            using (ProbeTexture caret = new ProbeTexture("caret", new List<string>()))
            {
                using (Game game = new Game())
                {
                    using (Font font = new Font("Arial", 12))
                    {
                        using (TextTextureResourceHolder holder = CreateHolder(null))
                        {
                            int key = HashCode.Combine("_", font, (OutlineRenderOptions)null);
                            Resources(holder, "currentResources").TryAdd(key, caret.Texture);
                            game.Components.Add(holder);
                            using (WindowManager<WindowKind> manager = CreateManager())
                            {
                                SetField(manager, "<Game>k__BackingField", game);
                                SetField(manager, "<TextFontDefault>k__BackingField", font);
                                using (RecordingWindow window = RecordingWindow.Create("window", new List<string>()))
                                {
                                    SetField(window, "<Owner>k__BackingField", manager);
                                    using (TextInput input = new TextInput(window, 100, 20, '_'))
                                    {

                                        input.Dispose();
                                        input.Dispose();

                                        Assert.AreEqual(0, caret.ReleaseCount);
                                        Assert.AreSame(caret.Texture, holder["_", font]);
                                        holder.Dispose();
                                        holder.Dispose();
                                        Assert.AreEqual(1, caret.ReleaseCount);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void ShadowLabelDisposalLeavesRoundedTextureAliveUntilManagerDisposal()
        {
            using (ProbeTexture shadow = new ProbeTexture("shadow", new List<string>()))
            {
                using (WindowManager<WindowKind> manager = CreateManager())
                {
                    Field<Dictionary<int, Texture2D>>(manager, "<RoundedShadows>k__BackingField").Add(20, shadow.Texture);
                    using (ShadowLabel label = (ShadowLabel)RuntimeHelpers.GetUninitializedObject(typeof(ShadowLabel)))
                    {
                        using (TextTextureResourceHolder holder = CreateHolder(null))
                        {
                            SetField(label, "resourceHolder", holder);

                            label.Dispose();
                            label.Dispose();

                            Assert.AreEqual(0, shadow.ReleaseCount);
                            Assert.AreSame(shadow.Texture, manager.RoundedShadows[20]);
                            manager.Dispose();
                            manager.Dispose();
                            Assert.AreEqual(1, shadow.ReleaseCount);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void ManagerDisposesUniqueStoredEagerAndCreatedLazyWindowsWithoutCreatingUnusedLazy()
        {
            List<string> releases = new List<string>();
            using RecordingWindow eager = RecordingWindow.Create("eager", releases);
            using RecordingWindow lazy = RecordingWindow.Create("lazy", releases);
            using WindowManager<WindowKind> manager = CreateManager();
            int unusedCreations = 0;
            manager[WindowKind.Eager] = eager;
            manager[WindowKind.Alias] = eager;
            manager.SetLazyWindows(WindowKind.Lazy, new Lazy<FormBase>(() => lazy));
            manager.SetLazyWindows(WindowKind.Unused, new Lazy<FormBase>(() =>
            {
                unusedCreations++;
                return RecordingWindow.Create("unused", releases);
            }));
            Assert.AreSame(lazy, manager[WindowKind.Lazy]);

            manager.Dispose();
            manager.Dispose();

            Assert.AreEqual(1, eager.ReleaseCount);
            Assert.AreEqual(1, lazy.ReleaseCount);
            Assert.AreEqual(1, lazy.InitializeCount);
            Assert.AreEqual(0, unusedCreations);
        }

        [TestMethod]
        public void ManagerFailuresDoNotSkipLaterWindowsOrBaseResourcesAndSurviveRetry()
        {
            List<string> releases = new List<string>();
            InvalidOperationException windowFailure = new InvalidOperationException("eager release");
            InvalidOperationException textureFailure = new InvalidOperationException("texture release");
            InvalidOperationException unsubscribeFailure = new InvalidOperationException("unsubscribe");
            using RecordingWindow eager = RecordingWindow.Create("eager", releases);
            using RecordingWindow later = RecordingWindow.Create("later", releases);
            using ProbeTexture texture = new ProbeTexture("texture", releases);
            using ProbeTexture white = new ProbeTexture("white", releases);
            using ProbeTexture shadow = new ProbeTexture("shadow", releases);
            using ProbeSpriteBatch batch = new ProbeSpriteBatch(releases);
            WindowManager<WindowKind> manager = CreateManager();
            try
            {
                eager.ReleaseFailure = windowFailure;
                texture.ReleaseFailure = textureFailure;
                manager[WindowKind.Eager] = eager;
                manager[WindowKind.Alias] = eager;
                manager[WindowKind.Lazy] = later;
                manager.SetLazyWindows(WindowKind.Lazy, new Lazy<FormBase>(() => later));
                // Realize an alias without initializing it again; it must still only be released once.
                Assert.AreSame(later, Field<EnumArray<Lazy<FormBase>, WindowKind>>(manager, "lazyWindows")[WindowKind.Lazy].Value);
                SetField(manager, "windowTexture", texture.Texture);
                SetField(manager, "<WhiteTexture>k__BackingField", white.Texture);
                SetField(manager, "spriteBatch", batch.SpriteBatch);
                Field<Dictionary<int, Texture2D>>(manager, "<RoundedShadows>k__BackingField").Add(1, shadow.Texture);
                SetField(manager, "removeUserCommandEvents", (Action)(() =>
                {
                    releases.Add("unsubscribe");
                    throw unsubscribeFailure;
                }));
                AggregateException failure = Assert.ThrowsExactly<AggregateException>(manager.Dispose);
                Assert.AreSame(failure, Assert.ThrowsExactly<AggregateException>(manager.Dispose));

                CollectionAssert.AreEqual(new Exception[] { windowFailure, unsubscribeFailure, textureFailure }, failure.Flatten().InnerExceptions);
                CollectionAssert.AreEqual(managerFailureReleases, releases);
                Assert.AreEqual((1, 1, 1, 1, 1, 1),
                    (eager.ReleaseCount, later.ReleaseCount, texture.ReleaseCount, white.ReleaseCount, shadow.ReleaseCount, batch.ReleaseCount));
            }
            finally
            {
                eager.ReleaseFailure = null;
                texture.ReleaseFailure = null;
                if (!Field<bool>(manager, "windowsDisposed"))
                {
                    SetField(manager, "removeUserCommandEvents", null);
                    manager.Dispose();
                }
            }
        }

        [TestMethod]
        public void ManagerReleasesCreatedLazyWhoseInitializeFailedAndRetainsReleaseFailureWithoutRealizingUnusedLazy()
        {
            List<string> releases = new List<string>();
            using RecordingWindow eager = RecordingWindow.Create("eager", releases);
            using RecordingWindow lazy = RecordingWindow.Create("lazy", releases);
            InvalidOperationException initializationFailure = new InvalidOperationException("initialize");
            InvalidOperationException releaseFailure = new InvalidOperationException("lazy release");
            using ProbeTexture white = new ProbeTexture("white", releases);
            WindowManager<WindowKind> manager = CreateManager();
            try
            {
                lazy.InitializeFailure = initializationFailure;
                lazy.ReleaseFailure = releaseFailure;
                SetField(manager, "<WhiteTexture>k__BackingField", white.Texture);
                int unusedCreations = 0;
                manager[WindowKind.Eager] = eager;
                manager[WindowKind.Alias] = eager;
                manager.SetLazyWindows(WindowKind.Lazy, new Lazy<FormBase>(() => lazy));
                manager.SetLazyWindows(WindowKind.Unused, new Lazy<FormBase>(() =>
                {
                    unusedCreations++;
                    return RecordingWindow.Create("unused", releases);
                }));
                Assert.AreSame(initializationFailure, Assert.ThrowsExactly<InvalidOperationException>(() => _ = manager[WindowKind.Lazy]));
                Assert.IsNull(Field<EnumArray<FormBase, WindowKind>>(manager, "windows")[WindowKind.Lazy]);

                Assert.AreSame(releaseFailure, Assert.ThrowsExactly<InvalidOperationException>(manager.Dispose));
                Assert.AreSame(releaseFailure, Assert.ThrowsExactly<InvalidOperationException>(manager.Dispose));

                Assert.AreEqual((1, 1, 1, 0), (eager.ReleaseCount, lazy.ReleaseCount, lazy.InitializeCount, unusedCreations));
                Assert.AreEqual(1, white.ReleaseCount);
                CollectionAssert.AreEqual(lazyFailureReleases, releases);
            }
            finally
            {
                lazy.ReleaseFailure = null;
                if (!Field<bool>(manager, "windowsDisposed"))
                    manager.Dispose();
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ManagerReleasesOwnedResourcesOnceAndBorrowsFontsAndShaders(bool scrollbarLoaded)
        {
            List<string> releases = new List<string>();
            using ProbeTexture window = new ProbeTexture("window", releases);
            using ProbeTexture white = new ProbeTexture("white", releases);
            using ProbeTexture background = new ProbeTexture("background", releases);
            using ProbeTexture scrollbar = scrollbarLoaded ? new ProbeTexture("scrollbar", releases) : null;
            using ProbeTexture shape = new ProbeTexture("shape", releases);
            using ProbeSpriteBatch batch = new ProbeSpriteBatch(releases);
            using BasicShapes shapes = (BasicShapes)Activator.CreateInstance(typeof(BasicShapes), true);
            Field<EnumArray<Texture2D, BasicTextureType>>(shapes, "basicTextures")[BasicTextureType.BlankPixel] = shape.Texture;
            using ProbePopupShader shader = new ProbePopupShader();
            using WindowManager<WindowKind> manager = CreateManager();
            using (Font font = new Font("Arial", 12))
            {
                SetField(manager, "windowTexture", window.Texture);
                SetField(manager, "<WhiteTexture>k__BackingField", white.Texture);
                SetField(manager, "<BackgroundTexture>k__BackingField", background.Texture);
                SetField(manager, "<ScrollbarTexture>k__BackingField", scrollbar?.Texture);
                SetField(manager, "<BasicShapes>k__BackingField", shapes);
                SetField(manager, "spriteBatch", batch.SpriteBatch);
                SetField(manager, "<TextFontDefault>k__BackingField", font);
                SetField(manager, "WindowShader", shader.Shader);

                manager.Dispose();
                manager.Dispose();

                Assert.AreEqual(1, window.ReleaseCount);
                Assert.AreEqual(1, white.ReleaseCount);
                Assert.AreEqual(1, background.ReleaseCount);
                Assert.AreEqual(scrollbarLoaded ? 1 : 0, scrollbar?.ReleaseCount ?? 0);
                Assert.AreEqual(1, shape.ReleaseCount);
                Assert.AreEqual(1, batch.ReleaseCount);
                Assert.AreEqual(0, shader.ReleaseCount);
                Assert.IsGreaterThan(0, font.Height);
            }
        }

        [TestMethod]
        public void ManagerCleanupDetachesItsWindowAndControllerHandlersButPreservesOtherSubscribers()
        {
            using (Game game = new Game())
            {
                using WindowManager<WindowKind> manager = CreateManager();
                SetField(manager, "gameWindow", game.Window);
                EventHandler<EventArgs> resize = (EventHandler<EventArgs>)Delegate.CreateDelegate(typeof(EventHandler<EventArgs>), manager,
                    typeof(WindowManager).GetMethod("Window_ClientSizeChanged", BindingFlags.Instance | BindingFlags.NonPublic));
                EventHandler<TextInputEventArgs> text = (EventHandler<TextInputEventArgs>)Delegate.CreateDelegate(typeof(EventHandler<TextInputEventArgs>), manager,
                    typeof(WindowManager).GetMethod("Window_TextInput", BindingFlags.Instance | BindingFlags.NonPublic));
                EventHandler<EventArgs> otherResize = (_, _) => { };
                EventHandler<TextInputEventArgs> otherText = (_, _) => { };
                game.Window.ClientSizeChanged += resize;
                game.Window.TextInput += text;
                game.Window.ClientSizeChanged += otherResize;
                game.Window.TextInput += otherText;
                UserCommandController<WindowKind> controller = new UserCommandController<WindowKind>();
                typeof(WindowManager).GetMethod("AddUserCommandEvents", BindingFlags.Instance | BindingFlags.NonPublic)
                    .MakeGenericMethod(typeof(WindowKind)).Invoke(manager, new object[] { controller });
                EnumArray<Action<UserCommandArgs, GameTime, KeyModifiers>, CommonUserCommand> commands =
                    Field<EnumArray<Action<UserCommandArgs, GameTime, KeyModifiers>, CommonUserCommand>>(controller, "commonUserCommandsArgs");
                Assert.IsNotNull(commands[CommonUserCommand.PointerMoved]);

                manager.Dispose();
                manager.Dispose();

                Assert.IsFalse(Field<Delegate>(game.Window, "ClientSizeChanged").GetInvocationList().Contains(resize));
                Assert.IsFalse(Field<Delegate>(game.Window, "TextInput").GetInvocationList().Contains(text));
                Assert.IsTrue(Field<Delegate>(game.Window, "ClientSizeChanged").GetInvocationList().Contains(otherResize));
                Assert.IsTrue(Field<Delegate>(game.Window, "TextInput").GetInvocationList().Contains(otherText));
                foreach (CommonUserCommand command in new[] { CommonUserCommand.PointerMoved, CommonUserCommand.PointerPressed,
                    CommonUserCommand.PointerDown, CommonUserCommand.PointerReleased, CommonUserCommand.PointerDragged, CommonUserCommand.VerticalScrollChanged })
                {
                    Assert.IsNull(commands[command]);
                }
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SharedTextCachesDoNotCacheOrSweepRendererEmptySentinel(bool textShape)
        {
            using ProbeTexture sentinel = new ProbeTexture("sentinel", new List<string>());
            using (TextTextureRenderer renderer = CreateRenderer(sentinel.Texture))
            {
                using ResourceGameComponent<Texture2D> cache = textShape ? CreateTextShape(renderer) : CreateHolder(renderer);
                for (int generation = 1; generation <= 4; generation++)
                {
                    Texture2D result = textShape
                        ? ((TextShape)cache).GetTextTexture(string.Empty, null, null)
                        : ((TextTextureResourceHolder)cache)[string.Empty, null, null];
                    Assert.AreSame(sentinel.Texture, result);
                    Assert.AreEqual(0, Resources(cache, "currentResources").Count);
                    cache.Update(new GameTime(TimeSpan.FromSeconds(generation * 31), TimeSpan.Zero));
                }

                cache.Dispose();
                cache.Dispose();

                Assert.AreEqual(0, sentinel.ReleaseCount);
                Assert.AreSame(sentinel.Texture, renderer.RenderText(string.Empty, null, null));
            }
            Assert.AreEqual(1, sentinel.ReleaseCount);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SharedTextCachesReleaseGeneratedTexturesOnSweepAndRepeatedShutdown(bool textShape)
        {
            List<string> releases = new List<string>();
            using ProbeTexture swept = new ProbeTexture("swept", releases);
            using ProbeTexture shutdown = new ProbeTexture("shutdown", releases);
            using ResourceGameComponent<Texture2D> cache = textShape ? CreateTextShape(null) : CreateHolder(null);
            Resources(cache, "currentResources").TryAdd(1, swept.Texture);
            cache.Update(new GameTime(TimeSpan.FromSeconds(31), TimeSpan.Zero));
            cache.Update(new GameTime(TimeSpan.FromSeconds(62), TimeSpan.Zero));
            Assert.AreEqual(1, swept.ReleaseCount);
            Resources(cache, "currentResources").TryAdd(2, shutdown.Texture);

            cache.Dispose();
            cache.Dispose();

            Assert.AreEqual(1, swept.ReleaseCount);
            Assert.AreEqual(1, shutdown.ReleaseCount);
            Assert.AreEqual(0, Resources(cache, "currentResources").Count);
            Assert.AreEqual(0, Resources(cache, "previousResources").Count);
            Assert.AreEqual(0, Resources(cache, "sweepResources").Count);
        }

        [TestMethod]
        public void GameShutdownReleasesSharedRendererAfterBorrowingCacheComponents()
        {
            List<string> releases = new List<string>();
            using ProbeTexture sentinel = new ProbeTexture("sentinel", releases);
            using ProbeTexture shapeTexture = new ProbeTexture("shape text", releases);
            using ProbeTexture controlTexture = new ProbeTexture("control text", releases);
            using (Game game = new Game())
            {
                using TextTextureRenderer renderer = CreateRenderer(sentinel.Texture);
                game.Services.AddService(renderer);
                TextShape shape = TextShape.Instance(game, null);
                TextTextureResourceHolder holder = TextTextureResourceHolder.Instance(game);
                Assert.AreSame(renderer, TextTextureRenderer.Instance(game));
                Resources(shape, "currentResources").TryAdd(1, shapeTexture.Texture);
                Resources(holder, "currentResources").TryAdd(2, controlTexture.Texture);
                renderer.Measure(string.Empty, null, null);
                SolidBrush brush = null;
                try
                {
                    brush = new SolidBrush(System.Drawing.Color.White);
                    Field<ConcurrentQueue<Brush>>(renderer, "whiteBrushHolder").Enqueue(brush);
                    brush = null;
                }
                finally
                {
                    brush?.Dispose();
                }

                game.Dispose();
                game.Dispose();
                renderer.Dispose();

                CollectionAssert.AreEqual(sharedRendererReleases, releases);
                Assert.AreEqual(1, sentinel.ReleaseCount);
                Assert.IsNull(game.Services.GetService<TextTextureRenderer>());
                Assert.AreEqual(0, Field<ConcurrentQueue<(System.Drawing.Graphics, StringFormat)>>(renderer, "measureGraphicsHolder").Count);
                Assert.AreEqual(0, Field<ConcurrentQueue<Brush>>(renderer, "whiteBrushHolder").Count);
            }
        }

        [TestMethod]
        public void DisposingSharedRendererDoesNotRemoveAReplacementService()
        {
            List<string> releases = new List<string>();
            using ProbeTexture originalSentinel = new ProbeTexture("original", releases);
            using ProbeTexture replacementSentinel = new ProbeTexture("replacement", releases);
            using (Game game = new Game())
            {
                using TextTextureRenderer original = CreateRenderer(originalSentinel.Texture);
                game.Services.AddService(original);
                TextTextureRenderer.Instance(game);
                game.Services.RemoveService(typeof(TextTextureRenderer));
                using TextTextureRenderer replacement = CreateRenderer(replacementSentinel.Texture);
                game.Services.AddService(replacement);
                TextTextureRenderer.Instance(game);

                original.Dispose();
                original.Dispose();

                Assert.AreSame(replacement, game.Services.GetService<TextTextureRenderer>());
                Assert.AreEqual(1, originalSentinel.ReleaseCount);
                Assert.AreEqual(0, replacementSentinel.ReleaseCount);
                game.Dispose();
                Assert.AreEqual(1, replacementSentinel.ReleaseCount);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TextControlDisposalDetachesRefreshAndWindowHandlersWithoutRemovingOtherSubscribers(bool initiallyVisible)
        {
            using (Game game = new Game())
            {
                using TextTextureResourceHolder holder = CreateHolder(null);
                game.Components.Add(holder);
                using WindowManager<WindowKind> manager = CreateManager();
                SetField(manager, "<Game>k__BackingField", game);
                using RecordingWindow window = RecordingWindow.Create("window", new List<string>());
                SetField(window, "<Owner>k__BackingField", manager);
                SetField(window, "<Visible>k__BackingField", initiallyVisible);
                int otherRefreshes = 0;
                int otherOpens = 0;
                int otherCloses = 0;
                EventHandler<EventArgs> otherRefresh = (_, _) => otherRefreshes++;
                EventHandler otherOpen = (_, _) => otherOpens++;
                EventHandler otherClose = (_, _) => otherCloses++;
                holder.Refresh += otherRefresh;
                window.OnWindowOpened += otherOpen;
                window.OnWindowClosed += otherClose;
                using SubscriptionProbeTextControl control = new SubscriptionProbeTextControl(window);
                Assert.IsTrue(Field<Delegate>(window, "OnWindowOpened").GetInvocationList().Any(handler => ReferenceEquals(handler.Target, control)));
                Assert.IsTrue(Field<Delegate>(window, "OnWindowClosed").GetInvocationList().Any(handler => ReferenceEquals(handler.Target, control)));
                holder.Update(new GameTime(TimeSpan.FromSeconds(31), TimeSpan.Zero));
                int initialRefreshes = initiallyVisible ? 1 : 0;
                Assert.AreEqual(initialRefreshes, control.InitializeCount);

                control.Dispose();
                control.Dispose();

                Assert.AreEqual(otherRefresh, Field<Delegate>(holder, "Refresh"));
                Assert.AreEqual(otherOpen, Field<Delegate>(window, "OnWindowOpened"));
                Assert.AreEqual(otherClose, Field<Delegate>(window, "OnWindowClosed"));
                // Raise the real publishers' delegates without creating a GPU-backed WindowManager.
                Field<EventHandler>(window, "OnWindowOpened").Invoke(window, EventArgs.Empty);
                holder.Update(new GameTime(TimeSpan.FromSeconds(62), TimeSpan.Zero));
                Field<EventHandler>(window, "OnWindowClosed").Invoke(window, EventArgs.Empty);
                Field<EventHandler>(window, "OnWindowOpened").Invoke(window, EventArgs.Empty);
                holder.Update(new GameTime(TimeSpan.FromSeconds(93), TimeSpan.Zero));

                Assert.AreEqual(initialRefreshes, control.InitializeCount);
                Assert.AreEqual((3, 2, 1), (otherRefreshes, otherOpens, otherCloses));
                Assert.AreEqual(otherRefresh, Field<Delegate>(holder, "Refresh"));
            }
        }

        [TestMethod]
        [DataRow("currentResources")]
        [DataRow("previousResources")]
        [DataRow("sweepResources")]
        public void TextureManagerReleasesMissingTextureOnceAfterCachedTexturesWithoutCachingTheFallback(string generation)
        {
            List<string> releases = new List<string>();
            using ProbeTexture missing = new ProbeTexture("missing", releases);
            using ProbeTexture cached = new ProbeTexture("cached", releases);
            using TextureManager manager = CreateTextureManager(missing.Texture);
            Assert.AreSame(missing.Texture, manager.GetTextureCached(null));
            Assert.AreSame(missing.Texture, manager.GetTextureCached(string.Empty));
            // An unsupported suffix takes the real missing-texture path without file or GPU acquisition.
            Assert.AreSame(missing.Texture, manager.GetTextureCached("missing.ownership-test"));
            foreach (string name in cacheGenerations)
                Assert.AreEqual(0, Resources(manager, name).Count);
            Resources(manager, generation).TryAdd(1, cached.Texture);
            EventHandler<EventArgs> verifyCleanup = (_, _) =>
            {
                Assert.AreEqual(1, cached.ReleaseCount);
                foreach (string name in cacheGenerations)
                    Assert.AreEqual(0, Resources(manager, name).Count);
            };
            missing.Texture.Disposing += verifyCleanup;
            try
            {
                manager.Dispose();
                manager.Dispose();

                CollectionAssert.AreEqual(textureManagerReleases, releases);
                Assert.AreEqual(1, cached.ReleaseCount);
                Assert.AreEqual(1, missing.ReleaseCount);
                Assert.IsNull(manager.MissingTexture);
            }
            finally
            {
                missing.Texture.Disposing -= verifyCleanup;
            }
        }

        [TestMethod]
        public void TextureManagerReleasesMissingTextureEvenWhenCachedTextureCleanupFails()
        {
            List<string> releases = new List<string>();
            using ProbeTexture missing = new ProbeTexture("missing", releases);
            using ProbeTexture cached = new ProbeTexture("cached", releases);
            InvalidOperationException expected = new InvalidOperationException("Cached texture cleanup failed.");
            TextureManager manager = CreateTextureManager(missing.Texture);
            try
            {
                cached.ReleaseFailure = expected;
                Resources(manager, "currentResources").TryAdd(1, cached.Texture);

                InvalidOperationException actual = Assert.ThrowsExactly<InvalidOperationException>(manager.Dispose);
                Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(manager.Dispose));

                Assert.AreSame(expected, actual);
                CollectionAssert.AreEqual(textureManagerReleases, releases);
                Assert.AreEqual(1, cached.ReleaseCount);
                Assert.AreEqual(1, missing.ReleaseCount);
                Assert.IsNull(manager.MissingTexture);
            }
            finally
            {
                cached.ReleaseFailure = null;
                if (!Field<bool>(manager, "resourcesDisposed"))
                    manager.Dispose();
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SharedCacheReleaseFailureDoesNotSkipOtherGenerationsAndSurvivesRetry(bool textShape)
        {
            List<string> releases = new List<string>();
            using ProbeTexture first = new ProbeTexture("first", releases);
            using ProbeTexture previous = new ProbeTexture("previous", releases);
            using ProbeTexture sweep = new ProbeTexture("sweep", releases);
            InvalidOperationException expected = new InvalidOperationException("cache release");
            ResourceGameComponent<Texture2D> cache = textShape ? CreateTextShape(null) : CreateHolder(null);
            try
            {
                first.ReleaseFailure = expected;
                Resources(cache, "currentResources").TryAdd(1, first.Texture);
                Resources(cache, "previousResources").TryAdd(2, previous.Texture);
                Resources(cache, "sweepResources").TryAdd(3, sweep.Texture);

                Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(cache.Dispose));
                Assert.AreSame(expected, Assert.ThrowsExactly<InvalidOperationException>(cache.Dispose));

                CollectionAssert.AreEqual(cacheFailureReleases, releases);
                Assert.AreEqual((1, 1, 1), (first.ReleaseCount, previous.ReleaseCount, sweep.ReleaseCount));
                foreach (string generation in cacheGenerations)
                    Assert.AreEqual(0, Resources(cache, generation).Count);
            }
            finally
            {
                first.ReleaseFailure = null;
                if (!Field<bool>(cache, "disposed"))
                    cache.Dispose();
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NoncontinuableCacheReleaseFailureSurvivesRetryWithoutReleasingLaterGenerations(bool textShape)
        {
            using ProbeTexture first = new ProbeTexture("first", new List<string>());
            using ProbeTexture later = new ProbeTexture("later", new List<string>());
            FatalException expected = new FatalException("Noncontinuable texture cleanup.");
            first.ReleaseFailure = expected;
            ResourceGameComponent<Texture2D> cache = textShape ? CreateTextShape(null) : CreateHolder(null);
            try
            {
                Resources(cache, "currentResources").TryAdd(1, first.Texture);
                Resources(cache, "previousResources").TryAdd(2, later.Texture);

                Assert.AreSame(expected, Assert.ThrowsExactly<FatalException>(cache.Dispose));
                Assert.AreSame(expected, Assert.ThrowsExactly<FatalException>(cache.Dispose));

                Assert.AreEqual((1, 0), (first.ReleaseCount, later.ReleaseCount));
            }
            finally
            {
                if (!Field<bool>(cache, "disposed"))
                    cache.Dispose();
            }
        }

        [TestMethod]
        public void TextureManagerRetainsCacheAndFallbackReleaseFailuresWithoutRetryingEither()
        {
            List<string> releases = new List<string>();
            using ProbeTexture cached = new ProbeTexture("cached", releases);
            using ProbeTexture missing = new ProbeTexture("missing", releases);
            InvalidOperationException cachedFailure = new InvalidOperationException("cached");
            InvalidOperationException missingFailure = new InvalidOperationException("missing");
            TextureManager manager = CreateTextureManager(missing.Texture);
            try
            {
                cached.ReleaseFailure = cachedFailure;
                missing.ReleaseFailure = missingFailure;
                Resources(manager, "currentResources").TryAdd(1, cached.Texture);

                AggregateException failure = Assert.ThrowsExactly<AggregateException>(manager.Dispose);
                Assert.AreSame(failure, Assert.ThrowsExactly<AggregateException>(manager.Dispose));
                CollectionAssert.AreEqual(new Exception[] { cachedFailure, missingFailure }, failure.InnerExceptions);
                CollectionAssert.AreEqual(textureManagerReleases, releases);
                Assert.AreEqual((1, 1), (cached.ReleaseCount, missing.ReleaseCount));
            }
            finally
            {
                cached.ReleaseFailure = null;
                missing.ReleaseFailure = null;
                if (!Field<bool>(manager, "resourcesDisposed"))
                    manager.Dispose();
            }
        }

        [TestMethod]
        [DataRow("currentResources")]
        [DataRow("previousResources")]
        public void HolderPreparationAndBorrowedIndexerKeepTextureOwnedByCache(string generation)
        {
            using ProbeTexture sentinel = new ProbeTexture("sentinel", new List<string>());
            using ProbeTexture texture = new ProbeTexture("text", new List<string>());
            using TextTextureRenderer renderer = CreateRenderer(sentinel.Texture);
            using TextTextureResourceHolder holder = CreateHolder(renderer);
            using Font font = new Font("Arial", 12);
            const string text = "cached text";
            int key = HashCode.Combine(text, font, (OutlineRenderOptions)null);
            Resources(holder, generation).TryAdd(key, texture.Texture);

            holder.PrepareResource(text, font);

            Assert.AreSame(texture.Texture, Resources(holder, "currentResources")[key]);
            Assert.AreEqual(0, Resources(holder, "previousResources").Count);
            Assert.AreSame(texture.Texture, holder[text, font]);
            holder.Update(new GameTime(TimeSpan.FromSeconds(31), TimeSpan.Zero));
            Assert.AreSame(texture.Texture, Resources(holder, "previousResources")[key]);
            Assert.AreSame(texture.Texture, holder[text, font]);
            holder.PrepareResource(text, font);
            Assert.AreSame(texture.Texture, Resources(holder, "currentResources")[key]);
            Assert.AreEqual(0, Resources(holder, "previousResources").Count);
            Assert.AreEqual(0, texture.ReleaseCount);

            holder.Dispose();
            holder.Dispose();

            Assert.AreEqual(1, texture.ReleaseCount);
            Assert.AreEqual(0, sentinel.ReleaseCount);
        }

        [TestMethod]
        [DataRow("currentResources")]
        [DataRow("previousResources")]
        public void OutlinedHolderPreparationAndBorrowedIndexerKeepTextureOwnedByCache(string generation)
        {
            using ProbeTexture sentinel = new ProbeTexture("sentinel", new List<string>());
            using ProbeTexture texture = new ProbeTexture("outlined text", new List<string>());
            using TextTextureRenderer renderer = CreateRenderer(sentinel.Texture);
            using TextTextureResourceHolder holder = CreateHolder(renderer);
            using Font font = new Font("Arial", 12);
            using OutlineRenderOptions outline = new OutlineRenderOptions(2, System.Drawing.Color.Black, System.Drawing.Color.White);
            const string text = "cached text";
            int key = HashCode.Combine(text, font, outline);
            Resources(holder, generation).TryAdd(key, texture.Texture);

            holder.PrepareResource(text, font, outline);

            Assert.AreSame(texture.Texture, Resources(holder, "currentResources")[key]);
            Assert.AreEqual(0, Resources(holder, "previousResources").Count);
            Assert.AreSame(texture.Texture, holder[text, font, outline]);
            holder.Update(new GameTime(TimeSpan.FromSeconds(31), TimeSpan.Zero));
            Assert.AreSame(texture.Texture, Resources(holder, "previousResources")[key]);
            Assert.AreSame(texture.Texture, holder[text, font, outline]);
            holder.PrepareResource(text, font, outline);
            Assert.AreSame(texture.Texture, Resources(holder, "currentResources")[key]);
            Assert.AreEqual(0, Resources(holder, "previousResources").Count);
            Assert.AreEqual(0, texture.ReleaseCount);

            holder.Dispose();
            holder.Dispose();

            Assert.AreEqual(1, texture.ReleaseCount);
            Assert.AreEqual(0, sentinel.ReleaseCount);
        }

        [TestMethod]
        public void HolderPreparationAndBorrowedIndexersKeepEmptySentinelOutOfTextureCache()
        {
            using ProbeTexture sentinel = new ProbeTexture("sentinel", new List<string>());
            using TextTextureRenderer renderer = CreateRenderer(sentinel.Texture);
            using TextTextureResourceHolder holder = CreateHolder(renderer);
            using Font font = new Font("Arial", 12);
            using OutlineRenderOptions outline = new OutlineRenderOptions(2, System.Drawing.Color.Black, System.Drawing.Color.White);

            holder.PrepareResource(string.Empty, font);
            holder.PrepareResource(string.Empty, font, outline);

            Assert.AreSame(sentinel.Texture, holder[string.Empty, font]);
            Assert.AreSame(sentinel.Texture, holder[string.Empty, font, outline]);
            foreach (string generation in cacheGenerations)
                Assert.AreEqual(0, Resources(holder, generation).Count);
            holder.Dispose();
            holder.Dispose();
            Assert.AreEqual(0, sentinel.ReleaseCount);
            renderer.Dispose();
            Assert.AreEqual(1, sentinel.ReleaseCount);
        }

        [TestMethod]
        public void HolderMeasurementMatchesRendererDimensionsWithoutPopulatingTextureCache()
        {
            using ProbeTexture sentinel = new ProbeTexture("sentinel", new List<string>());
            using TextTextureRenderer renderer = CreateRenderer(sentinel.Texture);
            using TextTextureResourceHolder holder = CreateHolder(renderer);
            using Font font = new Font("Arial", 12);
            using OutlineRenderOptions outline = new OutlineRenderOptions(2, System.Drawing.Color.Black, System.Drawing.Color.White);
            const string text = "measured text";
            System.Drawing.Size expected = renderer.Measure(text, font, null);
            System.Drawing.Size expectedOutlined = renderer.Measure(text, font, outline);

            Assert.AreEqual(expected, holder.Measure(text, font));
            Assert.AreEqual(expectedOutlined, holder.Measure(text, font, outline));
            Assert.IsGreaterThan(0, expected.Width);
            Assert.IsGreaterThan(0, expected.Height);
            foreach (string generation in cacheGenerations)
                Assert.AreEqual(0, Resources(holder, generation).Count);
            Assert.AreEqual(0, sentinel.ReleaseCount);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void HolderMeasurementForEmptyTextReturnsSentinelDimensionsWithoutPopulatingTextureCache(string text)
        {
            using ProbeTexture sentinel = new ProbeTexture("sentinel", new List<string>());
            SetField(sentinel.Texture, "width", 1);
            SetField(sentinel.Texture, "height", 1);
            using TextTextureRenderer renderer = CreateRenderer(sentinel.Texture);
            using TextTextureResourceHolder holder = CreateHolder(renderer);
            using Font font = new Font("Arial", 12);
            using OutlineRenderOptions outline = new OutlineRenderOptions(2, System.Drawing.Color.Black, System.Drawing.Color.White);

            Assert.AreEqual(new System.Drawing.Size(1, 1), holder.Measure(text, font));
            Assert.AreEqual(new System.Drawing.Size(1, 1), holder.Measure(text, font, outline));
            foreach (string generation in cacheGenerations)
                Assert.AreEqual(0, Resources(holder, generation).Count);
            Assert.AreEqual(0, sentinel.ReleaseCount);
        }

        [TestMethod]
        public void HolderMeasurementWithoutFontReturnsSentinelDimensionsWithoutPopulatingTextureCache()
        {
            using ProbeTexture sentinel = new ProbeTexture("sentinel", new List<string>());
            SetField(sentinel.Texture, "width", 1);
            SetField(sentinel.Texture, "height", 1);
            using TextTextureRenderer renderer = CreateRenderer(sentinel.Texture);
            using TextTextureResourceHolder holder = CreateHolder(renderer);
            using OutlineRenderOptions outline = new OutlineRenderOptions(2, System.Drawing.Color.Black, System.Drawing.Color.White);

            Assert.AreEqual(new System.Drawing.Size(1, 1), holder.Measure("text", null));
            Assert.AreEqual(new System.Drawing.Size(1, 1), holder.Measure("text", null, outline));
            foreach (string generation in cacheGenerations)
                Assert.AreEqual(0, Resources(holder, generation).Count);
            Assert.AreEqual(0, sentinel.ReleaseCount);
        }

        /// <summary>Releases renderer acquisition that was not transferred to a renderer owner.</summary>
        public void Dispose()
        {
            pendingRendererBitmap?.Dispose();
            pendingRendererBitmap = null;
        }

        // Bypass GPU acquisition only; all cache, component, host-event and owner-disposal paths are production code.
        // These fixtures do not establish actual device upload/release or HWND shutdown behavior.
        private static WindowManager<WindowKind> CreateManager()
        {
            WindowManager<WindowKind> manager = (WindowManager<WindowKind>)RuntimeHelpers.GetUninitializedObject(typeof(WindowManager<WindowKind>));
            SetField(manager, "windows", new EnumArray<FormBase, WindowKind>());
            SetField(manager, "lazyWindows", new EnumArray<Lazy<FormBase>, WindowKind>());
            SetField(manager, "<RoundedShadows>k__BackingField", new Dictionary<int, Texture2D>());
            return manager;
        }

        private TextTextureRenderer CreateRenderer(Texture2D sentinel)
        {
            TextTextureRenderer renderer = (TextTextureRenderer)RuntimeHelpers.GetUninitializedObject(typeof(TextTextureRenderer));
            SetField(renderer, "<EmptyTexture>k__BackingField", sentinel);
            SetField(renderer, "measureGraphicsHolder", new ConcurrentQueue<(System.Drawing.Graphics, StringFormat)>());
            SetField(renderer, "whiteBrushHolder", new ConcurrentQueue<Brush>());
            try
            {
                pendingRendererBitmap = new Bitmap(1, 1);
                SetField(renderer, "measureBitmap", pendingRendererBitmap);
                pendingRendererBitmap = null;
                return renderer;
            }
            finally
            {
                pendingRendererBitmap?.Dispose();
                pendingRendererBitmap = null;
            }
        }

        private static TextShape CreateTextShape(TextTextureRenderer renderer)
        {
            TextShape shape = (TextShape)RuntimeHelpers.GetUninitializedObject(typeof(TextShape));
            InitializeCache(shape, renderer);
            return shape;
        }

        private static TextTextureResourceHolder CreateHolder(TextTextureRenderer renderer)
        {
            TextTextureResourceHolder holder = (TextTextureResourceHolder)RuntimeHelpers.GetUninitializedObject(typeof(TextTextureResourceHolder));
            InitializeCache(holder, renderer);
            return holder;
        }

        private static void InitializeCache(ResourceGameComponent<Texture2D> cache, TextTextureRenderer renderer)
        {
            SetField(cache, "textRenderer", renderer);
            SetField(cache, "currentResources", new ConcurrentDictionary<int, Texture2D>());
            SetField(cache, "previousResources", new ConcurrentDictionary<int, Texture2D>());
            SetField(cache, "sweepResources", new ConcurrentDictionary<int, Texture2D>());
            SetField(cache, "disposableT", true);
            SetField(cache, "sweepInterval", TimeSpan.FromSeconds(30));
        }

        private static ConcurrentDictionary<int, Texture2D> Resources(object cache, string name)
        {
            return Field<ConcurrentDictionary<int, Texture2D>>(cache, name);
        }

        private static TextureManager CreateTextureManager(Texture2D missingTexture)
        {
            TextureManager manager = (TextureManager)RuntimeHelpers.GetUninitializedObject(typeof(TextureManager));
            SetField(manager, "<MissingTexture>k__BackingField", missingTexture);
            SetField(manager, "currentResources", new ConcurrentDictionary<int, Texture2D>());
            SetField(manager, "previousResources", new ConcurrentDictionary<int, Texture2D>());
            SetField(manager, "sweepResources", new ConcurrentDictionary<int, Texture2D>());
            SetField(manager, "disposableT", true);
            return manager;
        }

        private static T Field<T>(object target, string name)
        {
            return (T)FindField(target.GetType(), name).GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FindField(target.GetType(), name).SetValue(target, value);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
                type = type.BaseType;
            }
            throw new MissingFieldException(name);
        }

        private sealed class SubscriptionProbeTextControl : TextControl
        {
            /// <summary>Number of text initialization callbacks, without rendering or GPU upload.</summary>
            public int InitializeCount { get; private set; }

            public SubscriptionProbeTextControl(FormBase window) : base(window, 0, 0, 1, 1)
            {
            }

            protected override void InitializeText(string text)
            {
                InitializeCount++;
            }
        }

        private sealed class ProbeTexture : IDisposable
        {
            private readonly string name;
            private readonly List<string> releases;

            /// <summary>A real texture with no acquired device or native texture state.</summary>
            public Texture2D Texture { get; }

            /// <summary>Number of managed Disposing events, not native GPU releases.</summary>
            public int ReleaseCount { get; private set; }

            /// <summary>Failure injected after recording a managed disposal event.</summary>
            public Exception ReleaseFailure { get; set; }

            public ProbeTexture(string name, List<string> releases)
            {
                this.name = name;
                this.releases = releases;
                Texture = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
                // No native state was acquired; exercise explicit disposal only, including throwing events.
#pragma warning disable CA1816 // Intentional: uninitialized probe must not be finalized.
                GC.SuppressFinalize(Texture);
#pragma warning restore CA1816
                Texture.Disposing += Texture_Disposing;
            }

            private void Texture_Disposing(object sender, EventArgs e)
            {
                ReleaseCount++;
                releases.Add(name);
                if (ReleaseFailure != null)
                    throw ReleaseFailure;
            }

            /// <summary>Releases any remaining probe state without repeating its injected failure.</summary>
            public void Dispose()
            {
                Texture.Disposing -= Texture_Disposing;
                Texture.Dispose();
            }
        }

        private sealed class RecordingWindow : FormBase
        {
            private readonly string name;
            private readonly List<string> releases;

            /// <summary>Number of window cleanup attempts.</summary>
            public int ReleaseCount { get; private set; }
            /// <summary>Number of lazy window initialization attempts.</summary>
            public int InitializeCount { get; private set; }

            /// <summary>Failure injected after recording an initialization attempt.</summary>
            public Exception InitializeFailure { get; set; }

            /// <summary>Failure injected after recording a release attempt.</summary>
            public Exception ReleaseFailure { get; set; }

            private RecordingWindow(string name, List<string> releases) : base(CreateManager(), new GetText.Catalog())
            {
                this.name = name;
                this.releases = releases;
            }

            public static RecordingWindow Create(string name, List<string> releases)
            {
                return new RecordingWindow(name, releases);
            }

            protected internal override void Initialize()
            {
                InitializeCount++;
                if (InitializeFailure != null)
                    throw InitializeFailure;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    ReleaseCount++;
                    releases.Add(name);
                    if (ReleaseFailure != null)
                        throw ReleaseFailure;
                }
                base.Dispose(disposing);
            }
        }

        private sealed class ProbeSpriteBatch : IDisposable
        {
            private readonly List<string> releases;

            /// <summary>A real sprite batch with no acquired device or native state.</summary>
            public SpriteBatch SpriteBatch { get; }

            /// <summary>Number of managed Disposing events, not native GPU releases.</summary>
            public int ReleaseCount { get; private set; }

            public ProbeSpriteBatch(List<string> releases)
            {
                this.releases = releases;
                SpriteBatch = (SpriteBatch)RuntimeHelpers.GetUninitializedObject(typeof(SpriteBatch));
                SpriteBatch.Disposing += SpriteBatch_Disposing;
            }

            private void SpriteBatch_Disposing(object sender, EventArgs e)
            {
                ReleaseCount++;
                releases.Add("spriteBatch");
            }

            /// <summary>Releases any sprite batch state left by an interrupted ownership transfer.</summary>
            public void Dispose()
            {
                SpriteBatch.Disposing -= SpriteBatch_Disposing;
                SpriteBatch.Dispose();
            }
        }

        private sealed class ProbePopupShader : IDisposable
        {
            /// <summary>A real popup shader with no acquired device or native state.</summary>
            public PopupWindowShader Shader { get; }

            /// <summary>Number of managed disposal events caused by accidental borrower releases.</summary>
            public int ReleaseCount { get; private set; }

            public ProbePopupShader()
            {
                Shader = (PopupWindowShader)RuntimeHelpers.GetUninitializedObject(typeof(PopupWindowShader));
                Shader.Disposing += Shader_Disposing;
            }

            private void Shader_Disposing(object sender, EventArgs e)
            {
                ReleaseCount++;
            }

            /// <summary>Releases the independently owned shader after borrower assertions.</summary>
            public void Dispose()
            {
                Shader.Disposing -= Shader_Disposing;
                Shader.Dispose();
            }
        }
    }
}
