using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models.Handler
{
    [TestClass]
    public class NativeRouteCacheIdentityTests
    {
        private readonly List<RouteModel> routes = new List<RouteModel>();
        private readonly Dictionary<string, byte[]> savedFiles = new Dictionary<string, byte[]>();

        [TestCleanup]
        public void Cleanup()
        {
            foreach (RouteModel route in routes)
            {
                NativeModelCache<TrackModel>.Remove(route);
                NativeModelCache<TrackSectionModel>.Remove(route);
                NativeModelCache<SignalConfigurationModel>.Remove(route);
            }
        }

        [TestMethod]
        [DataRow("Track")]
        [DataRow("TrackSections")]
        [DataRow("Signals")]
        public async Task SameRouteIdInDifferentFoldersLoadsIndependentNativeFiles(string modelKind)
        {
            string routeId = "Route" + Guid.NewGuid().ToString("N");
            RouteModel first = await CreateRouteAsync("First" + routeId, routeId, 101).ConfigureAwait(false);
            RouteModel second = await CreateRouteAsync("Second" + routeId, routeId, 202).ConfigureAwait(false);

            ModelBase firstRead = await ReadAsync(first, modelKind).ConfigureAwait(false);
            ModelBase secondRead = await ReadAsync(second, modelKind).ConfigureAwait(false);

            AssertPayload(firstRead, 101);
            AssertPayload(secondRead, 202);
            Assert.AreSame(first, firstRead.Parent);
            Assert.AreSame(second, secondRead.Parent);
            Assert.AreSame(firstRead, await ReadAsync(first, modelKind).ConfigureAwait(false));
            Assert.AreSame(secondRead, await ReadAsync(second, modelKind).ConfigureAwait(false));
            AssertFilesUnchanged();
        }

        [TestMethod]
        [DataRow("Track")]
        [DataRow("TrackSections")]
        [DataRow("Signals")]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task ConcurrentSameIdReadsStayInTheirOwningFolders(string modelKind)
        {
            string routeId = "Route" + Guid.NewGuid().ToString("N");
            RouteModel first = await CreateRouteAsync("First" + routeId, routeId, 101).ConfigureAwait(false);
            RouteModel second = await CreateRouteAsync("Second" + routeId, routeId, 202).ConfigureAwait(false);
            TaskCompletionSource start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<ModelBase> ReadWhenReleasedAsync(RouteModel route)
            {
                await start.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                return await ReadAsync(route, modelKind).ConfigureAwait(false);
            }

            Task<ModelBase> firstRead = ReadWhenReleasedAsync(first);
            Task<ModelBase> secondRead = ReadWhenReleasedAsync(second);
            Assert.IsFalse(firstRead.IsCompleted);
            Assert.IsFalse(secondRead.IsCompleted);
            start.SetResult();
            ModelBase[] models = await Task.WhenAll(firstRead, secondRead).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

            AssertPayload(models[0], 101);
            AssertPayload(models[1], 202);
            Assert.AreSame(first, models[0].Parent);
            Assert.AreSame(second, models[1].Parent);
            Assert.AreSame(models[0], await ReadAsync(first, modelKind).ConfigureAwait(false));
            Assert.AreSame(models[1], await ReadAsync(second, modelKind).ConfigureAwait(false));
            AssertFilesUnchanged();
        }

        [TestMethod]
        [DataRow("Track")]
        [DataRow("TrackSections")]
        [DataRow("Signals")]
        public async Task QualifiedCacheIdentityRemainsOrdinalIgnoreCase(string modelKind)
        {
            string routeId = "Route" + Guid.NewGuid().ToString("N");
            RouteModel route = await CreateRouteAsync("Folder" + routeId, routeId, 101).ConfigureAwait(false);
            RouteModel alias = new RouteModel(WorldLocation.None) { Id = route.Id.ToUpperInvariant() };
            alias.Initialize(new FolderModel(route.Parent.Id.ToUpperInvariant(), route.Parent.ContentPath, new ContentModel()));

            ModelBase original = await ReadAsync(route, modelKind).ConfigureAwait(false);

            Assert.AreSame(original, await ReadAsync(alias, modelKind).ConfigureAwait(false));
            AssertPayload(original, 101);
            AssertFilesUnchanged();
        }

        [TestMethod]
        [DataRow("Track")]
        [DataRow("TrackSections")]
        [DataRow("Signals")]
        public async Task FaultedReadRetriesOnlyItsOwnNamespace(string modelKind)
        {
            string routeId = "Route" + Guid.NewGuid().ToString("N");
            RouteModel first = await CreateRouteAsync("First" + routeId, routeId, 101).ConfigureAwait(false);
            RouteModel second = await CreateRouteAsync("Second" + routeId, routeId, 202).ConfigureAwait(false);
            ModelBase secondRead = await ReadAsync(second, modelKind).ConfigureAwait(false);
            switch (modelKind)
            {
                case "Track": NativeModelCache<TrackModel>.Fault(first); break;
                case "TrackSections": NativeModelCache<TrackSectionModel>.Fault(first); break;
                case "Signals": NativeModelCache<SignalConfigurationModel>.Fault(first); break;
                default: throw new ArgumentOutOfRangeException(nameof(modelKind));
            }

            ModelBase retried = await ReadAsync(first, modelKind).ConfigureAwait(false);

            AssertPayload(retried, 101);
            Assert.AreSame(first, retried.Parent);
            Assert.AreSame(retried, await ReadAsync(first, modelKind).ConfigureAwait(false));
            Assert.AreSame(secondRead, await ReadAsync(second, modelKind).ConfigureAwait(false));
            AssertPayload(secondRead, 202);
            AssertFilesUnchanged();
        }

        [TestMethod]
        [DataRow("Track")]
        [DataRow("TrackSections")]
        [DataRow("Signals")]
        public async Task ImportPublicationStaysInOwningNamespaceWithoutLegacyContent(string modelKind)
        {
            string routeId = "Route" + Guid.NewGuid().ToString("N");
            RouteModel first = await CreateRouteAsync("First" + routeId, routeId, 101).ConfigureAwait(false);
            RouteModel second = await CreateRouteAsync("Second" + routeId, routeId, 202).ConfigureAwait(false);

            // Exercise the real publication methods, not conversion: canceled track import avoids source reads,
            // missing sections fault, and missing signal configuration completes with null.
            Task imported = modelKind switch
            {
                "Track" => TrackModelImportHandler.ExpandTrackModel(first, new CancellationToken(true)),
                "TrackSections" => TrackSectionModelImportHandler.ExpandTrackSectionModel(first, CancellationToken.None),
                "Signals" => SignalConfigurationModelImportHandler.ExpandSignalConfigurationModel(first, CancellationToken.None),
                _ => throw new ArgumentOutOfRangeException(nameof(modelKind)),
            };

            Assert.AreSame(imported, CachedTask(first, modelKind));
            Assert.IsNull(CachedTask(second, modelKind));
            if (modelKind == "Track")
                _ = await Assert.ThrowsAsync<OperationCanceledException>(() => imported).ConfigureAwait(false);
            else if (modelKind == "TrackSections")
                _ = await Assert.ThrowsAsync<IOException>(() => imported).ConfigureAwait(false);
            else
                await imported.ConfigureAwait(false);

            AssertPayload(await ReadAsync(second, modelKind).ConfigureAwait(false), 202);
            if (modelKind == "TrackSections")
                AssertPayload(await ReadAsync(first, modelKind).ConfigureAwait(false), 101);
            else
            {
                // Direct handler cancellation/null-result semantics are unchanged, unlike runtime waiter cancellation.
                Task nativeRead = modelKind == "Track" ? first.GetTrackModel(CancellationToken.None) : first.GetSignalConfigurationModel(CancellationToken.None);
                Assert.AreSame(imported, nativeRead);
            }
            AssertFilesUnchanged();
        }

        private async Task<RouteModel> CreateRouteAsync(string folderId, string routeId, int marker)
        {
            // TestEnvironment redirects the native store; no installed or source content is used.
            FolderModel folder = new FolderModel(folderId, Path.Combine(ModelStore.ContentRoot, "LegacySources", folderId), new ContentModel());
            RouteModel route = new RouteModel(WorldLocation.None)
            {
                Id = routeId,
                Tags = ImmutableDictionary<string, string>.Empty.Add(RouteModelImportHandler.SourceNameKey, routeId),
            };
            route.Initialize(folder);
            routes.Add(route);

            await SaveAsync(new TrackModel
            {
                Id = routeId,
                TrackDatabase = new TrackDatabase
                {
                    TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex { NodeIndex = marker }),
                },
            }, route, ".tmodel").ConfigureAwait(false);
            await SaveAsync(new TrackSectionModel
            {
                Id = routeId,
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1, new TrackSection { SectionIndex = 1, Length = marker }),
            }, route, ".tsection").ConfigureAwait(false);
            await SaveAsync(new SignalConfigurationModel
            {
                Id = routeId,
                ScriptFiles = ImmutableArray.Create(marker + ".cs"),
            }, route, ".sigcfg").ConfigureAwait(false);
            return route;
        }

        private async Task SaveAsync<TModel>(TModel model, RouteModel route, string extension) where TModel : ModelBase
        {
            await NativeModelCache<TModel>.SaveAsync(model, route).ConfigureAwait(false);
            string file = ModelFileResolver<TModel>.FilePath(model) + ContentHandlerBase<TModel>.SaveStateExtension;
            string expected = Path.Combine(ModelStore.ContentRoot, "Content", route.Parent.Id, route.Id,
                route.Id + extension + ContentHandlerBase<TModel>.SaveStateExtension);
            Assert.AreEqual(expected, file);
            Assert.AreEqual(file, ModelFileResolver<TModel>.FilePath(route.Id, route) + ContentHandlerBase<TModel>.SaveStateExtension);
            savedFiles.Add(file, await File.ReadAllBytesAsync(file, TestContext.CancellationToken).ConfigureAwait(false));
        }

        private static async Task<ModelBase> ReadAsync(RouteModelHeader route, string modelKind)
        {
            return modelKind switch
            {
                "Track" => await route.GetTrackModel(CancellationToken.None).ConfigureAwait(false),
                "TrackSections" => await route.GetTrackSectionModel(CancellationToken.None).ConfigureAwait(false),
                "Signals" => await route.GetSignalConfigurationModel(CancellationToken.None).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(modelKind)),
            };
        }

        private static Task CachedTask(RouteModel route, string modelKind)
        {
            return modelKind switch
            {
                "Track" => NativeModelCache<TrackModel>.Get(route),
                "TrackSections" => NativeModelCache<TrackSectionModel>.Get(route),
                "Signals" => NativeModelCache<SignalConfigurationModel>.Get(route),
                _ => throw new ArgumentOutOfRangeException(nameof(modelKind)),
            };
        }

        private static void AssertPayload(ModelBase model, int marker)
        {
            Assert.IsNotNull(model);
            switch (model)
            {
                case TrackModel track:
                    Assert.AreEqual(marker, track.TrackDatabase.TrackNodeConnectors[0].NodeIndex);
                    break;
                case TrackSectionModel sections:
                    Assert.AreEqual((float)marker, sections.TrackSections[1].Length);
                    break;
                case SignalConfigurationModel signals:
                    Assert.HasCount(1, signals.ScriptFiles);
                    Assert.AreEqual(marker + ".cs", signals.ScriptFiles[0]);
                    break;
                default:
                    Assert.Fail("Unexpected native model type.");
                    break;
            }
        }

        private void AssertFilesUnchanged()
        {
            foreach ((string file, byte[] bytes) in savedFiles)
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(file), file);
        }

        private abstract class NativeModelCache<TModel> : ContentHandlerBase<TModel> where TModel : ModelBase
        {
            internal static Task SaveAsync(TModel model, RouteModel route) => Create(model, route, CancellationToken.None);

            internal static Task<TModel> Get(RouteModel route) => modelTaskCache.TryGetValue(route.Hierarchy(route.Id), out Task<TModel> task) ? task : null;

            internal static void Fault(RouteModel route)
            {
                Task<TModel> failed = Task.FromException<TModel>(new IOException("Native model read failed."));
                _ = failed.Exception;
                modelTaskCache[route.Hierarchy(route.Id)] = failed;
            }

            internal static void Remove(RouteModel route) => modelTaskCache.TryRemove(route.Hierarchy(route.Id), out _);
        }

        public TestContext TestContext { get; set; }
    }
}
