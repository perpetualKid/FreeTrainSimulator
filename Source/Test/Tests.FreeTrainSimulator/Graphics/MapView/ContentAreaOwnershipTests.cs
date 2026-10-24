using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.MapView;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Point = Microsoft.Xna.Framework.Point;

namespace Tests.FreeTrainSimulator.Graphics.MapView
{
    [TestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public sealed class ContentAreaOwnershipTests : IDisposable
    {
        private static readonly string[] expectedReleases = { "unregister", "dispose" };
        private ProbeEnvironment emergencyEnvironment;

        [TestMethod]
        [DataRow("mouse")]
        [DataRow("resize")]
        public void ConstructorFailureReleasesAnAcquiredSessionAndPartialSubscriptions(string failureStage)
        {
            using (Game game = new Game())
            {
                using (Font font = new Font("Arial", 12))
                {
                    using (ProbeEnvironment environment = new ProbeEnvironment(failureStage, false))
                    {
                        ProbeEnvironment pendingEnvironment = environment;
                        try
                        {
                            ProbeSessionFactory factory = CreateFactory(ref pendingEnvironment, font);
                            InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(() =>
                            {
                                using (ContentArea unexpected = new ContentArea(game, EmptyContent(false), null, factory))
                                {
                                }
                            });

                            Assert.AreEqual(failureStage, error.Message);
                            Assert.AreEqual(1, environment.ReleaseCount);
                            Assert.AreEqual(0, environment.MouseHandlerCount);
                            Assert.AreEqual(0, environment.ResizeHandlerCount);
                            Assert.AreSequenceEqual(expectedReleases, environment.Releases);
                            Assert.IsGreaterThan(0, font.Height);
                        }
                        finally
                        {
                            pendingEnvironment?.Dispose();
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void ConstructorFailureRetainsAcquisitionAndCleanupErrors()
        {
            using (Game game = new Game())
            {
                using (Font font = new Font("Arial", 12))
                {
                    ProbeEnvironment pendingEnvironment = null;
                    try
                    {
                        emergencyEnvironment = new ProbeEnvironment("resize", true);
                        pendingEnvironment = emergencyEnvironment;
                        ProbeEnvironment environment = pendingEnvironment;
                        ProbeSessionFactory factory = CreateFactory(ref pendingEnvironment, font);
                        MapContentRollbackException error = Assert.ThrowsExactly<MapContentRollbackException>(() =>
                        {
                            using (ContentArea unexpected = new ContentArea(game, EmptyContent(false), null, factory))
                            { 
                            }
                        });

                        Assert.AreEqual("resize", error.InnerExceptions[0].Message);
                        Assert.AreEqual("release", error.Flatten().InnerExceptions[1].Message);
                        Assert.AreEqual(1, environment.ReleaseCount);
                        Assert.AreEqual(0, environment.MouseHandlerCount);
                        Assert.AreEqual(0, environment.ResizeHandlerCount);
                    }
                    finally
                    {
                        try
                        {
                            pendingEnvironment?.Dispose();
                        }
                        catch (InvalidOperationException failure) when (failure.Message == "release")
                        {
                            // Preserve an earlier setup failure when the throwing fixture was never transferred.
                            Assert.AreEqual(1, pendingEnvironment.ReleaseCount);
                        }
                    }
                }
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void RepeatedDiscardReleasesMapSubscriptionsOnceWithoutReleasingBorrowedFont(bool dispatcher)
        {
            using (Game game = new Game())
            {
                using (Font font = new Font("Arial", 12))
                {
                    using (ProbeEnvironment environment = new ProbeEnvironment(string.Empty, false))
                    {
                        ProbeEnvironment pendingEnvironment = environment;
                        try
                        {
                            using (ContentArea map = new ContentArea(game, EmptyContent(dispatcher), null, CreateFactory(ref pendingEnvironment, font)))
                            {
                                Assert.AreEqual(1, environment.MouseHandlerCount);
                                Assert.AreEqual(1, environment.ResizeHandlerCount);

                                map.Dispose();
                                map.Dispose();
                            }
                            Assert.AreEqual(1, environment.ReleaseCount);
                            Assert.AreEqual(0, environment.MouseHandlerCount);
                            Assert.AreEqual(0, environment.ResizeHandlerCount);
                            Assert.AreSequenceEqual(expectedReleases, environment.Releases);
                            Assert.IsGreaterThan(0, font.Height);
                        }
                        finally
                        {
                            pendingEnvironment?.Dispose();
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void StaleCandidateAndRepeatedReplacementDoNotReleaseSiblingDispatcherSubscriptions()
        {
            using (Game game = new Game())
            {
                using (Font font = new Font("Arial", 12))
                {
                    using (ProbeEnvironment dispatcherEnvironment = new ProbeEnvironment(string.Empty, false))
                    {
                        ProbeEnvironment pendingDispatcherEnvironment = dispatcherEnvironment;
                        try
                        {
                            using (ContentArea dispatcher = new ContentArea(game, EmptyContent(true), null, CreateFactory(ref pendingDispatcherEnvironment, font)))
                            {
                                for (int replacement = 0; replacement < 3; replacement++)
                                {
                                    using (ProbeEnvironment candidateEnvironment = new ProbeEnvironment(string.Empty, false))
                                    {
                                        ProbeEnvironment pendingCandidateEnvironment = candidateEnvironment;
                                        try
                                        {
                                            using (ContentArea candidate = new ContentArea(game, EmptyContent(false), null, CreateFactory(ref pendingCandidateEnvironment, font)))
                                            {
                                                // The owner discards stale and superseded candidates through the same Dispose path.
                                                candidate.Dispose();
                                                candidate.Dispose();
                                            }
                                            Assert.AreEqual(1, candidateEnvironment.ReleaseCount);
                                            Assert.AreEqual(0, dispatcherEnvironment.ReleaseCount);
                                            Assert.AreEqual(1, dispatcherEnvironment.MouseHandlerCount);
                                            Assert.AreEqual(1, dispatcherEnvironment.ResizeHandlerCount);
                                            Assert.AreSame(font, dispatcher.ConstantSizeFont);
                                            Assert.IsGreaterThan(0, font.Height);
                                        }
                                        finally
                                        {
                                            pendingCandidateEnvironment?.Dispose();
                                        }
                                    }
                                }
                            }
                            Assert.AreEqual(1, dispatcherEnvironment.ReleaseCount);
                        }
                        finally
                        {
                            pendingDispatcherEnvironment?.Dispose();
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void FailedReleaseIsRetainedWithoutRepeatingOwnerCleanup()
        {
            using (Game game = new Game())
            {
                using (Font font = new Font("Arial", 12))
                {
                    ProbeEnvironment pendingEnvironment = null;
                    try
                    {
                        emergencyEnvironment = new ProbeEnvironment(string.Empty, true);
                        pendingEnvironment = emergencyEnvironment;
                        ProbeEnvironment environment = pendingEnvironment;
                        ContentArea pendingMap = null;
                        try
                        {
                            pendingMap = new ContentArea(game, EmptyContent(false), null, CreateFactory(ref pendingEnvironment, font));
                            ContentArea map = pendingMap;
                            AggregateException failure = Assert.ThrowsExactly<AggregateException>(() => DisposePendingMap(ref pendingMap));
                            Assert.AreSame(failure, Assert.ThrowsExactly<AggregateException>(map.Dispose));

                            Assert.AreEqual(1, environment.ReleaseCount);
                            Assert.AreEqual(0, environment.MouseHandlerCount);
                            Assert.AreEqual(0, environment.ResizeHandlerCount);
                        }
                        finally
                        {
                            pendingMap?.Dispose();
                        }
                    }
                    finally
                    {
                        try
                        {
                            pendingEnvironment?.Dispose();
                        }
                        catch (InvalidOperationException failure) when (failure.Message == "release")
                        {
                            Assert.AreEqual(1, pendingEnvironment.ReleaseCount);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void FailedUnsubscriptionDoesNotSkipSessionReleaseAndBothFailuresSurviveRetry()
        {
            using (Game game = new Game())
            {
                using (Font font = new Font("Arial", 12))
                {
                    ProbeEnvironment pendingEnvironment = null;
                    try
                    {
                        emergencyEnvironment = new ProbeEnvironment("unregister", true);
                        pendingEnvironment = emergencyEnvironment;
                        ProbeEnvironment environment = pendingEnvironment;
                        ContentArea pendingMap = null;
                        try
                        {
                            pendingMap = new ContentArea(game, EmptyContent(false), null, CreateFactory(ref pendingEnvironment, font));
                            ContentArea map = pendingMap;
                            AggregateException failure = Assert.ThrowsExactly<AggregateException>(() => DisposePendingMap(ref pendingMap));
                            Assert.AreSame(failure, Assert.ThrowsExactly<AggregateException>(map.Dispose));
                            Assert.AreEqual("unregister", failure.Flatten().InnerExceptions[0].Message);
                            Assert.AreEqual("release", failure.Flatten().InnerExceptions[1].Message);
                            CollectionAssert.AreEqual(expectedReleases, environment.Releases);
                            Assert.AreEqual((1, 0, 0), (environment.ReleaseCount, environment.MouseHandlerCount, environment.ResizeHandlerCount));
                        }
                        finally
                        {
                            pendingMap?.Dispose();
                        }
                    }
                    finally
                    {
                        try
                        {
                            pendingEnvironment?.Dispose();
                        }
                        catch (InvalidOperationException failure) when (failure.Message == "release")
                        {
                            Assert.AreEqual(1, pendingEnvironment.ReleaseCount);
                        }
                    }
                }
            }
        }

        /// <summary>Releases a throwing fixture only if setup or owner cleanup never attempted its release.</summary>
        public void Dispose()
        {
            if (emergencyEnvironment == null || emergencyEnvironment.ReleaseCount != 0)
                return;

            try
            {
                emergencyEnvironment.Dispose();
            }
            catch (InvalidOperationException failure) when (failure.Message == "release")
            {
                Assert.AreEqual("release", failure.Message);
                Assert.AreEqual(1, emergencyEnvironment.ReleaseCount);
            }
        }

        private static void DisposePendingMap(ref ContentArea map)
        {
            ContentArea owner = map;
            map = null;
            owner?.Dispose();
        }

        private static ContentBase EmptyContent(bool dispatcher)
        {
            // Only shell construction/disposal is exercised, never runtime initialization or drawing.
            return (ContentBase)RuntimeHelpers.GetUninitializedObject(dispatcher ? typeof(DispatcherContent) : typeof(ToolboxContent));
        }

        private static ProbeSessionFactory CreateFactory(ref ProbeEnvironment environment, Font font)
        {
            MapViewController controller = new MapViewController(new MapViewportBounds(0, 0, 100, 100));
            MapViewStateAdapter viewState = new MapViewStateAdapter(controller, font);
            MapInteractionAdapter interaction = new MapInteractionAdapter(new FontManagerInstance("Arial", FontStyle.Regular, 1),
                controller, environment, viewState);
            IMapHostResources resources = new XnaMapHostResources(null, null, null, environment);
            environment = null;
            return new ProbeSessionFactory(new XnaMapHostSession(new ProbeAdapterBundle(viewState, interaction, resources)));
        }

        private sealed class ProbeSessionFactory : IMapHostSessionFactory
        {
            private readonly IMapHostSession session;

            public ProbeSessionFactory(IMapHostSession session)
            {
                this.session = session;
            }

            public IMapHostSession Create(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent)
            {
                return session;
            }
        }

        private sealed class ProbeAdapterBundle : IMapHostAdapterBundle
        {
            public IMapViewStateAdapter ViewStateAdapter { get; }
            public IMapRenderAdapter RenderAdapter => null;
            public IMapInteractionAdapter InteractionAdapter { get; }
            public IMapOverlayShapeAdapter OverlayShapeAdapter => null;
            public IMapHostResources HostResources { get; }

            public ProbeAdapterBundle(IMapViewStateAdapter viewStateAdapter, IMapInteractionAdapter interactionAdapter, IMapHostResources hostResources)
            {
                ViewStateAdapter = viewStateAdapter;
                InteractionAdapter = interactionAdapter;
                HostResources = hostResources;
            }
        }

        private sealed class ProbeEnvironment : IMapHostEnvironment, IDisposable
        {
            private readonly string failureStage;
            private readonly bool failRelease;
            private MouseInputGameComponent.MouseMoveEvent mouseHandlers;
            private EventHandler resizeHandlers;

            /// <summary>Ordered owner cleanup callbacks.</summary>
            public List<string> Releases { get; } = new List<string>();
            /// <summary>Number of environment release attempts.</summary>
            public int ReleaseCount { get; private set; }
            /// <summary>Number of retained map mouse subscriptions.</summary>
            public int MouseHandlerCount => mouseHandlers?.GetInvocationList().Length ?? 0;
            /// <summary>Number of retained map resize subscriptions.</summary>
            public int ResizeHandlerCount => resizeHandlers?.GetInvocationList().Length ?? 0;
            public Point ClientSize => new Point(800, 600);
            public Point PointerPosition => Point.Zero;

            public ProbeEnvironment(string failureStage, bool failRelease)
            {
                this.failureStage = failureStage;
                this.failRelease = failRelease;
            }

            public event EventHandler ClientSizeChanged
            {
                add
                {
                    resizeHandlers += value;
                    if (failureStage == "resize")
                        throw new InvalidOperationException(failureStage);
                }
                remove => resizeHandlers -= value;
            }

            public void RegisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler)
            {
                mouseHandlers += handler;
                if (failureStage == "mouse")
                    throw new InvalidOperationException(failureStage);
            }

            public void UnregisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler)
            {
                Releases.Add("unregister");
                mouseHandlers -= handler;
                if (failureStage == "unregister")
                    throw new InvalidOperationException(failureStage);
            }

            public void Dispose()
            {
                ReleaseCount++;
                Releases.Add("dispose");
                mouseHandlers = null;
                resizeHandlers = null;
                if (failRelease)
                    throw new InvalidOperationException("release");
            }
        }
    }
}
