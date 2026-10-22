using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Tests.FreeTrainSimulator.Graphics.MapView
{
    [TestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public sealed class MapRenderingResourceOwnershipTests : IDisposable
    {
        private static readonly string[] expectedHostReleases = { "environment", "generated", "shape", "sentinel", "spriteBatch" };
        private static readonly string[] expectedLifetimeReleases = { "shape", "sentinel" };
        private RecordingEnvironment emergencyEnvironment;
        private Bitmap pendingRendererBitmap;

        [TestMethod]
        public void BasicShapesReleasesNormalAndHighlightTexturesOnce()
        {
            List<string> releases = new List<string>();
            using (ProbeTexture blank = new ProbeTexture("blank", releases, false))
            {
                using (ProbeTexture normal = new ProbeTexture("normal", releases, false))
                {
                    using (ProbeTexture highlight = new ProbeTexture("highlight", releases, false))
                    {
                        using (BasicShapes shapes = CreateShapes())
                        {
                            Textures(shapes, "basicTextures")[BasicTextureType.BlankPixel] = blank.Texture;
                            Textures(shapes, "basicTextures")[BasicTextureType.Signal] = normal.Texture;
                            Textures(shapes, "basicHighlightTextures")[BasicTextureType.Signal] = highlight.Texture;

                            shapes.Dispose();
                            shapes.Dispose();

                            Assert.AreEqual(1, blank.ReleaseCount);
                            Assert.AreEqual(1, normal.ReleaseCount);
                            Assert.AreEqual(1, highlight.ReleaseCount);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void PartialShapesReleaseRemainingTexturesEvenWhenOneReleaseFails()
        {
            List<string> releases = new List<string>();
            using (ProbeTexture blank = new ProbeTexture("blank", releases, true))
            {
                using (ProbeTexture highlight = new ProbeTexture("highlight", releases, false))
                {
                    BasicShapes shapes = CreateShapes();
                    try
                    {
                        Textures(shapes, "basicTextures")[BasicTextureType.BlankPixel] = blank.Texture;
                        Textures(shapes, "basicHighlightTextures")[BasicTextureType.Signal] = highlight.Texture;

                        AggregateException error = Assert.ThrowsExactly<AggregateException>(shapes.Dispose);
                        Assert.AreSame(error, Assert.ThrowsExactly<AggregateException>(shapes.Dispose));

                        Assert.AreEqual("blank", error.InnerException.Message);
                        Assert.AreEqual(1, blank.ReleaseCount);
                        Assert.AreEqual(1, highlight.ReleaseCount);
                    }
                    finally
                    {
                        if (!Field<bool>(shapes, "disposed"))
                            shapes.Dispose();
                    }
                }
            }
        }

        [TestMethod]
        public void TextCacheOwnsGeneratedTexturesButBorrowsEmptySentinel()
        {
            List<string> releases = new List<string>();
            using (ProbeTexture sentinel = new ProbeTexture("sentinel", releases, false))
            {
                using (ProbeTexture generated = new ProbeTexture("generated", releases, false))
                {
                    using (TextTextureRenderer renderer = CreateCpuRenderer(sentinel.Texture))
                    {
                        using MapTextTextureCache cache = new MapTextTextureCache(renderer);
                        Assert.AreSame(sentinel.Texture, cache.GetTextTexture(string.Empty, null, null));
                        Assert.AreSame(sentinel.Texture, cache.GetTextTexture(null, null, null));
                        // Seed an already-generated texture without creating/uploading a GPU texture.
                        Field<ConcurrentDictionary<int, Texture2D>>(cache, "textures").TryAdd(123, generated.Texture);

                        cache.Dispose();
                        cache.Dispose();

                        Assert.AreEqual(1, generated.ReleaseCount);
                        Assert.AreEqual(0, sentinel.ReleaseCount);
                        _ = Assert.ThrowsExactly<ObjectDisposedException>(() => cache.GetTextTexture(string.Empty, null, null));
                        Assert.AreSame(sentinel.Texture, renderer.RenderText(string.Empty, null, null));
                    }
                    Assert.AreEqual(1, sentinel.ReleaseCount);
                }
            }
        }

        [TestMethod]
        public void TextCacheContinuesOwnedCleanupAfterATextureReleaseFailure()
        {
            List<string> releases = new List<string>();
            using (ProbeTexture sentinel = new ProbeTexture("sentinel", releases, false))
            {
                using (ProbeTexture failing = new ProbeTexture("failing", releases, true))
                {
                    using (ProbeTexture remaining = new ProbeTexture("remaining", releases, false))
                    {
                        using (TextTextureRenderer renderer = CreateCpuRenderer(sentinel.Texture))
                        {
                            MapTextTextureCache pendingCache = null;
                            try
                            {
                                pendingCache = new MapTextTextureCache(renderer);
                                MapTextTextureCache cache = pendingCache;
                                ConcurrentDictionary<int, Texture2D> textures = Field<ConcurrentDictionary<int, Texture2D>>(cache, "textures");
                                textures.TryAdd(1, failing.Texture);
                                textures.TryAdd(2, remaining.Texture);
                                cache.GetTextTexture(string.Empty, null, null);

                                AggregateException error = Assert.ThrowsExactly<AggregateException>(() => DisposePendingCache(ref pendingCache));
                                Assert.AreSame(error, Assert.ThrowsExactly<AggregateException>(cache.Dispose));

                                Assert.AreEqual("failing", error.InnerException.Message);
                                Assert.AreEqual(1, failing.ReleaseCount);
                                Assert.AreEqual(1, remaining.ReleaseCount);
                                Assert.AreEqual(0, sentinel.ReleaseCount);
                                Assert.IsEmpty(textures);
                            }
                            finally
                            {
                                pendingCache?.Dispose();
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        public void HostOwnerReleasesEachPartiallyAcquiredResourceOnce(int acquired)
        {
            List<string> releases = new List<string>();
            using (ProbeSpriteBatch spriteBatch = acquired >= 1 ? new ProbeSpriteBatch(releases) : null)
            {
                using (ProbeTexture sentinel = acquired >= 2 ? new ProbeTexture("sentinel", releases, false) : null)
                {
                    using (TextTextureRenderer renderer = acquired >= 2 ? CreateCpuRenderer(sentinel.Texture) : null)
                    using (BasicShapes shapes = acquired >= 2 ? CreateShapes() : null)
                    {
                        using (XnaMapRenderingLifetime lifetime = acquired >= 2 ? CreateLifetime(null, renderer, shapes) : null)
                        {
                            using (MapTextTextureCache cache = acquired >= 3 ? new MapTextTextureCache(lifetime.GetTextTextureRenderer()) : null)
                            {
                                RecordingEnvironment pendingEnvironment = null;
                                try
                                {
                                    emergencyEnvironment = acquired >= 4 ? new RecordingEnvironment(releases, false) : null;
                                    pendingEnvironment = emergencyEnvironment;
                                    RecordingEnvironment environment = pendingEnvironment;
                                    using (XnaMapHostResources resources = CreateHostResources(spriteBatch?.SpriteBatch, lifetime, cache, ref pendingEnvironment))
                                    {
                                        _ = (cache?.GetTextTexture(string.Empty, null, null));

                                        resources.Dispose();
                                        resources.Dispose();
                                    }

                                    Assert.AreEqual(acquired >= 1 ? 1 : 0, spriteBatch?.ReleaseCount ?? 0);
                                    Assert.AreEqual(acquired >= 2 ? 1 : 0, sentinel?.ReleaseCount ?? 0);
                                    Assert.AreEqual(acquired >= 4 ? 1 : 0, environment?.ReleaseCount ?? 0);
                                    if (cache != null)
                                        _ = Assert.ThrowsExactly<ObjectDisposedException>(() => cache.GetTextTexture(string.Empty, null, null));
                                }
                                finally
                                {
                                    pendingEnvironment?.Dispose();
                                }
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void HostOwnerContinuesCleanupAfterEnvironmentReleaseFailure()
        {
            List<string> releases = new List<string>();
            using (ProbeTexture generated = new ProbeTexture("generated", releases, false))
            {
                using (ProbeTexture shape = new ProbeTexture("shape", releases, false))
                {
                    using (ProbeTexture sentinel = new ProbeTexture("sentinel", releases, false))
                    {
                        using (ProbeSpriteBatch spriteBatch = new ProbeSpriteBatch(releases))
                        {
                            using (BasicShapes shapes = CreateShapes())
                            {
                                Textures(shapes, "basicTextures")[BasicTextureType.BlankPixel] = shape.Texture;
                                using (TextTextureRenderer renderer = CreateCpuRenderer(sentinel.Texture))
                                {
                                    using (XnaMapRenderingLifetime lifetime = CreateLifetime(null, renderer, shapes))
                                    {
                                        using (MapTextTextureCache cache = new MapTextTextureCache(lifetime.GetTextTextureRenderer()))
                                        {
                                            Field<ConcurrentDictionary<int, Texture2D>>(cache, "textures").TryAdd(123, generated.Texture);
                                            cache.GetTextTexture(string.Empty, null, null);
                                            RecordingEnvironment pendingEnvironment = null;
                                            try
                                            {
                                                emergencyEnvironment = new RecordingEnvironment(releases, true);
                                                pendingEnvironment = emergencyEnvironment;
                                                XnaMapHostResources pendingResources = null;
                                                try
                                                {
                                                    pendingResources = CreateHostResources(spriteBatch.SpriteBatch, lifetime, cache, ref pendingEnvironment);
                                                    XnaMapHostResources resources = pendingResources;
                                                    AggregateException error = Assert.ThrowsExactly<AggregateException>(() => DisposePendingResources(ref pendingResources));
                                                    Assert.AreSame(error, Assert.ThrowsExactly<AggregateException>(resources.Dispose));

                                                    Assert.AreEqual("environment", error.InnerException.Message);
                                                    Assert.AreSequenceEqual(expectedHostReleases, releases);
                                                }
                                                finally
                                                {
                                                    pendingResources?.Dispose();
                                                }
                                            }
                                            finally
                                            {
                                                try
                                                {
                                                    pendingEnvironment?.Dispose();
                                                }
                                                catch (InvalidOperationException failure) when (failure.Message == "environment")
                                                {
                                                    Assert.AreEqual(1, pendingEnvironment.ReleaseCount);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void LifetimeRetainsBothShapeAndRendererFailuresWithoutRepeatingReleases()
        {
            List<string> releases = new List<string>();
            using (ProbeTexture shape = new ProbeTexture("shape", releases, true))
            {
                using (ProbeTexture sentinel = new ProbeTexture("sentinel", releases, true))
                {
                    BasicShapes shapes = CreateShapes();
                    try
                    {
                        Textures(shapes, "basicTextures")[BasicTextureType.BlankPixel] = shape.Texture;
                        TextTextureRenderer renderer = CreateCpuRenderer(sentinel.Texture);
                        try
                        {
                            using (Bitmap bitmap = Field<Bitmap>(renderer, "measureBitmap"))
                            {
                                XnaMapRenderingLifetime lifetime = CreateLifetime(null, renderer, shapes);
                                try
                                {
                                    AggregateException failure = Assert.ThrowsExactly<AggregateException>(lifetime.Dispose);
                                    Assert.AreSame(failure, Assert.ThrowsExactly<AggregateException>(lifetime.Dispose));
                                    AggregateException shapeFailure = Assert.ThrowsExactly<AggregateException>(shapes.Dispose);
                                    InvalidOperationException rendererFailure = Assert.ThrowsExactly<InvalidOperationException>(renderer.Dispose);
                                    Assert.AreSequenceEqual(new Exception[] { shapeFailure, rendererFailure }, failure.InnerExceptions);
                                    Assert.AreEqual("shape", shapeFailure.InnerException.Message);
                                    Assert.AreEqual("sentinel", rendererFailure.Message);
                                    Assert.AreSequenceEqual(new Exception[] { shapeFailure.InnerException, rendererFailure }, failure.Flatten().InnerExceptions, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
                                    Assert.ThrowsExactly<ArgumentException>(() => bitmap.GetPixel(0, 0));
                                    Assert.AreSequenceEqual(expectedLifetimeReleases, releases);
                                    Assert.AreEqual((1, 1), (shape.ReleaseCount, sentinel.ReleaseCount));
                                }
                                finally
                                {
                                    if (!Field<bool>(lifetime, "disposed"))
                                        lifetime.Dispose();
                                }
                            }
                        }
                        finally
                        {
                            if (!Field<bool>(renderer, "disposedValue"))
                                renderer.Dispose();
                        }
                    }
                    finally
                    {
                        if (!Field<bool>(shapes, "disposed"))
                            shapes.Dispose();
                    }
                }
            }
        }

        [TestMethod]
        public void PrivateMapLifetimeDoesNotReleaseBorrowedTextShapeRendererOrFont()
        {
            List<string> releases = new List<string>();
            using (ProbeTexture sharedSentinel = new ProbeTexture("shared sentinel", releases, false))
            {
                using (ProbeTexture privateSentinel = new ProbeTexture("private sentinel", releases, false))
                {
                    using (Game game = new Game())
                    {
                        using (Font font = new Font("Arial", 12))
                        {
                            using (TextTextureRenderer sharedRenderer = CreateCpuRenderer(sharedSentinel.Texture))
                            {
                                game.Services.AddService(sharedRenderer);
                                TextShape borrowedTextShape = TextShape.Instance(game, null);
                                using (ProbeTexture borrowedTexture = new ProbeTexture("borrowed text", releases, false))
                                {
                                    Field<ConcurrentDictionary<int, Texture2D>>(borrowedTextShape, "currentResources")
                                        .TryAdd(HashCode.Combine(font, "borrowed", (OutlineRenderOptions)null), borrowedTexture.Texture);
                                    using (TextTextureRenderer privateRenderer = CreateCpuRenderer(privateSentinel.Texture))
                                    {
                                        using (BasicShapes privateShapes = CreateShapes())
                                        {
                                            using (XnaMapRenderingLifetime lifetime = CreateLifetime(game, privateRenderer, privateShapes))
                                            {
                                                Assert.AreSame(borrowedTextShape, lifetime.GetTextShape(null));

                                                lifetime.Dispose();
                                                lifetime.Dispose();

                                                Assert.AreEqual(1, privateSentinel.ReleaseCount);
                                                Assert.AreEqual(0, sharedSentinel.ReleaseCount);
                                                Assert.AreEqual(0, borrowedTexture.ReleaseCount);
                                                Assert.AreSame(sharedRenderer, TextTextureRenderer.Instance(game));
                                                Assert.AreSame(borrowedTextShape, TextShape.Instance(game, null));
                                                Assert.AreSame(borrowedTexture.Texture, borrowedTextShape.GetTextTexture("borrowed", font, null));
                                                Assert.IsGreaterThan(0, font.Height);
                                                // Leave the host component collection and shared renderer with their original owners.
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void MeasurementReturnsItsLocalPoolEntryAfterOutlineFailure()
        {
            List<string> releases = new List<string>();
            using (ProbeTexture sentinel = new ProbeTexture("sentinel", releases, false))
            {
                using (TextTextureRenderer renderer = CreateCpuRenderer(sentinel.Texture))
                {
                    using (Font font = new Font("Arial", 12))
                    {
                        using (OutlineRenderOptions invalidOptions = new OutlineRenderOptions(2, System.Drawing.Color.Black, System.Drawing.Color.White))
                        {
                            Size expected = renderer.Measure("label", font, null);
                            invalidOptions.Dispose();
                            _ = Assert.ThrowsExactly<ArgumentException>(() => renderer.Measure("label", font, invalidOptions));

                            Assert.AreEqual(expected, renderer.Measure("label", font, null));
                            Assert.HasCount(1, Field<ConcurrentQueue<(System.Drawing.Graphics, StringFormat)>>(renderer, "measureGraphicsHolder"));
                            Assert.IsGreaterThan(0, font.Height);
                        }
                    }
                }
            }
        }

        /// <summary>Releases untransferred renderer state and an environment whose release was never attempted.</summary>
        public void Dispose()
        {
            pendingRendererBitmap?.Dispose();
            pendingRendererBitmap = null;
            if (emergencyEnvironment == null || emergencyEnvironment.ReleaseCount != 0)
                return;

            try
            {
                emergencyEnvironment.Dispose();
            }
            catch (InvalidOperationException failure) when (failure.Message == "environment")
            {
                Assert.AreEqual("environment", failure.Message);
                Assert.AreEqual(1, emergencyEnvironment.ReleaseCount);
            }
        }

        private static BasicShapes CreateShapes()
        {
            return (BasicShapes)Activator.CreateInstance(typeof(BasicShapes), true);
        }

        private static EnumArray<Texture2D, BasicTextureType> Textures(BasicShapes shapes, string name)
        {
            return Field<EnumArray<Texture2D, BasicTextureType>>(shapes, name);
        }

        private static XnaMapHostResources CreateHostResources(SpriteBatch spriteBatch, XnaMapRenderingLifetime lifetime,
            MapTextTextureCache cache, ref RecordingEnvironment environment)
        {
            XnaMapHostResources resources = new XnaMapHostResources(spriteBatch, lifetime, cache, environment);
            environment = null;
            return resources;
        }

        private static void DisposePendingCache(ref MapTextTextureCache cache)
        {
            MapTextTextureCache owner = cache;
            cache = null;
            owner?.Dispose();
        }

        private static void DisposePendingResources(ref XnaMapHostResources resources)
        {
            XnaMapHostResources owner = resources;
            resources = null;
            owner?.Dispose();
        }

        // These fixtures bypass native allocation only. Disposal and cache/lifetime code are the real implementations.
        // They cannot establish GraphicsDevice acquisition/upload or real GPU-release behavior.
        private TextTextureRenderer CreateCpuRenderer(Texture2D sentinel)
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

        private static XnaMapRenderingLifetime CreateLifetime(Game game, TextTextureRenderer renderer, BasicShapes shapes)
        {
            XnaMapRenderingLifetime lifetime = (XnaMapRenderingLifetime)RuntimeHelpers.GetUninitializedObject(typeof(XnaMapRenderingLifetime));
            SetField(lifetime, "game", game);
            SetField(lifetime, "textTextureRenderer", renderer);
            SetField(lifetime, "basicShapes", shapes);
            return lifetime;
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
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    return field;
                type = type.BaseType;
            }
            throw new MissingFieldException(name);
        }

        private sealed class ProbeTexture : IDisposable
        {
            private readonly string name;
            private readonly List<string> releases;
            private readonly bool failRelease;

            /// <summary>Number of owner release attempts, independent of native GPU state.</summary>
            public int ReleaseCount { get; private set; }
            /// <summary>Real texture whose disposal is exercised without native allocation.</summary>
            public Texture2D Texture { get; }

            /// <summary>Observes real texture disposal and optionally injects a managed release failure.</summary>
            public ProbeTexture(string name, List<string> releases, bool failRelease)
            {
                this.name = name;
                this.releases = releases;
                this.failRelease = failRelease;
                Texture = (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
                Texture.Disposing += OnDisposing;
            }

            private void OnDisposing(object sender, EventArgs args)
            {
                ReleaseCount++;
                releases.Add(name);
                if (failRelease)
                    throw new InvalidOperationException(name);
            }

            /// <summary>Removes injected failure before releasing any fixture-owned remainder.</summary>
            public void Dispose()
            {
                Texture.Disposing -= OnDisposing;
                Texture.Dispose();
            }
        }

        private sealed class ProbeSpriteBatch : IDisposable
        {
            private readonly List<string> releases;

            /// <summary>Number of owner release attempts, independent of native GPU state.</summary>
            public int ReleaseCount { get; private set; }
            /// <summary>Real sprite batch with no acquired effect or native buffers.</summary>
            public SpriteBatch SpriteBatch { get; }

            /// <summary>Observes real sprite-batch disposal without allocating rendering resources.</summary>
            public ProbeSpriteBatch(List<string> releases)
            {
                this.releases = releases;
                SpriteBatch = (SpriteBatch)RuntimeHelpers.GetUninitializedObject(typeof(SpriteBatch));
                SpriteBatch.Disposing += OnDisposing;
            }

            private void OnDisposing(object sender, EventArgs args)
            {
                ReleaseCount++;
                releases.Add("spriteBatch");
            }

            /// <summary>Releases the fixture if its map owner did not reach disposal.</summary>
            public void Dispose()
            {
                SpriteBatch.Disposing -= OnDisposing;
                SpriteBatch.Dispose();
            }
        }

        private sealed class RecordingEnvironment : IDisposable
        {
            private readonly List<string> releases;
            private readonly bool failRelease;

            /// <summary>Number of environment release attempts.</summary>
            public int ReleaseCount { get; private set; }

            public RecordingEnvironment(List<string> releases, bool failRelease)
            {
                this.releases = releases;
                this.failRelease = failRelease;
            }

            public void Dispose()
            {
                ReleaseCount++;
                releases.Add("environment");
                if (failRelease)
                    throw new InvalidOperationException("environment");
            }
        }
    }
}
