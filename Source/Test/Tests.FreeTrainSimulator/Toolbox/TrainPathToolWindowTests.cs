using System.Collections.Immutable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.ToolWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Common;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class TrainPathToolWindowTests
    {
        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenSnapshotStaysEmpty()
        {
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(_ => { });
            trainPathToolWindow.Active = false;

            trainPathToolWindow.RefreshSnapshot();

            Assert.AreEqual(TrainPathSnapshot.Empty, trainPathToolWindow.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenActiveWithNoEditorRefreshSnapshotThenSnapshotStaysEmpty()
        {
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(_ => { });
            trainPathToolWindow.Active = true;

            trainPathToolWindow.RefreshSnapshot();

            Assert.AreEqual(TrainPathSnapshot.Empty, trainPathToolWindow.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenActiveWithNoEditorButPathsUpdatedThenSnapshotSurfacesPathsWithValidationState()
        {
            // Regression: after loading a route or running "Validate All" no path is being edited, yet the
            // available-paths list and its validation markers must still be published.
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(_ => { });
            trainPathToolWindow.Active = true;
            ImmutableArray<PathModelHeader> paths = ImmutableArray.Create(
                new PathModelHeader { Id = "p1", Name = "Alpha", ValidationState = PathValidationState.Valid },
                new PathModelHeader { Id = "p2", Name = "Beta", ValidationState = PathValidationState.Invalid });
            trainPathToolWindow.UpdatePaths(paths);

            trainPathToolWindow.RefreshSnapshot();

            TrainPathSnapshot snapshot = trainPathToolWindow.CaptureTrainPathSnapshot();
            Assert.AreEqual(2, snapshot.Paths.Length);
            Assert.AreEqual(PathValidationState.Valid, snapshot.Paths[0].ValidationState);
            Assert.AreEqual(PathValidationState.Invalid, snapshot.Paths[1].ValidationState);
        }

        [TestMethod]
        public void WhenPathsInvalidatedThenSnapshotClearsPreviousPaths()
        {
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(action => action());
            trainPathToolWindow.Active = true;
            trainPathToolWindow.UpdatePaths(ImmutableArray.Create(new PathModelHeader { Id = "old", Name = "Old Path" }));
            trainPathToolWindow.RefreshSnapshot();

            trainPathToolWindow.InvalidatePaths();
            trainPathToolWindow.RefreshSnapshot();

            Assert.IsTrue(trainPathToolWindow.CaptureTrainPathSnapshot().Paths.IsEmpty);
        }

        [TestMethod]
        public void WhenCurrentPathIsNotSavedThenBuildPathRowsAddsVirtualCurrentPathFirst()
        {
            ImmutableArray<PathModelHeader> savedPaths = ImmutableArray.Create(
                new PathModelHeader { Id = "saved", Name = "Saved Path", ValidationState = PathValidationState.Valid });
            PathModel currentPath = new PathModel
            {
                Id = "<New Path>",
                Name = "<New Path>",
                ValidationState = PathValidationState.NotValidated,
            };

            ImmutableArray<TrainPathListRow> rows = TrainPathToolWindow.BuildPathRows(savedPaths, currentPath);

            Assert.HasCount(2, rows);
            Assert.AreEqual("<New Path>", rows[0].Id);
            Assert.AreEqual(PathValidationState.NotValidated, rows[0].ValidationState);
            Assert.AreEqual("saved", rows[1].Id);
        }

        [TestMethod]
        public void WhenCurrentPathIsSavedThenBuildPathRowsDoesNotDuplicateIt()
        {
            ImmutableArray<PathModelHeader> savedPaths = ImmutableArray.Create(
                new PathModelHeader { Id = "path-1", Name = "Saved Path", ValidationState = PathValidationState.Valid });
            PathModel currentPath = new PathModel
            {
                Id = "path-1",
                Name = "Saved Path",
                ValidationState = PathValidationState.Valid,
            };

            ImmutableArray<TrainPathListRow> rows = TrainPathToolWindow.BuildPathRows(savedPaths, currentPath);

            Assert.HasCount(1, rows);
            Assert.AreEqual("path-1", rows[0].Id);
        }

        [TestMethod]
        public void WhenCurrentPathOverridesSavedPathThenBuildPathRowsUsesCurrentValidationState()
        {
            ImmutableArray<PathModelHeader> savedPaths = ImmutableArray.Create(
                new PathModelHeader { Id = "path-1", Name = "Saved Path", ValidationState = PathValidationState.Invalid });
            PathModel currentPath = new PathModel
            {
                Id = "path-1",
                Name = "Saved Path",
                ValidationState = PathValidationState.Valid,
            };

            ImmutableArray<TrainPathListRow> rows = TrainPathToolWindow.BuildPathRows(savedPaths, currentPath);

            Assert.HasCount(1, rows);
            Assert.AreEqual(PathValidationState.Valid, rows[0].ValidationState);
        }

        [TestMethod]
        public void WhenTransientPathExistsThenBuildPathRowsIncludesItBeforeSavedPaths()
        {
            ImmutableArray<PathModelHeader> savedPaths = ImmutableArray.Create(
                new PathModelHeader { Id = "saved", Name = "Saved Path", ValidationState = PathValidationState.Valid });
            ImmutableArray<PathModel> transientPaths = ImmutableArray.Create(new PathModel
            {
                Id = "edited",
                Name = "Edited Path",
                ValidationState = PathValidationState.NotValidated,
            });

            ImmutableArray<TrainPathListRow> rows = TrainPathToolWindow.BuildPathRows(savedPaths, transientPaths, null);

            Assert.HasCount(2, rows);
            Assert.AreEqual("edited", rows[0].Id);
            Assert.AreEqual("saved", rows[1].Id);
        }

        [TestMethod]
        public void WhenTransientPathExistsThenHasUnsavedPathChangesIsTrue()
        {
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(action => action());
            Dictionary<string, PathModel> transientPaths = (Dictionary<string, PathModel>)typeof(TrainPathToolWindow)
                .GetField("transientPaths", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(trainPathToolWindow);
            transientPaths.Add("edited", new PathModel { Id = "edited", Name = "Edited Path" });

            Assert.IsTrue(trainPathToolWindow.HasUnsavedPathChanges);
        }

        [TestMethod]
        public void WhenNoEditorOrTransientPathThenHasUnsavedPathChangesIsFalse()
        {
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(action => action());

            Assert.IsFalse(trainPathToolWindow.HasUnsavedPathChanges);
        }

        [TestMethod]
        public void WhenTransientPathMatchesSavedPathThenBuildPathRowsDoesNotDuplicateIt()
        {
            ImmutableArray<PathModelHeader> savedPaths = ImmutableArray.Create(
                new PathModelHeader { Id = "path-1", Name = "Saved Path", ValidationState = PathValidationState.Valid });
            ImmutableArray<PathModel> transientPaths = ImmutableArray.Create(new PathModel
            {
                Id = "path-1",
                Name = "Edited Path",
                ValidationState = PathValidationState.Invalid,
            });

            ImmutableArray<TrainPathListRow> rows = TrainPathToolWindow.BuildPathRows(savedPaths, transientPaths, null);

            Assert.HasCount(1, rows);
            Assert.AreEqual("path-1", rows[0].Id);
            Assert.AreEqual(PathValidationState.Invalid, rows[0].ValidationState);
        }

        [TestMethod]
        public void WhenPathIsTransientThenBuildPathRowsMarksItAsUnsaved()
        {
            ImmutableArray<PathModelHeader> savedPaths = ImmutableArray.Create(
                new PathModelHeader { Id = "path-1", Name = "Saved Path", ValidationState = PathValidationState.Valid });
            ImmutableArray<PathModel> transientPaths = ImmutableArray.Create(new PathModel { Id = "path-1", Name = "Saved Path" });

            ImmutableArray<TrainPathListRow> rows = TrainPathToolWindow.BuildPathRows(savedPaths, transientPaths, null, false);

            Assert.IsTrue(rows[0].HasUnsavedChanges);
        }

        [TestMethod]
        public void WhenPathIsSavedAndUnchangedThenBuildPathRowsDoesNotMarkItAsUnsaved()
        {
            ImmutableArray<PathModelHeader> savedPaths = ImmutableArray.Create(
                new PathModelHeader { Id = "path-1", Name = "Saved Path", ValidationState = PathValidationState.Valid });

            ImmutableArray<TrainPathListRow> rows = TrainPathToolWindow.BuildPathRows(savedPaths, ImmutableArray<PathModel>.Empty, null, false);

            Assert.IsFalse(rows[0].HasUnsavedChanges);
        }

        [TestMethod]
        public void WhenCurrentPathHasPendingEditsThenBuildPathRowsMarksOnlyThatPathAsUnsaved()
        {
            ImmutableArray<PathModelHeader> savedPaths = ImmutableArray.Create(
                new PathModelHeader { Id = "path-1", Name = "Edited Path", ValidationState = PathValidationState.Valid },
                new PathModelHeader { Id = "path-2", Name = "Other Path", ValidationState = PathValidationState.Valid });
            PathModel currentPath = new PathModel { Id = "path-1", Name = "Edited Path", ValidationState = PathValidationState.Valid };

            ImmutableArray<TrainPathListRow> rows = TrainPathToolWindow.BuildPathRows(savedPaths, ImmutableArray<PathModel>.Empty, currentPath, true);

            Assert.IsTrue(rows.Single(row => row.Id == "path-1").HasUnsavedChanges);
            Assert.IsFalse(rows.Single(row => row.Id == "path-2").HasUnsavedChanges);
        }

        [TestMethod]
        public void WhenCurrentPathWasNeverSavedThenBuildPathRowsMarksItAsUnsaved()
        {
            PathModel currentPath = new PathModel { Id = "new-path", Name = "New Path" };

            ImmutableArray<TrainPathListRow> rows = TrainPathToolWindow.BuildPathRows(ImmutableArray<PathModelHeader>.Empty, currentPath);

            Assert.IsTrue(rows[0].HasUnsavedChanges);
        }

        [TestMethod]
        public void WhenSelectPathThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(_ => invocations++);

            trainPathToolWindow.SelectPath("path-1");

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenSelectPathThenLoadPathActionIsCalled()
        {
            PathModelHeader loadedPath = null;
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(action => action(), () => { }, () => { }, path => loadedPath = path, () => { });
            trainPathToolWindow.UpdatePaths(ImmutableArray.Create(new PathModelHeader { Id = "path-1", Name = "First Path" }));

            trainPathToolWindow.SelectPath("path-1");

            Assert.IsNotNull(loadedPath);
            Assert.AreEqual("path-1", loadedPath.Id);
        }

        [TestMethod]
        public void WhenSelectPathCacheIsEmptyThenPathsAreRefreshedFromContext()
        {
            PathModelHeader loadedPath = null;
            TestTrainPathToolingContext toolingContext = new TestTrainPathToolingContext(ImmutableArray.Create(new PathModelHeader { Id = "path-1", Name = "First Path" }));
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => null, () => toolingContext, action => action(), () => { }, () => { }, path => loadedPath = path, () => { }, () => { });

            trainPathToolWindow.SelectPath("path-1");

            Assert.IsNotNull(loadedPath);
            Assert.AreEqual("path-1", loadedPath.Id);
        }

        [TestMethod]
        public void WhenSelectPathClearedThenUnloadPathActionIsCalled()
        {
            int unloads = 0;
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(action => action(), () => { }, () => { }, _ => { }, () => unloads++);

            trainPathToolWindow.SelectPath(null);

            Assert.AreEqual(1, unloads);
        }

        [TestMethod]
        public void WhenCreatePathThenCreateActionIsMarshaled()
        {
            int invocations = 0;
            int createActions = 0;
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(action => { invocations++; action(); }, () => createActions++, () => { });

            trainPathToolWindow.CreatePath();

            Assert.AreEqual(1, invocations);
            Assert.AreEqual(1, createActions);
        }

        [TestMethod]
        public void WhenSavePathThenSaveActionIsMarshaled()
        {
            int invocations = 0;
            int saveActions = 0;
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(action => { invocations++; action(); }, () => { }, () => saveActions++);

            trainPathToolWindow.SavePath();

            Assert.AreEqual(1, invocations);
            Assert.AreEqual(1, saveActions);
        }

        [TestMethod]
        public void WhenHighlightNodeThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(_ => invocations++);

            trainPathToolWindow.HighlightNode(3);

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenBeginMoveNodeThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(_ => invocations++);

            trainPathToolWindow.BeginMoveNode(2);

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenCommitMoveNodeThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(_ => invocations++);

            trainPathToolWindow.CommitMoveNode();

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenRepairSelectedNodeThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(_ => invocations++);

            trainPathToolWindow.RepairSelectedNode(2);

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenCancelMoveNodeThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(_ => invocations++);

            trainPathToolWindow.CancelMoveNode();

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenSelectPathInvokedWithNullEditorThenMarshaledActionIsSafeNoOp()
        {
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(action => action());

            trainPathToolWindow.SelectPath("path-1");

            Assert.AreEqual(TrainPathSnapshot.Empty, trainPathToolWindow.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenHighlightNodeInvokedWithNullEditorThenMarshaledActionIsSafeNoOp()
        {
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(action => action());

            trainPathToolWindow.HighlightNode(0);

            Assert.AreEqual(TrainPathSnapshot.Empty, trainPathToolWindow.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenPathEditorIsNotQueried()
        {
            int editorQueries = 0;
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => { editorQueries++; return null; }, () => null, action => action(), () => { }, () => { }, _ => { }, () => { }, () => { })
            {
                Active = false,
            };

            trainPathToolWindow.RefreshSnapshot();

            Assert.AreEqual(0, editorQueries);
        }

        [TestMethod]
        public void WhenResolverHasNoDiagnosticsThenDiagnosticRowsAreEmpty()
        {
            PathRouteResolution resolution = new PathRouteResolution(null, ImmutableArray<PathRouteDiagnostic>.Empty);

            ImmutableArray<TrainPathDiagnosticRow> rows = TrainPathToolWindow.BuildResolverDiagnostics(resolution);

            Assert.IsTrue(rows.IsEmpty);
        }

        [TestMethod]
        public void WhenResolverHasNodeDiagnosticThenDiagnosticRowPreservesNodeTargetAndAction()
        {
            PathRouteDiagnostic diagnostic = new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.MissingEndNode,
                "Path has no end node.", 2, "Add or mark an end node.");
            PathRouteResolution resolution = new PathRouteResolution(null, ImmutableArray.Create(diagnostic));

            ImmutableArray<TrainPathDiagnosticRow> rows = TrainPathToolWindow.BuildResolverDiagnostics(resolution);

            Assert.HasCount(1, rows);
            Assert.AreEqual(PathRouteDiagnosticSeverity.Warning, rows[0].Severity);
            Assert.AreEqual(PathRouteDiagnosticCode.MissingEndNode, rows[0].Code);
            Assert.AreEqual("Path has no end node.", rows[0].Message);
            Assert.AreEqual(2, rows[0].NodeIndex);
            Assert.AreEqual("Add or mark an end node.", rows[0].SuggestedAction);
            Assert.IsTrue(rows[0].HasNodeTarget);
            Assert.IsFalse(rows[0].HasSpanTarget);
        }

        [TestMethod]
        public void WhenResolverHasSpanDiagnosticThenDiagnosticRowPreservesSpanTarget()
        {
            PathRouteDiagnostic diagnostic = new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.AmbiguousRoute,
                "Nodes 1-4 have equal-cost routes.", 1, 4, "Choose a route candidate.");
            PathRouteResolution resolution = new PathRouteResolution(null, ImmutableArray.Create(diagnostic));

            ImmutableArray<TrainPathDiagnosticRow> rows = TrainPathToolWindow.BuildResolverDiagnostics(resolution);

            Assert.HasCount(1, rows);
            Assert.AreEqual(1, rows[0].FromNodeIndex);
            Assert.AreEqual(4, rows[0].ToNodeIndex);
            Assert.IsFalse(rows[0].HasNodeTarget);
            Assert.IsTrue(rows[0].HasSpanTarget);
            Assert.IsFalse(rows[0].CanRepair);
        }

        [TestMethod]
        public void WhenHighlightDiagnosticTargetInvokedThenActionIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(_ => invocations++);

            trainPathToolWindow.HighlightDiagnosticTarget(-1, 1, 4);

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenRepairDiagnosticNodeInvokedThenActionIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow trainPathToolWindow = CreateTrainPathToolWindow(_ => invocations++);

            trainPathToolWindow.RepairDiagnosticNode(2);

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenPathHasNodeFeaturesThenEditorStateMetadataSummarizesThem()
        {
            TestTrainPath trainPath = new TestTrainPath(new PathModel
            {
                Id = "test-path",
                Name = "Test Path",
            });
            trainPath.PathPoints.Add(new TestTrainPathPoint(PathNodeType.Start) { NextSidingNode = 1 });
            trainPath.PathPoints.Add(new TestTrainPathPoint(PathNodeType.Wait | PathNodeType.Reversal) { WaitInfo = new PathNodeWaitInfo { WaitTime = 30 } });
            trainPath.PathPoints.Add(new TestTrainPathPoint(PathNodeType.End) { ValidationResult = PathNodeInvalidReasons.NotOnTrack });

            ImmutableArray<ToolWindowRow> rows = TrainPathToolWindow.BuildEditorStateMetadata(trainPath);

            Assert.AreEqual("3", rows.Single(row => row.Name == "Node Count").Value);
            Assert.AreEqual("Yes", rows.Single(row => row.Name == "Has End").Value);
            Assert.AreEqual("Yes", rows.Single(row => row.Name == "Has Broken Nodes").Value);
            Assert.AreEqual("Yes", rows.Single(row => row.Name == "Has Passing Paths").Value);
            Assert.AreEqual("Yes", rows.Single(row => row.Name == "Has Wait Nodes").Value);
            Assert.AreEqual("Yes", rows.Single(row => row.Name == "Has Reversal Nodes").Value);
        }

        [TestMethod]
        public void WhenEditorHistoryAvailabilityChangesThenHistoryMetadataReflectsState()
        {
            ImmutableArray<ToolWindowRow> rows = TrainPathToolWindow.BuildEditorHistoryMetadata(true, false);

            Assert.AreEqual("Yes", rows.Single(row => row.Name == "Can Undo").Value);
            Assert.AreEqual("No", rows.Single(row => row.Name == "Can Redo").Value);
        }

        [TestMethod]
        public void WhenPathPointHasDefaultConnectivityThenToPathModelThrowsInvalidOperationException()
        {
            TestTrainPath trainPath = new TestTrainPath(new PathModel
            {
                Id = "test-path",
                Name = "Test Path",
            });
            trainPath.PathPoints.Add(new TestTrainPathPoint(PathNodeType.Start));

            try
            {
                trainPath.ConvertToPathModel(new PathModelHeader());
                Assert.Fail("Expected InvalidOperationException.");
            }
            catch (InvalidOperationException exception)
            {
                Assert.AreEqual("Invalid path point not on track segment", exception.Message);
            }
        }

        private sealed record TestTrainPath : TrainPathBase
        {
            public TestTrainPath(PathModel pathModel)
                : base(pathModel, CreateInitializedTrackWorld())
            {
            }

            protected override TrackSegmentSectionBase<TrainPathSegmentBase> InitializeSection(in PointD startLocation, in PointD endLocation)
            {
                throw new System.NotSupportedException();
            }

            protected override TrackSegmentSectionBase<TrainPathSegmentBase> InitializeSection(TrackWorld trackWorld, int trackNodeIndex)
            {
                throw new System.NotSupportedException();
            }

            protected override TrackSegmentSectionBase<TrainPathSegmentBase> InitializeSection(TrackWorld trackWorld, int trackNodeIndex,
                in PointD startLocation, in PointD endLocation)
            {
                throw new System.NotSupportedException();
            }

            public override double DistanceSquared(in PointD point)
            {
                return double.NaN;
            }

            public PathModel ConvertToPathModel(PathModelHeader pathModelHeader)
            {
                return ToPathModel(pathModelHeader);
            }
        }

        private sealed record TestTrainPathPoint : TrainPathPointBase
        {
            public TestTrainPathPoint(PathNodeType nodeType)
                : base(PointD.None, nodeType)
            {
            }
        }

        private static TrainPathToolWindow CreateTrainPathToolWindow(Action<Action> invoker)
        {
            return CreateTrainPathToolWindow(invoker, () => { }, () => { });
        }

        private static TrainPathToolWindow CreateTrainPathToolWindow(Action<Action> invoker, Action createPathAction, Action savePathAction)
        {
            return CreateTrainPathToolWindow(invoker, createPathAction, savePathAction, _ => { }, () => { });
        }

        private static TrainPathToolWindow CreateTrainPathToolWindow(Action<Action> invoker, Action createPathAction, Action savePathAction,
            Action<PathModelHeader> loadPathAction, Action unloadPathAction)
        {
            return new TrainPathToolWindow(() => null, () => null, invoker, createPathAction, savePathAction, loadPathAction, unloadPathAction, () => { });
        }

        private sealed class TestTrainPathToolingContext : ITrainPathToolingContext
        {
            private readonly ImmutableArray<PathModelHeader> paths;

            public TestTrainPathToolingContext(ImmutableArray<PathModelHeader> paths)
            {
                this.paths = paths;
            }

            public bool UseMetricUnits => true;

            public TrackWorld TrackWorld => null;

            public Task<ImmutableArray<PathModelHeader>> GetPaths() => Task.FromResult(paths);

            public Task<ImmutableArray<PathModelHeader>> ValidateAllPaths() => Task.FromResult(paths);
        }

        private static TrackWorld CreateInitializedTrackWorld() => TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
    }
}
