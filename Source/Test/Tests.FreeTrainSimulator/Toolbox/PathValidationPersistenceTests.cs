using System;
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
                });
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

        private static JunctionNode CreateJunctionNode(int nodeIndex)
        {
            return new JunctionNode(new WorldLocation(new Tile(0, 0), new Vector3(nodeIndex * 100, 0, 0)),
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
    }
}
