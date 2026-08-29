using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

using EditorTrainPath = FreeTrainSimulator.Graphics.MapView.Widgets.EditorTrainPath;

namespace Tests.FreeTrainSimulator.Toolbox
{
    // Integration coverage for path validation and guarded persistence using the assembly-wide isolated model store.
    [TestClass]
    public class PathValidationPersistenceTests
    {
        [TestMethod]
        public async Task WhenValidateRoutePathsRunsThenReloadedHeadersCarryPersistedValidationState()
        {
            RouteModelHeader route = await SeedRouteWithPathsAsync().ConfigureAwait(false);

            _ = await PathEditor.ValidateRoutePaths(route, null, true, CancellationToken.None).ConfigureAwait(false);

            ImmutableArray<PathModelHeader> reloaded = await route.GetPaths(CancellationToken.None).ConfigureAwait(false);
            PathModelHeader emptyPath = reloaded.Single(p => p.Id == "empty-path");
            PathModelHeader linearPath = reloaded.Single(p => p.Id == "linear-path");

            Assert.AreEqual(PathValidationState.Invalid, emptyPath.ValidationState);
            Assert.AreEqual(PathValidationState.Valid, linearPath.ValidationState);
        }

        [TestMethod]
        public async Task WhenValidPathIsSavedThenNormalizedContentIsPersisted()
        {
            RouteModel route = CreateRoute();
            PathModel path = CreateLinearPath("valid-path");

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(path, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()).ConfigureAwait(false);

            PathModelHeader savedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(savedPath => savedPath.Id == path.Id);
            PathModel savedPath = await savedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(result.PersistenceAllowed);
            Assert.AreEqual(PathValidationState.Valid, savedPath.ValidationState);
            Assert.AreSequenceEqual(result.PathModel.PathNodes, savedPath.PathNodes);
        }

