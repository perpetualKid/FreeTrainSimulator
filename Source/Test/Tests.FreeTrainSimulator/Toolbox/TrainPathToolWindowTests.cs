using System.Collections.Immutable;
using System;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;
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
            TrainPathToolWindow trainPathToolWindow= new TrainPathToolWindow(() => null, () => null, _ => { })
            {
                Active = false,
            };

            trainPathToolWindow.RefreshSnapshot();

            Assert.AreEqual(TrainPathSnapshot.Empty, trainPathToolWindow.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenActiveWithNoEditorRefreshSnapshotThenSnapshotStaysEmpty()
        {
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => null, () => null, _ => { })
            {
                Active = true,
            };

            trainPathToolWindow.RefreshSnapshot();

            Assert.AreEqual(TrainPathSnapshot.Empty, trainPathToolWindow.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenSelectPathThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => null, () => null, _ => invocations++);

            trainPathToolWindow.SelectPath("path-1");

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenHighlightNodeThenGameThreadInvokerIsCalled()
        {
            int invocations = 0;
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => null, () => null, _ => invocations++);

            trainPathToolWindow.HighlightNode(3);

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenSelectPathInvokedWithNullEditorThenMarshaledActionIsSafeNoOp()
        {
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => null, () => null, action => action());

            trainPathToolWindow.SelectPath("path-1");

            Assert.AreEqual(TrainPathSnapshot.Empty, trainPathToolWindow.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenHighlightNodeInvokedWithNullEditorThenMarshaledActionIsSafeNoOp()
        {
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => null, () => null, action => action());

            trainPathToolWindow.HighlightNode(0);

            Assert.AreEqual(TrainPathSnapshot.Empty, trainPathToolWindow.CaptureTrainPathSnapshot());
        }

        [TestMethod]
        public void WhenInactiveRefreshSnapshotThenPathEditorIsNotQueried()
        {
            int editorQueries = 0;
            TrainPathToolWindow trainPathToolWindow = new TrainPathToolWindow(() => { editorQueries++; return null; }, () => null, action => action())
            {
                Active = false,
            };

            trainPathToolWindow.RefreshSnapshot();

            Assert.AreEqual(0, editorQueries);
        }

        [TestMethod]
        public void WhenResolverHasNoDiagnosticsThenDiagnosticMetadataIsEmpty()
        {
            PathRouteResolution resolution = new PathRouteResolution(null, ImmutableArray<PathRouteDiagnostic>.Empty);

            ImmutableArray<ToolWindowRow> rows = TrainPathToolWindow.BuildResolverDiagnosticMetadata(resolution);

            Assert.IsTrue(rows.IsEmpty);
        }

        [TestMethod]
        public void WhenResolverHasDiagnosticsThenDiagnosticMetadataContainsSummaryAndDetails()
        {
            PathRouteDiagnostic diagnostic = new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.MissingEndNode,
                "Path has no end node.");
            PathRouteResolution resolution = new PathRouteResolution(null, ImmutableArray.Create(diagnostic));

            ImmutableArray<ToolWindowRow> rows = TrainPathToolWindow.BuildResolverDiagnosticMetadata(resolution);

            Assert.HasCount(3, rows);
            Assert.AreEqual("Route Diagnostics", rows[0].Name);
            Assert.AreEqual(string.Empty, rows[0].Value);
            Assert.IsTrue(rows[0].Bold);
            Assert.AreEqual("Summary", rows[1].Name);
            Assert.AreEqual("1 (Warning)", rows[1].Value);
            Assert.AreEqual(nameof(PathRouteDiagnosticCode.MissingEndNode), rows[2].Name);
            Assert.AreEqual("Path has no end node.", rows[2].Value);
        }

        [TestMethod]
        public void WhenResolverHasAnchorMismatchThenDiagnosticMetadataContainsMismatchDetails()
        {
            PathRouteDiagnostic diagnostic = new PathRouteDiagnostic(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.AnchorLocationMismatch,
                "Path node 0 has track anchor 2, but its stored location resolves to track node 1.", 0,
                "Review the path node location and stored track anchor before saving or repairing the path.");
            PathRouteResolution resolution = new PathRouteResolution(null, ImmutableArray.Create(diagnostic));

            ImmutableArray<ToolWindowRow> rows = TrainPathToolWindow.BuildResolverDiagnosticMetadata(resolution);

            Assert.HasCount(3, rows);
            Assert.AreEqual(nameof(PathRouteDiagnosticCode.AnchorLocationMismatch), rows[2].Name);
            Assert.Contains("track anchor 2", rows[2].Value);
            Assert.Contains("track node 1", rows[2].Value);
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

        private static TrackWorld CreateInitializedTrackWorld() => TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
    }
}
