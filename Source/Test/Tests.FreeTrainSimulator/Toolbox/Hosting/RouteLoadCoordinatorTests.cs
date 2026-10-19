using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.Hosting;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    [TestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public sealed class RouteLoadCoordinatorTests : IDisposable
    {
        private readonly RuntimeDataResolver previousResolver = RuntimeDataResolver.Instance;
        private readonly TrackWorld previousTrackWorld = TrackWorld.Instance;
        private readonly List<NativeRoute> routes = new List<NativeRoute>();
        private readonly List<RouteLoadCoordinator> coordinators = new List<RouteLoadCoordinator>();

        /// <summary>Provides cancellation and reports the isolated native model locations.</summary>
        public TestContext TestContext { get; set; }

        private static readonly string[] expectedStrings = new string[] { "A map", "A drained", "A discard", "B map", "B map update", "B commit" };
        private static readonly string[] expectedStringsDiscard = new string[] { "A map", "A discard", "B map", "B commit" };
        private static readonly string[] expectedStringsCanceledWaiter = new string[] { "A map", "A drained", "A discard", "C map", "C commit" };

        /// <summary>Late shared native reads must not replace an already accepted newer route.</summary>
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenNativePreparationsCompleteInReverseOrderThenOnlyLatestRoutePublishes()
        {
            NativeRoute routeA = await CreateRouteAsync().ConfigureAwait(false);
            NativeRoute routeB = await CreateRouteAsync().ConfigureAwait(false);
            TaskCompletionSource<TrackSectionModel> preparationA = Completion<TrackSectionModel>();
            TaskCompletionSource<TrackSectionModel> preparationB = Completion<TrackSectionModel>();
            NativeModelCache<TrackSectionModel>.Set(routeA.Route.Id, preparationA.Task);
            NativeModelCache<TrackSectionModel>.Set(routeB.Route.Id, preparationB.Task);
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            int maps = 0;
            int commits = 0;
            RuntimeDataResolver published = null;
            RequestScope requestA = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
            await using (requestA.ConfigureAwait(false))
            {
                Task<bool> loadA = requestA.LoadAsync(routeA.Route, false, (_, _) => throw new AssertFailedException("Stale A initialized a map."), () => Assert.Fail("Stale A committed."), () => Assert.Fail("Unpublished A discarded a map."));
                RequestScope requestB = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
                await using (requestB.ConfigureAwait(false))
                {
                    Task<bool> loadB = requestB.LoadAsync(routeB.Route, true, (resolver, _) =>
                    {
                        Assert.IsTrue(dispatcher.IsDispatching);
                        AssertPublished(routeB, resolver, true);
                        maps++;
                        published = resolver;
                        return Task.CompletedTask;
                    }, () =>
                    {
                        Assert.IsTrue(dispatcher.IsDispatching);
                        AssertPublished(routeB, published, true);
                        commits++;
                    }, () => Assert.Fail("Accepted B was discarded."));

                    preparationB.SetResult(routeB.Sections);
                    await dispatcher.RunNextAsync().ConfigureAwait(false);
                    await dispatcher.RunNextAsync().ConfigureAwait(false);
                    bool acceptedB = await loadB.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    // A's shared native task deliberately ignores its canceled request token.
                    preparationA.SetResult(routeA.Sections);
                    bool acceptedA = await loadA.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                    AssertPublished(routeB, published, true);
                    Assert.AreEqual((false, true, 1, 1, requestB.Request), (acceptedA, acceptedB, maps, commits, coordinator.Current));
                }
            }
        }

        /// <summary>Unloading must remain authoritative even when native work ignores cancellation.</summary>
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenUnloadInvalidatesNativePreparationThenLateCompletionCannotRepublish()
        {
            NativeRoute route = await CreateRouteAsync().ConfigureAwait(false);
            TaskCompletionSource<TrackSectionModel> preparation = Completion<TrackSectionModel>();
            NativeModelCache<TrackSectionModel>.Set(route.Route.Id, preparation.Task);
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            RequestScope request = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
            await using (request.ConfigureAwait(false))
            {
                Task<bool> load = request.LoadAsync(route.Route, true,
                    (_, _) => throw new AssertFailedException("Unloaded route initialized a map."),
                    () => Assert.Fail("Unloaded route committed."), () => Assert.Fail("Unpublished route discarded a map."));

                coordinator.Invalidate();
                GameService<RuntimeDataResolver>.Set(null, null);
                GameService<TrackWorld>.Set(null, null);
                preparation.SetResult(route.Sections);
                bool accepted = await load.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreEqual((false, true, true, true, false),
                    (accepted, coordinator.Current is null, RuntimeDataResolver.Instance is null,
                    TrackWorld.Instance is null, dispatcher.HasPending));
            }
        }

        /// <summary>Ownership cannot be decided before the game-thread completion actually runs.</summary>
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenUnloadOccursAfterCommitIsQueuedThenOwnershipIsCheckedInsideDispatcher()
        {
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            RequestScope request = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
            await using (request.ConfigureAwait(false))
            {
                int commits = 0;
                Task<bool> commit = request.CommitAsync(() => commits++);
                Assert.IsTrue(dispatcher.HasPending);

                coordinator.Invalidate();
                await dispatcher.RunNextAsync().ConfigureAwait(false);
                bool accepted = await commit.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreEqual((false, 0, previousResolver, previousTrackWorld),
                    (accepted, commits, RuntimeDataResolver.Instance, TrackWorld.Instance));
            }
        }

        /// <summary>Prepared data is private until a still-owned dispatched publication runs.</summary>
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenUnloadOccursAfterPublicationIsQueuedThenNeitherGlobalIsPublished()
        {
            NativeRoute route = await CreateRouteAsync().ConfigureAwait(false);
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            RequestScope request = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
            await using (request.ConfigureAwait(false))
            {
                int discards = 0;
                Task<bool> load = request.LoadAsync(route.Route, true,
                    (_, _) => throw new AssertFailedException("Queued stale publication initialized a map."),
                    () => Assert.Fail("Queued stale publication committed."), () =>
                    {
                        Assert.IsTrue(dispatcher.IsDispatching);
                        discards++;
                    });
                Assert.IsTrue(dispatcher.HasPending);
                Assert.AreEqual((previousResolver, previousTrackWorld),
                    (RuntimeDataResolver.Instance, TrackWorld.Instance), "Preparation must not publish either runtime service.");

                coordinator.Invalidate();
                GameService<RuntimeDataResolver>.Set(null, null);
                GameService<TrackWorld>.Set(null, null);
                await dispatcher.RunNextAsync().ConfigureAwait(false); // Rejected publication.
                await dispatcher.RunNextAsync().ConfigureAwait(false); // Rejected final commit.
                await dispatcher.RunNextAsync().ConfigureAwait(false); // Candidate cleanup.
                bool accepted = await load.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreEqual((false, 1, true, true, false),
                    (accepted, discards, RuntimeDataResolver.Instance is null, TrackWorld.Instance is null, dispatcher.HasPending));
            }
        }

        /// <summary>Legacy global readers require the entire old map phase to drain before replacement.</summary>
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenSupersededMapIsPausedThenNextMapWaitsForItsDrainAndDiscard()
        {
            NativeRoute routeA = await CreateRouteAsync().ConfigureAwait(false);
            NativeRoute routeB = await CreateRouteAsync().ConfigureAwait(false);
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            TaskCompletionSource resumeA = Completion();
            List<string> events = new List<string>();
            string status = "Loading A";
            RuntimeDataResolver resolverA = null;
            RuntimeDataResolver resolverB = null;
            RequestScope requestA = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
            await using (requestA.ConfigureAwait(false))
            {
                Task<bool> loadA = requestA.LoadAsync(routeA.Route, false, async (resolver, _) =>
                {
                    Assert.IsTrue(dispatcher.IsDispatching);
                    AssertPublished(routeA, resolver, false);
                    resolverA = resolver;
                    events.Add("A map");
                    await resumeA.Task.WaitAsync(requestA.CancellationToken).ConfigureAwait(false);
                    AssertPublished(routeA, resolver, false);
                    events.Add("A drained");
                }, () => Assert.Fail("Superseded A committed."), () =>
                {
                    Assert.IsTrue(dispatcher.IsDispatching);
                    AssertPublished(routeA, resolverA, false);
                    events.Add("A discard");
                });
                Task mapA = await dispatcher.StartNextAsync().ConfigureAwait(false);
                RequestScope requestB = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
                await using (requestB.ConfigureAwait(false))
                {
                    status = "Loading B";
                    Task<bool> loadB = requestB.LoadAsync(routeB.Route, true, async (resolver, guardedActionAsync) =>
                    {
                        Assert.IsTrue(dispatcher.IsDispatching);
                        AssertPublished(routeB, resolver, true);
                        resolverB = resolver;
                        events.Add("B map");
                        await guardedActionAsync(() =>
                        {
                            Assert.IsTrue(dispatcher.IsDispatching);
                            AssertPublished(routeB, resolver, true);
                            events.Add("B map update");
                        }).ConfigureAwait(false);
                    }, () =>
                    {
                        Assert.IsTrue(dispatcher.IsDispatching);
                        AssertPublished(routeB, resolverB, true);
                        events.Add("B commit");
                        status = "Ready B";
                    }, () => Assert.Fail("Accepted B was discarded."));

                    // All B native tasks are already complete, so its only wait is the map gate.
                    Assert.AreEqual((false, false, "Loading B"), (dispatcher.HasPending, loadB.IsCompleted, status));
                    AssertPublished(routeA, resolverA, false);
                    resumeA.SetResult();
                    await mapA.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await dispatcher.RunNextAsync().ConfigureAwait(false); // Reject A's final commit even though its map succeeded.
                    Assert.AreEqual("Loading B", status);
                    await dispatcher.RunNextAsync().ConfigureAwait(false); // Drain A's candidate before releasing the gate.
                    bool acceptedA = await loadA.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    Task mapB = await dispatcher.StartNextAsync().ConfigureAwait(false);
                    await dispatcher.RunNextAsync().ConfigureAwait(false); // B's guarded map mutation.
                    await mapB.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await dispatcher.RunNextAsync().ConfigureAwait(false); // B's final commit.
                    bool acceptedB = await loadB.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                    AssertPublished(routeB, resolverB, true);
                    Assert.AreSequenceEqual(expectedStrings, events);
                    Assert.AreEqual((false, true, "Ready B", false), (acceptedA, acceptedB, status, dispatcher.HasPending));
                }
            }
        }

        /// <summary>Canceling a queued map must not let its successor bypass an earlier map that is still draining.</summary>
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenWaitingMapIsCanceledThenSuccessorStillWaitsForActiveMapToDrainAndDiscard()
        {
            NativeRoute routeA = await CreateRouteAsync().ConfigureAwait(false);
            NativeRoute routeB = await CreateRouteAsync().ConfigureAwait(false);
            NativeRoute routeC = await CreateRouteAsync().ConfigureAwait(false);
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            TaskCompletionSource resumeA = Completion();
            List<string> events = new List<string>();
            RuntimeDataResolver resolverA = null;
            RuntimeDataResolver resolverC = null;
            RequestScope requestA = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
            await using (requestA.ConfigureAwait(false))
            {
                Task<bool> loadA = requestA.LoadAsync(routeA.Route, false, async (resolver, _) =>
                {
                    Assert.IsTrue(dispatcher.IsDispatching);
                    AssertPublished(routeA, resolver, false);
                    resolverA = resolver;
                    events.Add("A map");
                    await resumeA.Task.WaitAsync(requestA.CancellationToken).ConfigureAwait(false);
                    AssertPublished(routeA, resolver, false);
                    events.Add("A drained");
                }, () => Assert.Fail("Superseded A committed."), () =>
                {
                    Assert.IsTrue(dispatcher.IsDispatching);
                    AssertPublished(routeA, resolverA, false);
                    events.Add("A discard");
                });
                Task mapA = await dispatcher.StartNextAsync().ConfigureAwait(false);
                RequestScope requestB = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
                await using (requestB.ConfigureAwait(false))
                {
                    Task<bool> loadB = requestB.LoadAsync(routeB.Route, true,
                        (_, _) => throw new AssertFailedException("Canceled B initialized a map."),
                        () => Assert.Fail("Canceled B committed."), () => Assert.Fail("Unpublished B discarded a map."));
                    Assert.AreEqual((false, false), (dispatcher.HasPending, loadB.IsCompleted));

                    coordinator.CancelCurrent();
                    bool acceptedB = await loadB.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    Assert.IsFalse(acceptedB);

                    // B has completed its own turn, but its tail must still include A's unfinished turn.
                    RequestScope requestC = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
                    await using (requestC.ConfigureAwait(false))
                    {
                        Task<bool> loadC = requestC.LoadAsync(routeC.Route, true, (resolver, _) =>
                        {
                            Assert.IsTrue(dispatcher.IsDispatching);
                            AssertPublished(routeC, resolver, true);
                            resolverC = resolver;
                            events.Add("C map");
                            return Task.CompletedTask;
                        }, () =>
                        {
                            Assert.IsTrue(dispatcher.IsDispatching);
                            AssertPublished(routeC, resolverC, true);
                            events.Add("C commit");
                        }, () => Assert.Fail("Accepted C was discarded."));
                        Assert.AreEqual((false, false, false), (mapA.IsCompleted, dispatcher.HasPending, loadC.IsCompleted));
                        AssertPublished(routeA, resolverA, false);

                        resumeA.SetResult();
                        await mapA.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        await dispatcher.RunNextAsync().ConfigureAwait(false); // Reject A's final commit.
                        await dispatcher.RunNextAsync().ConfigureAwait(false); // Discard A before releasing C.
                        bool acceptedA = await loadA.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        await dispatcher.RunNextAsync().ConfigureAwait(false); // Publish C, not B.
                        await dispatcher.RunNextAsync().ConfigureAwait(false); // Commit C.
                        bool acceptedC = await loadC.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                        AssertPublished(routeC, resolverC, true);
                        Assert.AreSequenceEqual(expectedStringsCanceledWaiter, events);
                        Assert.AreEqual((false, true, requestC.Request, false), (acceptedA, acceptedC, coordinator.Current, dispatcher.HasPending));
                    }
                }
            }
        }

        /// <summary>A failed old candidate cannot block a new map or apply a late error status.</summary>
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenMapFailsThenGateIsReleasedAndLateFailureCannotEraseNextRouteStatus()
        {
            NativeRoute routeA = await CreateRouteAsync().ConfigureAwait(false);
            NativeRoute routeB = await CreateRouteAsync().ConfigureAwait(false);
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            TaskCompletionSource failMap = Completion();
            InvalidOperationException failure = new InvalidOperationException("Native A map failed.");
            List<string> events = new List<string>();
            string status = "Loading A";
            RuntimeDataResolver resolverA = null;
            RuntimeDataResolver resolverB = null;
            RequestScope requestA = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
            await using (requestA.ConfigureAwait(false))
            {
                Task<bool> loadA = requestA.LoadAsync(routeA.Route, false, async (resolver, _) =>
                {
                    Assert.IsTrue(dispatcher.IsDispatching);
                    AssertPublished(routeA, resolver, false);
                    resolverA = resolver;
                    events.Add("A map");
                    await failMap.Task.WaitAsync(requestA.CancellationToken).ConfigureAwait(false);
                    throw failure;
                }, () => Assert.Fail("Failed A committed."), () =>
                {
                    Assert.IsTrue(dispatcher.IsDispatching);
                    AssertPublished(routeA, resolverA, false);
                    events.Add("A discard");
                });
                Task mapA = await dispatcher.StartNextAsync().ConfigureAwait(false);
                RequestScope requestB = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
                await using (requestB.ConfigureAwait(false))
                {
                    status = "Loading B";
                    Task<bool> loadB = requestB.LoadAsync(routeB.Route, true, (resolver, _) =>
                    {
                        Assert.IsTrue(dispatcher.IsDispatching);
                        AssertPublished(routeB, resolver, true);
                        resolverB = resolver;
                        events.Add("B map");
                        return Task.CompletedTask;
                    }, () =>
                    {
                        Assert.IsTrue(dispatcher.IsDispatching);
                        events.Add("B commit");
                        status = "Ready B";
                    }, () => Assert.Fail("Accepted B was discarded."));
                    Assert.IsFalse(dispatcher.HasPending);

                    failMap.SetResult();
                    _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => mapA).ConfigureAwait(false);
                    await dispatcher.RunNextAsync().ConfigureAwait(false); // A's cleanup must precede B's publication.
                    InvalidOperationException observed = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                        async () => await loadA.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
                    Assert.AreEqual("Loading B", status);
                    await dispatcher.RunNextAsync().ConfigureAwait(false);
                    await dispatcher.RunNextAsync().ConfigureAwait(false);
                    bool acceptedB = await loadB.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    Task<bool> lateFailure = requestA.CommitAsync(() => status = "Late A failure");
                    await dispatcher.RunNextAsync().ConfigureAwait(false);
                    bool failureApplied = await lateFailure.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                    Assert.AreSame(failure, observed);
                    AssertPublished(routeB, resolverB, true);
                    Assert.AreSequenceEqual(expectedStringsDiscard, events);
                    Assert.AreEqual((true, false, "Ready B", false), (acceptedB, failureApplied, status, dispatcher.HasPending));
                }
            }
        }

        /// <summary>Map mutations queued before unloading must still honor the unload boundary.</summary>
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenUnloadOccursDuringQueuedMapMutationThenGuardRejectsMutationAndCandidateIsDiscarded()
        {
            NativeRoute route = await CreateRouteAsync().ConfigureAwait(false);
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            RequestScope request = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
            await using (request.ConfigureAwait(false))
            {
                int discards = 0;
                string status = "Loading";
                Task<bool> load = request.LoadAsync(route.Route, true, async (resolver, guardedActionAsync) =>
                {
                    Assert.IsTrue(dispatcher.IsDispatching);
                    AssertPublished(route, resolver, true);
                    await guardedActionAsync(() => status = "Stale map update").ConfigureAwait(false);
                }, () => Assert.Fail("Unloaded map committed."), () =>
                {
                    Assert.IsTrue(dispatcher.IsDispatching);
                    discards++;
                });
                Task map = await dispatcher.StartNextAsync().ConfigureAwait(false);
                Assert.IsTrue(dispatcher.HasPending);

                coordinator.Invalidate();
                GameService<RuntimeDataResolver>.Set(null, null);
                GameService<TrackWorld>.Set(null, null);
                status = "Unloaded";
                await dispatcher.RunNextAsync().ConfigureAwait(false);
                _ = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => map).ConfigureAwait(false);
                await dispatcher.RunNextAsync().ConfigureAwait(false);
                bool accepted = await load.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreEqual((false, 1, "Unloaded", true, true, false),
                    (accepted, discards, status, RuntimeDataResolver.Instance is null, TrackWorld.Instance is null, dispatcher.HasPending));
            }
        }

        /// <summary>Unsaved-change confirmation must not cancel the accepted load prematurely.</summary>
        [TestMethod]
        public void WhenSelectionAwaitsConfirmationThenAcceptedRequestIsNotCanceled()
        {
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            long acceptedIntent = coordinator.BeginSelection();
            using (RouteLoadCoordinator.Request request = coordinator.BeginRequest(acceptedIntent))
            {
                long pendingIntent = coordinator.BeginSelection();

                Assert.AreEqual((true, false, true, false),
                    (coordinator.IsCurrent(request), coordinator.IsLatestSelection(acceptedIntent),
                    coordinator.IsLatestSelection(pendingIntent), request.Token.IsCancellationRequested));
            }
        }

        /// <summary>Delayed confirmation or discovery cannot supersede newer accepted intent.</summary>
        [TestMethod]
        public void WhenEarlierConfirmationOrDiscoveryCompletesThenItCannotReplaceLatestRequest()
        {
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            long earlierIntent = coordinator.BeginSelection();
            long latestIntent = coordinator.BeginSelection();
            using RouteLoadCoordinator.Request latest = coordinator.BeginRequest(latestIntent);

            using (RouteLoadCoordinator.Request rejected = coordinator.BeginRequest(earlierIntent))
            {
                Assert.AreEqual((true, latest, true, false),
                    (rejected is null, coordinator.Current, coordinator.IsCurrent(latest), latest.Token.IsCancellationRequested));
            }
        }

        /// <summary>Unloading also rejects discovery that has not yet produced a request.</summary>
        [TestMethod]
        public void WhenUnloadInvalidatesPendingDiscoveryThenItsIntentCannotStartARequest()
        {
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            long pendingIntent = coordinator.BeginSelection();

            coordinator.Invalidate();
            using (RouteLoadCoordinator.Request rejected = coordinator.BeginRequest(pendingIntent))
            {
                Assert.AreEqual((false, true, true),
                    (coordinator.IsLatestSelection(pendingIntent), rejected is null, coordinator.Current is null));
            }
        }

        /// <summary>Canceling route work is distinct from invalidating pending user intent.</summary>
        [TestMethod]
        public void WhenCurrentRequestIsCanceledThenPendingSelectionIntentRemainsValid()
        {
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            using RouteLoadCoordinator.Request previous = coordinator.BeginRequest(coordinator.BeginSelection());
            long pendingIntent = coordinator.BeginSelection();

            coordinator.CancelCurrent();
            using (RouteLoadCoordinator.Request next = coordinator.BeginRequest(pendingIntent))
            {
                Assert.AreEqual((true, true, true),
                    (previous.Token.IsCancellationRequested, coordinator.IsLatestSelection(pendingIntent), coordinator.IsCurrent(next)));
            }
        }

        /// <summary>Canceling a request waiter must leave the shared native read reusable by a retry.</summary>
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenCanceledNativeLoadIsRetriedForSameRouteThenSharedReadStillSucceeds()
        {
            NativeRoute route = await CreateRouteAsync().ConfigureAwait(false);
            TaskCompletionSource<TrackSectionModel> preparation = Completion<TrackSectionModel>();
            NativeModelCache<TrackSectionModel>.Set(route.Route.Id, preparation.Task);
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            RequestScope canceled = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
            await using (canceled.ConfigureAwait(false))
            {
                Task<bool> firstLoad = canceled.LoadAsync(route.Route, false,
                    (_, _) => throw new AssertFailedException("Canceled attempt initialized a map."),
                    () => Assert.Fail("Canceled attempt committed."), () => Assert.Fail("Unpublished attempt discarded a map."));

                coordinator.CancelCurrent();
                bool firstAccepted = await firstLoad.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                Assert.IsFalse(preparation.Task.IsCompleted, "Canceling one waiter must not cancel the cached native read.");
                RequestScope retry = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
                await using (retry.ConfigureAwait(false))
                {
                    int maps = 0;
                    int commits = 0;
                    RuntimeDataResolver published = null;
                    Task<bool> retryLoad = retry.LoadAsync(route.Route, true, (resolver, _) =>
                    {
                        Assert.IsTrue(dispatcher.IsDispatching);
                        AssertPublished(route, resolver, true);
                        published = resolver;
                        maps++;
                        return Task.CompletedTask;
                    }, () =>
                    {
                        Assert.IsTrue(dispatcher.IsDispatching);
                        commits++;
                    }, () => Assert.Fail("Successful retry was discarded."));
                    // No cache reset or fresh model: the retry shares the original native read.
                    preparation.SetResult(route.Sections);
                    await dispatcher.RunNextAsync().ConfigureAwait(false);
                    await dispatcher.RunNextAsync().ConfigureAwait(false);
                    bool retryAccepted = await retryLoad.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                    AssertPublished(route, published, true);
                    Assert.AreEqual((false, true, 1, 1, true), (firstAccepted, retryAccepted, maps, commits, coordinator.IsCurrent(retry.Request)));
                }
            }
        }

        /// <summary>Early scope exit must drain paused map work before disposing its request.</summary>
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenRequestScopeEndsWithPausedMapThenWorkDrainsBeforeRequestIsDisposed()
        {
            NativeRoute route = await CreateRouteAsync().ConfigureAwait(false);
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            TaskCompletionSource resumeMap = Completion();
            Task<bool> load;
            WaitHandle cancellationHandle;
            bool drained = false;
            int discards = 0;
            RequestScope request = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
            await using (request.ConfigureAwait(false))
            {
                cancellationHandle = request.Request.Token.WaitHandle;
                load = request.LoadAsync(route.Route, true, async (_, _) =>
                {
                    try
                    {
                        await resumeMap.Task.WaitAsync(request.CancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        Assert.IsFalse(cancellationHandle.SafeWaitHandle.IsClosed, "The request must outlive its map work.");
                        drained = true;
                    }
                }, () => Assert.Fail("An abandoned load committed."), () =>
                {
                    Assert.IsTrue(dispatcher.IsDispatching);
                    Assert.IsFalse(cancellationHandle.SafeWaitHandle.IsClosed, "The request must outlive candidate cleanup.");
                    discards++;
                });
                Task map = await dispatcher.StartNextAsync().ConfigureAwait(false);
                Assert.IsFalse(map.IsCompleted);
            }

            bool accepted = await load.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            Assert.AreEqual((false, true, 1, true, false),
                (accepted, drained, discards, cancellationHandle.SafeWaitHandle.IsClosed, dispatcher.HasPending));
        }

        /// <summary>Completing a load scope releases resources without invalidating the accepted route.</summary>
        [TestMethod]
        public void WhenLoadScopeCompletesThenDisposalReleasesResourcesAndPreservesAcceptedRequest()
        {
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            using (RouteLoadCoordinator.Request request = coordinator.BeginRequest(coordinator.BeginSelection()))
            {
                CancellationToken token = request.Token;
                WaitHandle cancellationHandle = token.WaitHandle;

                request.Dispose();
                request.Dispose();

                Assert.AreEqual((true, false, true), (cancellationHandle.SafeWaitHandle.IsClosed, token.IsCancellationRequested, coordinator.IsCurrent(request)));
            }
        }

        /// <summary>Unloading a completed request must not cancel its already disposed source.</summary>
        [TestMethod]
        public void WhenDisposedRequestIsInvalidatedThenCancellationIsSafeAndOwnershipIsCleared()
        {
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            using (RouteLoadCoordinator.Request request = coordinator.BeginRequest(coordinator.BeginSelection()))
            {
                CancellationToken token = request.Token;
                request.Dispose();

                coordinator.Invalidate();
                coordinator.Invalidate();

                Assert.AreEqual((false, true, false),
                    (token.IsCancellationRequested, coordinator.Current is null, coordinator.IsCurrent(request)));
            }
        }

        /// <summary>Stopping invalidates ownership permanently while request resources remain caller-owned.</summary>
        [TestMethod]
        public void WhenCoordinatorIsStoppedThenRequestIsCanceledAndNewSelectionsAreRejected()
        {
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            long intent = coordinator.BeginSelection();
            using (RouteLoadCoordinator.Request request = coordinator.BeginRequest(intent))
            {
                CancellationToken token = request.Token;
                WaitHandle cancellationHandle = token.WaitHandle;

                coordinator.Stop();
                coordinator.Stop();

                Assert.AreEqual((true, true, false, false, true, false),
                    (token.IsCancellationRequested, coordinator.Current is null, coordinator.IsCurrent(request),
                    coordinator.IsLatestSelection(intent), coordinator.BeginRequest(intent) is null, cancellationHandle.SafeWaitHandle.IsClosed));
                _ = Assert.ThrowsExactly<InvalidOperationException>(() => coordinator.BeginSelection());
            }
        }

        /// <summary>Shutdown cancels waiting work without interrupting the active map's drain and cleanup.</summary>
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenCoordinatorIsStoppedDuringMapPhaseThenOwnerAndWaiterDrainSafely()
        {
            NativeRoute routeA = await CreateRouteAsync().ConfigureAwait(false);
            NativeRoute routeB = await CreateRouteAsync().ConfigureAwait(false);
            QueuedDispatcher dispatcher = new QueuedDispatcher(TestContext.CancellationToken);
            RouteLoadCoordinator coordinator = CreateCoordinator(dispatcher);
            TaskCompletionSource resumeA = Completion();
            int discards = 0;
            RequestScope requestA = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
            await using (requestA.ConfigureAwait(false))
            {
                Task<bool> loadA = requestA.LoadAsync(routeA.Route, false, async (resolver, _) =>
                {
                    AssertPublished(routeA, resolver, false);
                    await resumeA.Task.WaitAsync(requestA.CancellationToken).ConfigureAwait(false);
                }, () => Assert.Fail("A committed during shutdown."), () =>
                {
                    Assert.IsTrue(dispatcher.IsDispatching);
                    discards++;
                });
                Task mapA = await dispatcher.StartNextAsync().ConfigureAwait(false);
                RequestScope requestB = new RequestScope(coordinator, dispatcher, TestContext.CancellationToken);
                await using (requestB.ConfigureAwait(false))
                {
                    Task<bool> loadB = requestB.LoadAsync(routeB.Route, true,
                        (_, _) => throw new AssertFailedException("Waiting B initialized during shutdown."),
                        () => Assert.Fail("Waiting B committed during shutdown."), () => Assert.Fail("Unpublished B was discarded."));
                    Assert.AreEqual((false, false), (dispatcher.HasPending, loadB.IsCompleted));

                    coordinator.Stop();
                    coordinator.Stop();
                    bool acceptedB = await loadB.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    Assert.AreEqual((false, false, true, true, true),
                        (acceptedB, mapA.IsCompleted, requestA.Request.Token.IsCancellationRequested,
                        requestB.Request.Token.IsCancellationRequested, coordinator.Current is null));

                    resumeA.SetResult();
                    await mapA.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await dispatcher.RunNextAsync().ConfigureAwait(false); // Discard A before completing its map turn.
                    bool acceptedA = await loadA.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    bool lateCommit = await requestA.CommitAsync(() => Assert.Fail("A committed after shutdown.")).ConfigureAwait(false);
                    bool lateLoad = await requestA.LoadAsync(routeA.Route, false,
                        (_, _) => throw new AssertFailedException("A load started after shutdown."),
                        () => Assert.Fail("A new load committed after shutdown."), () => Assert.Fail("A new load discarded after shutdown.")).ConfigureAwait(false);

                    Assert.AreEqual((false, false, false, 1, false), (acceptedA, lateCommit, lateLoad, discards, dispatcher.HasPending));
                }
            }
        }

        /// <summary>Restores services and removes only cache entries owned by this test.</summary>
        public void Dispose()
        {
            foreach (RouteLoadCoordinator coordinator in coordinators)
                coordinator.Stop();
            foreach (NativeRoute route in routes)
            {
                NativeModelCache<TrackSectionModel>.Remove(route.Route.Id);
                NativeModelCache<TrackModel>.Remove(route.Route.Id);
                NativeModelCache<SignalConfigurationModel>.Remove(route.Route.Id);
                NativeModelCache<RouteModelHeader>.Remove(route.Route.Hierarchy());
                NativeModelCache<RouteModelHeader>.RemoveCollection(route.Route.Parent.Hierarchy());
            }
            GameService<RuntimeDataResolver>.Set(null, previousResolver);
            GameService<TrackWorld>.Set(null, previousTrackWorld);
            GC.SuppressFinalize(this);
        }

        private RouteLoadCoordinator CreateCoordinator(QueuedDispatcher dispatcher)
        {
            RouteLoadCoordinator coordinator = new RouteLoadCoordinator(dispatcher.DispatchAsync);
            coordinators.Add(coordinator);
            return coordinator;
        }

        private async Task<NativeRoute> CreateRouteAsync()
        {
            CancellationToken token = TestContext.CancellationToken;
            string identity = Guid.NewGuid().ToString("N");
            // This path is metadata only: there is no installed/imported source content.
            FolderModel folder = new FolderModel("Coordinator" + identity,
                Path.Combine(Path.GetTempPath(), "FreeTrainSimulator", "AbsentSource", identity), null);
            Tile tile = new Tile(routes.Count + 1, 1);
            WorldLocation location = new WorldLocation(tile, Vector3.Zero);
            RouteModel route = new RouteModel(location)
            {
                Id = identity,
                Name = "Coordinator " + identity,
                Tags = ImmutableDictionary<string, string>.Empty,
                RouteConditions = new RouteConditionModel(),
            };
            route.Initialize(folder);
            await RouteModelHandler.ToFile(route, token).ConfigureAwait(false);
            route = await RouteModelHandler.GetExtended(route.Id, folder, token).ConfigureAwait(false);
            await TrackSectionsModelHandler.Create(new TrackSectionModel
            {
                Id = identity,
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1,
                    new TrackSection { SectionIndex = 1, Gauge = 1.435f, Length = 100 + routes.Count }),
            }, route, token).ConfigureAwait(false);
            await TrackModelHandler.Create(new TrackModel
            {
                Id = identity,
                TrackDatabase = new TrackDatabase
                {
                    TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                        new EndNode(location, tile, Vector3.Zero) { NodeIndex = 1 }),
                    TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex()),
                },
            }, route, token).ConfigureAwait(false);
            await SignalConfigurationModelHandler.Create(new SignalConfigurationModel
            {
                Id = identity,
                Name = "Signals " + identity,
            }, route, token).ConfigureAwait(false);

            // All three handlers now expose completed tasks backed by actual deserialization.
            TrackSectionModel sections = await route.GetTrackSectionModel(token).ConfigureAwait(false);
            TrackModel track = await route.GetTrackModel(token).ConfigureAwait(false);
            SignalConfigurationModel signals = await route.GetSignalConfigurationModel(token).ConfigureAwait(false);
            NativeRoute result = new NativeRoute(route, sections, track, signals);
            routes.Add(result);
            TestContext.WriteLine("Native models: {0}", ModelFileResolver<TrackModel>.FilePath(track));
            return result;
        }

        private static TaskCompletionSource<T> Completion<T>() => new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        private static TaskCompletionSource Completion() => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        private static void AssertPublished(NativeRoute expected, RuntimeDataResolver resolver, bool metricUnits)
        {
            Assert.AreEqual((true, true, true, true, true, true, metricUnits),
                (ReferenceEquals(resolver, RuntimeDataResolver.Instance),
                ReferenceEquals(resolver.TrackWorld, TrackWorld.Instance),
                ReferenceEquals(expected.Route, resolver.RouteData),
                ReferenceEquals(expected.Sections, resolver.TrackSections),
                ReferenceEquals(expected.Signals, resolver.SignalConfiguration),
                ReferenceEquals(expected.Track.TrackDatabase, resolver.TrackWorld.TrackDatabase),
                resolver.MetricUnits), "The map and both globals must refer to exactly the published native route.");
        }

        /// <summary>Owns a request until all deliberately overlapping test operations have drained.</summary>
        private sealed class RequestScope : IAsyncDisposable
        {
            private readonly RouteLoadCoordinator coordinator;
            private readonly QueuedDispatcher dispatcher;
            private readonly CancellationTokenSource cancellation;
            private readonly List<Task> operations = new List<Task>();

            internal RouteLoadCoordinator.Request Request { get; }

            internal CancellationToken CancellationToken => cancellation.Token;

            internal RequestScope(RouteLoadCoordinator coordinator, QueuedDispatcher dispatcher, CancellationToken cancellationToken)
            {
                this.coordinator = coordinator;
                this.dispatcher = dispatcher;
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Request = coordinator.BeginRequest(coordinator.BeginSelection());
            }

            internal Task<bool> LoadAsync(RouteModel route, bool metricUnits,
                Func<RuntimeDataResolver, Func<Action, Task>, Task> initializeMapAsync, Action commit, Action discard)
            {
                Task<bool> operation = coordinator.LoadAsync(Request, route, metricUnits, initializeMapAsync, commit, discard);
                operations.Add(operation);
                return operation;
            }

            internal Task<bool> CommitAsync(Action action)
            {
                Task<bool> operation = coordinator.CommitAsync(Request, action);
                operations.Add(operation);
                return operation;
            }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    if (operations.Exists(operation => !operation.IsCompleted))
                    {
                        Request.Cancel();
                        // Release test-only pauses as well as canceling the production request.
                        await cancellation.CancelAsync().ConfigureAwait(false);
                    }

                    await dispatcher.DrainAsync(Task.WhenAll(operations)).ConfigureAwait(false);
                }
                finally
                {
                    Request.Dispose();
                    cancellation.Dispose();
                }
            }
        }

        private sealed class NativeRoute
        {
            /// <summary>The persisted and reloaded route.</summary>
            internal RouteModel Route { get; }
            /// <summary>The exact native geometry returned by the handler cache.</summary>
            internal TrackSectionModel Sections { get; }
            /// <summary>The exact native infrastructure returned by the handler cache.</summary>
            internal TrackModel Track { get; }
            /// <summary>The exact native signal configuration returned by the handler cache.</summary>
            internal SignalConfigurationModel Signals { get; }

            internal NativeRoute(RouteModel route, TrackSectionModel sections, TrackModel track, SignalConfigurationModel signals)
            {
                Route = route;
                Sections = sections;
                Track = track;
                Signals = signals;
            }
        }

        private sealed class NativeModelCache<TModel> : ContentHandlerBase<TModel> where TModel : ModelBase
        {
            internal static void Set(string key, Task<TModel> task) => modelTaskCache[key] = task;

            internal static void Remove(string key) => modelTaskCache.TryRemove(key, out _);

            internal static void RemoveCollection(string key)
            {
                collectionUpdateRequired.TryRemove(key, out _);
                modelSetTaskCache.TryRemove(key, out _);
            }
        }

        private sealed class QueuedDispatcher
        {
            private readonly Channel<QueuedAction> queue = Channel.CreateUnbounded<QueuedAction>(new UnboundedChannelOptions
            {
                SingleReader = true,
                AllowSynchronousContinuations = false,
            });
            private readonly AsyncLocal<bool> dispatching = new AsyncLocal<bool>();
            private readonly CancellationToken cancellationToken;

            internal bool IsDispatching => dispatching.Value;
            internal bool HasPending => queue.Reader.TryPeek(out _);

            internal QueuedDispatcher(CancellationToken cancellationToken)
            {
                this.cancellationToken = cancellationToken;
            }

            internal Task DispatchAsync(Func<Task> action)
            {
                QueuedAction queued = new QueuedAction(action);
                return !queue.Writer.TryWrite(queued) ? throw new InvalidOperationException("The test dispatcher is closed.") : queued.Completion;
            }

            internal async Task<Task> StartNextAsync()
            {
                QueuedAction next = await queue.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                return next.StartAsync(dispatching);
            }

            internal async Task RunNextAsync()
            {
                Task execution = await StartNextAsync().ConfigureAwait(false);
                await execution.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            /// <summary>Finishes owned work during teardown, even after the test token is canceled.</summary>
            internal async Task DrainAsync(Task completion)
            {
                using (CancellationTokenSource finished = new CancellationTokenSource())
                {
                    try
                    {
                        while (!completion.IsCompleted)
                        {
                            if (queue.Reader.TryRead(out QueuedAction next))
                            {
                                // A map action can await another queued action; do not block the pump.
                                _ = next.StartAsync(dispatching);
                            }
                            else
                            {
                                _ = await Task.WhenAny(completion, queue.Reader.WaitToReadAsync(finished.Token).AsTask()).ConfigureAwait(false);
                            }
                        }

                        // Tests await the original tasks to assert results and expected exceptions.
                        // Teardown observes failures without replacing an assertion already in flight.
                        await completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    }
                    finally
                    {
                        await finished.CancelAsync().ConfigureAwait(false);
                    }
                }
            }

            private sealed class QueuedAction
            {
                private readonly Func<Task> action;
                private readonly TaskCompletionSource<Task> started = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);

                internal Task Completion => started.Task.Unwrap();

                internal QueuedAction(Func<Task> action)
                {
                    this.action = action;
                }

                internal Task StartAsync(AsyncLocal<bool> dispatching)
                {
                    Task execution = ExecuteAsync(dispatching);
                    started.SetResult(execution);
                    return execution;
                }

                private async Task ExecuteAsync(AsyncLocal<bool> dispatching)
                {
                    bool previous = dispatching.Value;
                    dispatching.Value = true;
                    try
                    {
                        await action().ConfigureAwait(false);
                    }
                    finally
                    {
                        dispatching.Value = previous;
                    }
                }
            }
        }
    }
}