        [TestMethod]
        public async Task WhenWarningOnlyPathIsSavedThenContentIsPersisted()
        {
            RouteModel route = CreateRoute();
            PathModel path = new PathModel
            {
                Id = "warning-path",
                Name = "Warning Path",
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(100, PathNodeType.Start, 1) with { NodeIndex = 1 },
                    CreatePathNode(200, PathNodeType.End, -1) with { NodeIndex = 2 }),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(path, route,
                CreateAmbiguousRouteTrackWorld()).ConfigureAwait(false);

            PathModelHeader savedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(savedPath => savedPath.Id == path.Id);
            PathModel savedPath = await savedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Severity == PathRouteDiagnosticSeverity.Warning));
            Assert.IsTrue(result.Diagnostics.All(diagnostic => diagnostic.Severity < PathRouteDiagnosticSeverity.Error),
                string.Join("; ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Severity}:{diagnostic.Code}:{diagnostic.Message}")));
            Assert.AreSequenceEqual(result.PathModel.PathNodes, savedPath.PathNodes);
            Assert.IsTrue(savedPath.PathNodes.Length > path.PathNodes.Length);
        }

        [TestMethod]
        public async Task WhenErrorPathSaveIsBlockedThenExistingPersistedContentIsUnchanged()
        {
            RouteModel route = CreateRoute();
            PathModel persisted = CreateLinearPath("protected-path");
            _ = await route.Save(persisted).ConfigureAwait(false);
            PathModel invalid = persisted with
            {
                Name = "Invalid Replacement",
                PathNodes = persisted.PathNodes.SetItem(0, persisted.PathNodes[0] with { NodeType = PathNodeType.Start | PathNodeType.Junction }),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(invalid, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()).ConfigureAwait(false);

            PathModelHeader reloadedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == persisted.Id);
            PathModel reloaded = await reloadedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(result.PersistenceAllowed);
            Assert.AreEqual(persisted.Name, reloaded.Name);
            Assert.AreSequenceEqual(persisted.PathNodes, reloaded.PathNodes);
        }

        [TestMethod]
        public async Task WhenFatalPathSaveIsBlockedThenNoContentIsPersisted()
        {
            RouteModel route = CreateRoute();
            PathModel invalid = CreateLinearPath("fatal-path") with
            {
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(0, PathNodeType.Start, 4),
                    CreatePathNode(100, PathNodeType.End, -1)),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(invalid, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()).ConfigureAwait(false);

            ImmutableArray<PathModelHeader> paths = await route.GetPaths(CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(result.PersistenceAllowed);
            Assert.IsFalse(paths.Any(path => path.Id == invalid.Id));
        }

        [TestMethod]
        public async Task WhenPathHasSidingLinkBelowMinusOneThenSaveIsBlocked()
        {
            RouteModel route = CreateRoute();
            PathModel invalid = CreateLinearPath("invalid-siding-link") with
            {
                PathNodes = CreateLinearPath("source").PathNodes.SetItem(0,
                    CreateLinearPath("source").PathNodes[0] with { NextSidingNode = -2 }),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(invalid, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()).ConfigureAwait(false);

            Assert.IsFalse(result.PersistenceAllowed);
            Assert.IsFalse((await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Any(path => path.Id == invalid.Id));
        }

        [TestMethod]
        public async Task WhenValidPassingBranchIsSavedThenItRoundTripsWithResolverValidation()
        {
            RouteModel route = CreateRoute();
            PathModel source = CreateLinearPath("passing-round-trip");
            PathModel branch = source with
            {
                PathNodes = source.PathNodes.SetItem(0, source.PathNodes[0] with { NextSidingNode = 1 }),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(branch, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()).ConfigureAwait(false);
            PathModelHeader savedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == branch.Id);
            PathModel reloaded = await savedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);

            Assert.IsTrue(result.PersistenceAllowed);
            Assert.AreEqual(PathValidationState.Valid, reloaded.ValidationState);
            Assert.IsTrue(reloaded.PathNodes.Any(node => node.NextSidingNode >= 0));
            Assert.IsTrue(PathRouteResolver.Resolve(reloaded, TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld(), CancellationToken.None).IsValid);
        }

        [TestMethod]
        public async Task WhenGeneratedPassingPathIsReloadedThenRuntimeConsumerRetainsRejoiningBranch()
        {
            RouteModel route = CreateRoute();
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSidingTrackWorld();
            PathModel source = CreateRepresentativePassingPath("generated-passing-consumer", trackWorld);

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(source, route, trackWorld).ConfigureAwait(false);
            PathModelHeader savedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == source.Id);
            PathModel reloaded = await savedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            EditorTrainPath runtimePath = new EditorTrainPath(reloaded, trackWorld);
            TrainPathPointBase branchStart = runtimePath.PathPoints.Single(point => point.NextMainNode > -1 && point.NextSidingNode > -1);

            Assert.IsTrue(result.PersistenceAllowed && PassingBranchRejoinsMain(runtimePath.PathPoints, branchStart));
        }

        [TestMethod]
        public async Task WhenGeneratedMainPathIsReloadedThenResolverRetainsRepresentativeRoute()
        {
            RouteModel route = CreateRoute();
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
            PathModel source = CreateLinearPath("generated-main-round-trip");

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(source, route, trackWorld).ConfigureAwait(false);
            PathModelHeader savedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == source.Id);
            PathModel reloaded = await savedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            PathRouteResolution resolution = PathRouteResolver.Resolve(reloaded, trackWorld, CancellationToken.None);

            Assert.IsTrue(result.PersistenceAllowed && resolution.IsValid,
                string.Join("; ", resolution.Diagnostics.Select(diagnostic => $"{diagnostic.Severity}:{diagnostic.Code}:{diagnostic.Message}")));
        }

        [TestMethod]
        public async Task WhenGeneratedMainPathIsReloadedThenRuntimeConsumerRetainsMainConnectivity()
        {
            RouteModel route = CreateRoute();
            TrackWorld trackWorld = TrackWorldTestFixture.CreateDeadEndTrackWorld();
            PathModel source = CreateAnchoredEndpointPath("generated-main-consumer", trackWorld);

            _ = await PathEditor.SaveValidatedPath(source, route, trackWorld).ConfigureAwait(false);
            PathModelHeader savedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == source.Id);
            PathModel reloaded = await savedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            EditorTrainPath runtimePath = new EditorTrainPath(reloaded, trackWorld);

            Assert.IsTrue(MainChainReachesEnd(runtimePath.PathPoints));
        }

        [TestMethod]
        public async Task WhenChangedPathIdIsSavedThenOriginalAndSaveAsPathsArePersistedSeparately()
        {
            RouteModel route = CreateRoute();
            PathModel original = CreateLinearPath("original-path") with { Name = "Original" };
            _ = await route.Save(original).ConfigureAwait(false);
            PathModel saveAs = original with { Id = "copied-path", Name = "Copy" };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(saveAs, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()).ConfigureAwait(false);
            ImmutableArray<PathModelHeader> paths = await route.GetPaths(CancellationToken.None).ConfigureAwait(false);
            PathModel persistedOriginal = await paths.Single(path => path.Id == original.Id).GetExtended(CancellationToken.None).ConfigureAwait(false);
            PathModel persistedCopy = await paths.Single(path => path.Id == saveAs.Id).GetExtended(CancellationToken.None).ConfigureAwait(false);

            Assert.IsTrue(result.PersistenceAllowed);
            Assert.AreEqual("Original", persistedOriginal.Name);
            Assert.AreEqual("Copy", persistedCopy.Name);
            Assert.AreSequenceEqual(original.PathNodes, persistedOriginal.PathNodes);
            Assert.AreSequenceEqual(saveAs.PathNodes, persistedCopy.PathNodes);
        }

        [TestMethod]
        public async Task WhenConfirmedSaveAsOverwritesExistingIdThenOnlyTheTargetPathIsReplaced()
        {
            RouteModel route = CreateRoute();
            PathModel original = CreateLinearPath("original-path") with { Name = "Original" };
            PathModel existingTarget = CreateLinearPath("target-path") with { Name = "Existing Target" };
            _ = await route.Save(original).ConfigureAwait(false);
            _ = await route.Save(existingTarget).ConfigureAwait(false);
            PathModel replacement = original with { Id = existingTarget.Id, Name = "Replacement Target" };

            _ = await PathEditor.SaveValidatedPath(replacement, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()).ConfigureAwait(false);
            ImmutableArray<PathModelHeader> paths = await route.GetPaths(CancellationToken.None).ConfigureAwait(false);
            PathModel persistedOriginal = await paths.Single(path => path.Id == original.Id).GetExtended(CancellationToken.None).ConfigureAwait(false);
            PathModel persistedTarget = await paths.Single(path => path.Id == existingTarget.Id).GetExtended(CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("Original", persistedOriginal.Name);
            Assert.AreEqual("Replacement Target", persistedTarget.Name);
        }

        [TestMethod]
        public async Task WhenSaveNormalizationFailsThenExistingPersistedContentIsUnchanged()
        {
            RouteModel route = CreateRoute();
            PathModel persisted = CreateLinearPath("normalization-protected-path");
            _ = await route.Save(persisted).ConfigureAwait(false);
            PathModel nonRejoiningPassingPath = new PathModel(persisted)
            {
                Name = "Unpersisted Replacement",
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(0, PathNodeType.Start, 1) with { NextSidingNode = 2 },
                    CreatePathNode(50, PathNodeType.Intermediate, 3),
                    CreatePathNode(25, PathNodeType.Intermediate, -1),
                    CreatePathNode(100, PathNodeType.End, -1)),
            };

            PathPersistenceValidationResult result = await PathEditor.SaveValidatedPath(nonRejoiningPassingPath, route,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()).ConfigureAwait(false);

            PathModelHeader reloadedHeader = (await route.GetPaths(CancellationToken.None).ConfigureAwait(false)).Single(path => path.Id == persisted.Id);
            PathModel reloaded = await reloadedHeader.GetExtended(CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(result.PersistenceAllowed);
            Assert.AreEqual(persisted.Name, reloaded.Name);
            Assert.AreSequenceEqual(persisted.PathNodes, reloaded.PathNodes);
        }

        [TestMethod]
        public async Task WhenAtomicReplacementFailsThenExistingPersistedBytesAreUnchanged()
        {
            RouteModel route = CreateRoute();
            PathModel persisted = CreateLinearPath("atomic-replacement-path");
            _ = await route.Save(persisted).ConfigureAwait(false);
            string targetFileName = ModelFileResolver<PathModelHeader>.FilePath(persisted) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            byte[] originalBytes = await File.ReadAllBytesAsync(targetFileName).ConfigureAwait(false);
            PathModel replacement = persisted with { Name = "Replacement That Must Not Persist" };

            using (FileStream targetLock = new FileStream(targetFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await route.Save(replacement).ContinueWith(completedSave =>
                {
                    Assert.IsTrue(completedSave.IsFaulted);
                    Assert.IsTrue(completedSave.Exception?.InnerException is IOException or UnauthorizedAccessException);
                }, TestContext.CancellationToken, TaskContinuationOptions.None, TaskScheduler.Default).ConfigureAwait(false);
            }

            byte[] persistedBytes = await File.ReadAllBytesAsync(targetFileName).ConfigureAwait(false);
            Assert.AreSequenceEqual(originalBytes, persistedBytes);
        }

        private static async Task<RouteModelHeader> SeedRouteWithPathsAsync()
        {
            RouteModel routeModel = CreateRoute();

            PathModel emptyPath = new PathModel() { Id = "empty-path", Name = "Empty Path" };
            PathModel linearPath = new PathModel()
            {
                Id = "linear-path",
                Name = "Linear Path",
                PathNodes = ImmutableArray.Create(
                    new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero)) { NodeType = PathNodeType.Start, NextMainNode = 1 },
                    new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero)) { NodeType = PathNodeType.End, NextMainNode = -1 }),
            };

            _ = await routeModel.Save(emptyPath).ConfigureAwait(false);
            _ = await routeModel.Save(linearPath).ConfigureAwait(false);
            return routeModel;
        }

        private static RouteModel CreateRoute()
        {
            string uniqueSuffix = Guid.NewGuid().ToString("N");
            ContentModel contentModel = new ContentModel();
            FolderModel folderModel = new FolderModel($"Folder-{uniqueSuffix}", Path.Combine(Path.GetTempPath(), $"fts-content-{uniqueSuffix}"), contentModel);
            contentModel = contentModel with { ContentFolders = ImmutableArray.Create(folderModel) };
            contentModel.Initialize(null);

            RouteModel routeModel = new RouteModel(WorldLocation.None)
            {
                Id = $"Route-{uniqueSuffix}",
                Name = "Validation Test Route",
                RouteKey = "ValidationTestKey",
            };
            routeModel.Initialize(folderModel);
            return routeModel;
        }

        private static PathModel CreateLinearPath(string id)
        {
            return new PathModel
            {
                Id = id,
                Name = id,
                PathNodes = ImmutableArray.Create(
                    CreatePathNode(0, PathNodeType.Start, 1),
                    CreatePathNode(100, PathNodeType.End, -1)),
            };
        }

        private static PathModel CreateRepresentativePassingPath(string id, TrackWorld trackWorld)
        {
            return new PathModel
            {
                Id = id,
                Name = id,
                PathNodes = ImmutableArray.Create(
                    CreateAnchoredPathNode(trackWorld, 1, PathNodeType.Start, 1, 3),
                    CreateAnchoredPathNode(trackWorld, 4, PathNodeType.Intermediate, 2, -1),
                    CreateAnchoredPathNode(trackWorld, 2, PathNodeType.End, -1, -1),
                    CreateAnchoredPathNode(trackWorld, 5, PathNodeType.Intermediate, -1, 2)),
            };
        }

        private static bool MainChainReachesEnd(List<TrainPathPointBase> pathPoints)
        {
            int currentIndex = pathPoints.Select((point, index) => (point, index))
                .Single(item => item.point.NodeType.Includes(PathNodeType.Start)).index;
            HashSet<int> visited = new HashSet<int>();
            while (currentIndex >= 0 && currentIndex < pathPoints.Count && visited.Add(currentIndex))
            {
                TrainPathPointBase current = pathPoints[currentIndex];
                if (current.NodeType.Includes(PathNodeType.End))
                    return true;

                currentIndex = current.NextMainNode;
            }

            return false;
        }

        private static bool PassingBranchRejoinsMain(List<TrainPathPointBase> pathPoints, TrainPathPointBase branchStart)
        {
            HashSet<int> mainIndexes = new HashSet<int>();
            int mainIndex = pathPoints.Select((point, index) => (point, index)).Single(item => ReferenceEquals(item.point, branchStart)).index;
            while (mainIndex >= 0 && mainIndex < pathPoints.Count && mainIndexes.Add(mainIndex))
                mainIndex = pathPoints[mainIndex].NextMainNode;

            HashSet<int> branchIndexes = new HashSet<int>();
            int branchIndex = branchStart.NextSidingNode;
            while (branchIndex >= 0 && branchIndex < pathPoints.Count && branchIndexes.Add(branchIndex))
            {
                if (mainIndexes.Contains(branchIndex))
                    return true;

                branchIndex = pathPoints[branchIndex].NextSidingNode;
            }

            return false;
        }

        private static PathNode CreateAnchoredPathNode(TrackWorld trackWorld, int trackNodeIndex, PathNodeType nodeType,
            int nextMainNode, int nextSidingNode)
        {
            return new PathNode(trackWorld.TrackDatabase.TrackNodes[trackNodeIndex].Location)
            {
                NodeType = nodeType,
                NodeIndex = trackNodeIndex,
                NextMainNode = nextMainNode,
                NextSidingNode = nextSidingNode,
            };
        }

        private static PathModel CreateAnchoredEndpointPath(string id, TrackWorld trackWorld)
        {
            return new PathModel
            {
                Id = id,
                Name = id,
                PathNodes = ImmutableArray.Create(
                    new PathNode(trackWorld.TrackDatabase.TrackNodes[1].Location)
                    {
                        NodeType = PathNodeType.Start,
                        NodeIndex = 1,
                        NextMainNode = 1,
                        NextSidingNode = -1,
                    },
                    new PathNode(trackWorld.TrackDatabase.TrackNodes[2].Location)
                    {
                        NodeType = PathNodeType.End,
                        NodeIndex = 2,
                        NextMainNode = -1,
                        NextSidingNode = -1,
                    }),
            };
        }

        private static PathNode CreatePathNode(float x, PathNodeType nodeType, int nextMainNode)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(x, 0, 0)))
            {
                NodeType = nodeType,
                NodeIndex = 1,
                NextMainNode = nextMainNode,
                NextSidingNode = -1,
            };
        }

        private static TrackWorld CreateAmbiguousRouteTrackWorld()
        {
            TrackDatabase trackDatabase = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1, 100), CreateVectorNode(2, 200),
                    CreateJunctionNode(3), CreateJunctionNode(4)),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3, 4),
                    CreateConnectors(2, 3, 4), CreateConnectors(3, 1, 2), CreateConnectors(4, 1, 2)),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(trackDatabase);
            TrackModel trackModel = new TrackModel { TrackDatabase = trackDatabase };

            return TrackWorld.Initialize(null, trackModel, new TrackSectionModel());
        }

        private static VectorNode CreateVectorNode(int nodeIndex, float startX)
        {
            WorldLocation start = new WorldLocation(new Tile(0, 0), new Vector3(startX, 0, 0));
            WorldLocation end = new WorldLocation(new Tile(0, 0), new Vector3(startX + 100, 0, 0));
            return new VectorNode(start, new Tile(0, 0), end)
            {
                NodeIndex = nodeIndex,
            };
        }

        private static global::FreeTrainSimulator.Models.Track.JunctionNode CreateJunctionNode(int nodeIndex)
        {
            return new global::FreeTrainSimulator.Models.Track.JunctionNode(new WorldLocation(new Tile(0, 0), new Vector3(nodeIndex * 100, 0, 0)),
                new Tile(0, 0), Vector3.Zero) { NodeIndex = nodeIndex };
        }

        private static TrackNodeConnectorIndex CreateConnectors(int nodeIndex, params int[] linkedNodeIndexes)
        {
            return new TrackNodeConnectorIndex
            {
                NodeIndex = nodeIndex,
                TrackNodeConnectors = linkedNodeIndexes.Select(link => new TrackNodeConnector { Link = link }).ToImmutableArray(),
            };
        }

        public TestContext TestContext { get; set; }
    }
}
