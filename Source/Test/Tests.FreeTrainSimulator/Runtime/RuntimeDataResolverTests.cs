using System;
using System.Collections.Immutable;
using System.Threading;
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Runtime
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class RuntimeDataResolverTests
    {
        private TrackWorld previousWorld;
        private RuntimeDataResolver previousResolver;
        private RuntimeDataResolver publishedResolver;

        [TestInitialize]
        public async Task InitializeAsync()
        {
            previousWorld = TrackWorld.Instance;
            previousResolver = RuntimeDataResolver.Instance;
            RouteModel route = await CreateRouteAsync().ConfigureAwait(false);
            publishedResolver = await RuntimeDataResolver.PrepareAsync(route, true, null, CancellationToken.None).ConfigureAwait(false);
            publishedResolver.Publish();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _ = GameService<TrackWorld>.Set(null, previousWorld);
            _ = GameService<RuntimeDataResolver>.Set(null, previousResolver);
        }

        [TestMethod]
        public async Task PrepareAsyncLeavesBothPublishedServicesUnchanged()
        {
            RouteModel route = await CreateRouteAsync().ConfigureAwait(false);

            _ = await RuntimeDataResolver.PrepareAsync(route, false, null, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual((publishedResolver.TrackWorld, publishedResolver), (TrackWorld.Instance, RuntimeDataResolver.Instance));
        }

        [TestMethod]
        public async Task PrepareAsyncBuildsRailAndRoadGeometryFromNativeModels()
        {
            RouteModel route = await CreateRouteAsync().ConfigureAwait(false);

            RuntimeDataResolver prepared = await RuntimeDataResolver.PrepareAsync(route, true, null, CancellationToken.None).ConfigureAwait(false);

            Assert.HasCount(2, prepared.TrackWorld.SectionGeometry);
        }

        [TestMethod]
        public async Task PrepareAsyncKeepsLoadedNativeModelsAndRequestedSettings()
        {
            RouteModel route = await CreateRouteAsync().ConfigureAwait(false);

            RuntimeDataResolver prepared = await RuntimeDataResolver.PrepareAsync(route, false, null, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual((route, await route.GetTrackSectionModel(CancellationToken.None).ConfigureAwait(false),
                await route.GetSignalConfigurationModel(CancellationToken.None).ConfigureAwait(false), false),
                (prepared.RouteData, prepared.TrackSections, prepared.SignalConfiguration, prepared.MetricUnits));
        }

        [TestMethod]
        public async Task PublishRegistersExactPreparedWorldAndResolver()
        {
            RouteModel route = await CreateRouteAsync().ConfigureAwait(false);
            RuntimeDataResolver prepared = await RuntimeDataResolver.PrepareAsync(route, false, null, CancellationToken.None).ConfigureAwait(false);

            prepared.Publish();

            Assert.AreEqual((prepared.TrackWorld, prepared), (TrackWorld.Instance, RuntimeDataResolver.Instance));
        }

        [TestMethod]
        public async Task InitializePublishesMatchingWorldAndRouteResolver()
        {
            RouteModel route = await CreateRouteAsync().ConfigureAwait(false);

            await RuntimeDataResolver.Initialize(route, false, null, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual((route, RuntimeDataResolver.Instance.TrackWorld, false),
                (RuntimeDataResolver.Instance.RouteData, TrackWorld.Instance, RuntimeDataResolver.Instance.MetricUnits));
        }

        [TestMethod]
        public async Task PrepareAsyncWithCanceledTokenLeavesBothPublishedServicesUnchanged()
        {
            RouteModel route = await CreateRouteAsync().ConfigureAwait(false);
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                await cancellation.CancelAsync().ConfigureAwait(false);

                _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                    RuntimeDataResolver.PrepareAsync(route, true, null, cancellation.Token)).ConfigureAwait(false);
            }

            Assert.AreEqual((publishedResolver.TrackWorld, publishedResolver), (TrackWorld.Instance, RuntimeDataResolver.Instance));
        }

        [TestMethod]
        public async Task InitializeWithCanceledTokenLeavesBothPublishedServicesUnchanged()
        {
            RouteModel route = await CreateRouteAsync().ConfigureAwait(false);
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                await cancellation.CancelAsync().ConfigureAwait(false);

                _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                    RuntimeDataResolver.Initialize(route, true, null, cancellation.Token)).ConfigureAwait(false);
            }

            Assert.AreEqual((publishedResolver.TrackWorld, publishedResolver), (TrackWorld.Instance, RuntimeDataResolver.Instance));
        }

        [TestMethod]
        [DataRow("TrackSections")]
        [DataRow("Track")]
        [DataRow("Signals")]
        public async Task PrepareAsyncCanceledWaitDoesNotPoisonSharedModelCache(string modelKind)
        {
            RouteModel route = await CreateRouteAsync().ConfigureAwait(false);
            (Task sharedRead, Action completeRead, _) = await DelayNativeReadAsync(route, modelKind).ConfigureAwait(false);
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                Task<RuntimeDataResolver> preparation = RuntimeDataResolver.PrepareAsync(route, true, null, cancellation.Token);
                await cancellation.CancelAsync().ConfigureAwait(false);

                try
                {
                    _ = await Assert.ThrowsAsync<OperationCanceledException>(() => preparation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken)).ConfigureAwait(false);
                }
                finally
                {
                    completeRead();
                }
                _ = await RuntimeDataResolver.PrepareAsync(route, true, null, CancellationToken.None).ConfigureAwait(false);

                Assert.AreSame(sharedRead, GetNativeRead(route, modelKind));
            }
        }

        [TestMethod]
        public async Task PrepareAsyncCanceledWaitLeavesBothPublishedServicesUnchanged()
        {
            RouteModel route = await CreateRouteAsync().ConfigureAwait(false);
            (_, Action completeRead, _) = await DelayNativeReadAsync(route, "Track").ConfigureAwait(false);
            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                Task<RuntimeDataResolver> preparation = RuntimeDataResolver.PrepareAsync(route, true, null, cancellation.Token);
                await cancellation.CancelAsync().ConfigureAwait(false);

                try
                {
                    _ = await Assert.ThrowsAsync<OperationCanceledException>(() => preparation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken)).ConfigureAwait(false);

                    Assert.AreEqual((publishedResolver.TrackWorld, publishedResolver), (TrackWorld.Instance, RuntimeDataResolver.Instance));
                }
                finally
                {
                    completeRead();
                }
            }
        }

        [TestMethod]
        [DataRow("TrackSections")]
        [DataRow("Track")]
        [DataRow("Signals")]
        public async Task PrepareAsyncFaultLeavesBothPublishedServicesUnchanged(string modelKind)
        {
            RouteModel route = await CreateRouteAsync().ConfigureAwait(false);
            (_, _, Action<Exception> failRead) = await DelayNativeReadAsync(route, modelKind).ConfigureAwait(false);
            Task<RuntimeDataResolver> preparation = RuntimeDataResolver.PrepareAsync(route, true, null, CancellationToken.None);

            failRead(new InvalidOperationException("Native model read failed."));
            _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => preparation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken)).ConfigureAwait(false);

            Assert.AreEqual((publishedResolver.TrackWorld, publishedResolver), (TrackWorld.Instance, RuntimeDataResolver.Instance));
        }

        private static async Task<RouteModel> CreateRouteAsync()
        {
            RouteModel route = new RouteModel(WorldLocation.None) { Id = Guid.NewGuid().ToString("N") };
            TrackSectionModel sections = new TrackSectionModel
            {
                Id = route.Id,
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1,
                    new TrackSection { SectionIndex = 1, Gauge = 1.435f, Length = 100 }),
            };
            TrackModel track = new TrackModel
            {
                Id = route.Id,
                TrackDatabase = CreateDatabase(0),
                RoadDatabase = CreateDatabase(200),
            };

            // TestEnvironment redirects this native persistence to the assembly's isolated content root.
            await TestModelHandler<TrackSectionModel>.SaveAsync(sections, route).ConfigureAwait(false);
            await TestModelHandler<TrackModel>.SaveAsync(track, route).ConfigureAwait(false);
            await TestModelHandler<SignalConfigurationModel>.SaveAsync(new SignalConfigurationModel { Id = route.Id }, route).ConfigureAwait(false);
            return route;
        }

        private static TrackDatabase CreateDatabase(float x)
        {
            WorldLocation start = new WorldLocation(new Tile(0, 0), new Vector3(x, 0, 0));
            WorldLocation end = new WorldLocation(new Tile(0, 0), new Vector3(x, 0, 100));
            VectorSectionNode section = new VectorSectionNode(start, new Tile(0, 0), Vector3.Zero, end) { NodeIndex = 1 };
            VectorNode node = new VectorNode(start, new Tile(0, 0), end)
            {
                NodeIndex = 1,
                VectorSections = ImmutableArray.Create(section),
            };
            return new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, node),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), new TrackNodeConnectorIndex()),
            };
        }

        private static async Task<(Task Read, Action Complete, Action<Exception> Fail)> DelayNativeReadAsync(RouteModel route, string modelKind)
        {
            TrackSectionModel sections = await route.GetTrackSectionModel(CancellationToken.None).ConfigureAwait(false);
            TrackModel track = await route.GetTrackModel(CancellationToken.None).ConfigureAwait(false);
            SignalConfigurationModel signals = await route.GetSignalConfigurationModel(CancellationToken.None).ConfigureAwait(false);

            // Earlier reads must already be complete so PrepareAsync reaches the pending task before the test cancels or faults it.
            return modelKind switch
            {
                "TrackSections" => DelayRead(route, sections),
                "Track" => DelayRead(route, track),
                "Signals" => DelayRead(route, signals),
                _ => throw new ArgumentOutOfRangeException(nameof(modelKind)),
            };
        }

        private static (Task Read, Action Complete, Action<Exception> Fail) DelayRead<TModel>(RouteModel route, TModel model) where TModel : ModelBase
        {
            TaskCompletionSource<TModel> completion = new TaskCompletionSource<TModel>(TaskCreationOptions.RunContinuationsAsynchronously);
            TestModelHandler<TModel>.SetLoadTask(route.Id, completion.Task);
            return (completion.Task, () => completion.SetResult(model), completion.SetException);
        }

        private static Task GetNativeRead(RouteModel route, string modelKind)
        {
            return modelKind switch
            {
                "TrackSections" => route.GetTrackSectionModel(CancellationToken.None),
                "Track" => route.GetTrackModel(CancellationToken.None),
                "Signals" => route.GetSignalConfigurationModel(CancellationToken.None),
                _ => throw new ArgumentOutOfRangeException(nameof(modelKind)),
            };
        }

        private abstract class TestModelHandler<TModel> : ContentHandlerBase<TModel> where TModel : ModelBase
        {
            public static Task SaveAsync(TModel model, RouteModel route) => Create(model, route, CancellationToken.None);

            public static void SetLoadTask(string key, Task<TModel> loadTask) => modelTaskCache[key] = loadTask;
        }

        public TestContext TestContext { get; set; }
    }
}