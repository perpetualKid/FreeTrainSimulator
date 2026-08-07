using System.Collections.Immutable;
using System;
using System.Reflection;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class PathEditorTests
    {
        [TestMethod]
        public void WhenPathHasFatalResolverDiagnosticThenCanInitializePathReturnsFalse()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 4), CreateNode(PathNodeType.End, -1)),
            };

            bool canInitialize = PathEditor.CanInitializePath(pathModel, null, out PathRouteResolution resolution);

            Assert.IsFalse(canInitialize);
            Assert.AreEqual(PathRouteDiagnosticSeverity.Fatal, resolution.HighestSeverity);
        }

        [TestMethod]
        public void WhenMainRouteDoesNotReachEndThenCanInitializePathReturnsFalse()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.Intermediate, -1), CreateNode(PathNodeType.End, -1)),
            };

            bool canInitialize = PathEditor.CanInitializePath(pathModel, null, out PathRouteResolution resolution);

            Assert.IsFalse(canInitialize);
            Assert.IsTrue(HasDiagnostic(resolution, PathRouteDiagnosticCode.MainRouteDoesNotReachEnd));
        }

        [TestMethod]
        public void WhenPathHasNoFatalResolverDiagnosticThenCanInitializePathReturnsTrue()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.End, -1)),
            };

            bool canInitialize = PathEditor.CanInitializePath(pathModel, null, out PathRouteResolution resolution);

            Assert.IsTrue(canInitialize);
            Assert.IsTrue(resolution.HighestSeverity < PathRouteDiagnosticSeverity.Fatal);
        }

        [TestMethod]
        public void WhenSnapshotContextHasDefaultConnectivityThenInvalidPointDetailsAreIncluded()
        {
            PathModelHeader path = new PathModelHeader()
            {
                Id = "path-1",
                Name = "Path 1",
            };
            TestTrainPath trainPath = new TestTrainPath(new PathModel());
            trainPath.PathPoints.Add(new TestTrainPathPoint(PathNodeType.Start));

            string context = PathEditor.BuildSnapshotContext(path, trainPath, true, false, true, false, false);

            Assert.Contains("PathId='path-1'", context);
            Assert.Contains("PathName='Path 1'", context);
            Assert.Contains("EditMode=True", context);
            Assert.Contains("PointCount=1", context);
            Assert.Contains("InvalidPoints=#0:Start:None/DefaultSegments", context);
            Assert.Contains("CanRedo=True", context);
        }

        [TestMethod]
        public void WhenSnapshotContextHasNoTrainPathThenInvalidPointsAreNone()
        {
            string context = PathEditor.BuildSnapshotContext(null, null, false, false, false, false, true);

            Assert.Contains("PathId='<none>'", context);
            Assert.Contains("PathName='<none>'", context);
            Assert.Contains("EditMode=False", context);
            Assert.Contains("PointCount=0", context);
            Assert.Contains("InvalidPoints=none", context);
            Assert.Contains("EditorDragged=True", context);
        }

        [TestMethod]
        public void WhenPathHasStartButNoEndThenTrainPathConstructionSucceeds()
        {
            // Regression: undoing after an endpoint was set restores a partial path (Start + intermediate,
            // no End yet). The TrainPathBase constructor must not throw for an incomplete path during editing;
            // completeness is validated separately by PathRouteResolver.
            PathModel partialPath = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.Intermediate, -1)),
            };

            TestTrainPath trainPath = new TestTrainPath(partialPath);

            Assert.IsNotNull(trainPath);
        }

        [TestMethod]
        public void WhenAnchoredViaPlacementBeginsThenInsertedNodeUsesProvidedAnchor()
        {
            PathModel source = CreateEditablePath();
            PathEditor editor = CreateEditor(source);
            PathNode anchor = CreateNodeAt(50, PathNodeType.Intermediate, -1);

            PathEditResult result = editor.BeginViaPointPlacementAt(0, anchor);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(anchor.Location, editor.TryCaptureCurrentPathModel().PathNodes[1].Location);
        }

        [TestMethod]
        public void WhenPendingViaPlacementIsCanceledThenHistoryAndSourcePathAreRestored()
        {
            PathModel source = CreateEditablePath();
            PathEditor editor = CreateEditor(source);
            _ = editor.BeginViaPointPlacementAt(0, CreateNodeAt(50, PathNodeType.Intermediate, -1));

            bool canceled = editor.CancelMoveNode();

            Assert.IsTrue(canceled);
            Assert.AreEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            Assert.IsFalse(editor.CanUndo);
            Assert.IsFalse(editor.CanRedo);
        }

        [TestMethod]
        public void WhenPendingViaPlacementIsCommittedThenSingleUndoRestoresSourcePath()
        {
            PathModel source = CreateEditablePath();
            PathEditor editor = CreateEditor(source);
            _ = editor.BeginViaPointPlacementAt(0, CreateNodeAt(50, PathNodeType.Intermediate, -1));
            PathModel insertedModel = editor.TryCaptureCurrentPathModel();
            SetPrivateField(editor, "movePreviewModel", insertedModel);

            PathEditResult committed = editor.CommitMoveNode();
            bool undone = editor.Undo();

            Assert.IsTrue(committed.Success);
            Assert.IsTrue(undone);
            Assert.AreEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            Assert.IsFalse(editor.CanUndo);
        }

        [TestMethod]
        public void WhenPathHasEndButNoStartThenTrainPathConstructionSucceeds()
        {
            PathModel partialPath = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Intermediate, 1), CreateNode(PathNodeType.End, -1)),
            };

            TestTrainPath trainPath = new TestTrainPath(partialPath);

            Assert.IsNotNull(trainPath);
        }

        [TestMethod]
        public void WhenPathCannotResolveToTrackThenSnapToTrackFailsAndReturnsOriginalUnchanged()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.End, -1)),
            };

            PathEditResult result = PathEditor.SnapPathToTrack(pathModel, null);

            Assert.IsFalse(result.Success);
            Assert.AreSame(pathModel, result.PathModel);
        }

        [TestMethod]
        public void WhenPathHasPassingBranchThenSnapToTrackNoLongerRefusesForPassingBranches()
        {
            // The passing-branch guard was lifted: snapping now defers to path generation, which weaves passing
            // branches where they rejoin. Without a track world the spans stay unresolved, so this still fails,
            // but not because of a passing-branch refusal.
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreatePassingNode(PathNodeType.Start, 1, 2),
                    CreatePassingNode(PathNodeType.End, -1, -1),
                    CreatePassingNode(PathNodeType.Intermediate, -1, 1)),
            };

            PathEditResult result = PathEditor.SnapPathToTrack(pathModel, null);

            Assert.IsFalse(result.Success);
            Assert.DoesNotContain("passing", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.AreSame(pathModel, result.PathModel);
        }

        [TestMethod]
        public void WhenPathHasFatalResolverDiagnosticThenResolveValidationStateReturnsInvalid()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 4), CreateNode(PathNodeType.End, -1)),
            };

            PathValidationState state = PathEditor.ResolveValidationState(pathModel, null);

            Assert.AreEqual(PathValidationState.Invalid, state);
        }

        [TestMethod]
        public void WhenStaleValidationStateIsRefreshedThenModelReportsCurrentState()
        {
            // Regression: ValidationState is persisted on the model, so an edit that replaced PathNodes kept the
            // stale marker until the path was saved or revalidated.
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 4), CreateNode(PathNodeType.End, -1)),
                ValidationState = PathValidationState.Valid,
            };

            PathModel refreshed = PathEditor.RefreshValidationState(pathModel, null);

            Assert.AreEqual(PathValidationState.Invalid, refreshed.ValidationState);
        }

        [TestMethod]
        public void WhenValidationStateIsUnchangedThenRefreshReturnsTheSameInstance()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.End, -1)),
                ValidationState = PathValidationState.Valid,
            };

            PathModel refreshed = PathEditor.RefreshValidationState(pathModel, null);

            Assert.AreSame(pathModel, refreshed);
        }

        [TestMethod]
        public void WhenPathResolvesWithoutErrorThenResolveValidationStateReturnsValid()
        {
            // A start-to-end path without a track world produces at most warnings/information (no error or
            // fatal), which ResolveValidationState treats as valid, matching PathRouteResolution.IsValid.
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.End, -1)),
            };

            PathValidationState state = PathEditor.ResolveValidationState(pathModel, null);

            Assert.AreEqual(PathValidationState.Valid, state);
        }

        [TestMethod]
        public void WhenPathHasJunctionNodeAwayFromJunctionThenResolveValidationStateReturnsInvalid()
        {
            PathModel pathModel = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.Junction, 2), CreateNode(PathNodeType.End, -1)),
            };

            PathValidationState state = PathEditor.ResolveValidationState(pathModel, TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld());

            Assert.AreEqual(PathValidationState.Invalid, state);
        }

        [TestMethod]
        public void WhenMoveAnchorsHaveSameLocationAndTrackNodeThenTheyAreEquivalent()
        {
            PathNode first = new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(1, 0, 2)))
            {
                NodeIndex = 4,
            };
            PathNode second = new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(1, 0, 2)))
            {
                NodeIndex = 4,
            };

            bool equivalent = PathEditor.EquivalentMoveAnchor(first, second);

            Assert.IsTrue(equivalent);
        }

        [TestMethod]
        public void WhenMoveAnchorsHaveDifferentTrackNodeThenTheyAreNotEquivalent()
        {
            PathNode first = new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(1, 0, 2)))
            {
                NodeIndex = 4,
            };
            PathNode second = new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(1, 0, 2)))
            {
                NodeIndex = 5,
            };

            bool equivalent = PathEditor.EquivalentMoveAnchor(first, second);

            Assert.IsFalse(equivalent);
        }

        private static PathNode CreateNode(PathNodeType nodeType, int nextMainNode)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero))
            {
                NodeType = nodeType,
                NextMainNode = nextMainNode,
            };
        }

        private static PathNode CreatePassingNode(PathNodeType nodeType, int nextMainNode, int nextSidingNode)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero))
            {
                NodeType = nodeType,
                NextMainNode = nextMainNode,
                NextSidingNode = nextSidingNode,
            };
        }

        private static bool HasDiagnostic(PathRouteResolution resolution, PathRouteDiagnosticCode code)
        {
            foreach (PathRouteDiagnostic diagnostic in resolution.Diagnostics)
            {
                if (diagnostic.Code == code)
                    return true;
            }

            return false;
        }

        [TestMethod]
        public void WhenLocationIsWithinToleranceThenTryGetPathNodeAtReturnsClosestNode()
        {
            TestTrainPathPoint[] pathPoints = new TestTrainPathPoint[]
            {
                new TestTrainPathPoint(new PointD(0, 0)),
                new TestTrainPathPoint(new PointD(100, 0)),
                new TestTrainPathPoint(new PointD(200, 0)),
            };

            bool found = PathEditor.TryGetPathNodeAt(pathPoints, new PointD(104, 3), 10, out int nodeIndex);

            Assert.IsTrue(found);
            Assert.AreEqual(1, nodeIndex);
        }

        [TestMethod]
        public void WhenLocationIsOutsideToleranceThenTryGetPathNodeAtReturnsFalse()
        {
            TestTrainPathPoint[] pathPoints = new TestTrainPathPoint[]
            {
                new TestTrainPathPoint(new PointD(0, 0)),
            };

            bool found = PathEditor.TryGetPathNodeAt(pathPoints, new PointD(50, 0), 10, out int nodeIndex);

            Assert.IsFalse(found);
            Assert.AreEqual(-1, nodeIndex);
        }

        [TestMethod]
        public void WhenPathHasNoPointsThenTryGetPathNodeAtReturnsFalse()
        {
            bool found = PathEditor.TryGetPathNodeAt(Array.Empty<TrainPathPointBase>(), new PointD(0, 0), 10, out int nodeIndex);

            Assert.IsFalse(found);
            Assert.AreEqual(-1, nodeIndex);
        }

        private static PathEditor CreateEditor(PathModel pathModel)
        {
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
            PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld));
            editor.InitializeNewPath();
            typeof(PathEditor).GetMethod("RestoreSnapshot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { pathModel });
            return editor;
        }

        private static PathModel CreateEditablePath()
        {
            return new PathModel()
            {
                Id = "editable-path",
                Name = "Editable Path",
                PathNodes = ImmutableArray.Create(
                    CreateNodeAt(0, PathNodeType.Start, 1),
                    CreateNodeAt(100, PathNodeType.End, -1)),
            };
        }

        private static PathNode CreateNodeAt(float x, PathNodeType nodeType, int nextMainNode)
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(x, 0, 0)))
            {
                NodeType = nodeType,
                NodeIndex = 1,
                NextMainNode = nextMainNode,
                NextSidingNode = -1,
            };
        }

        private static void SetPrivateField(PathEditor editor, string fieldName, object value)
        {
            typeof(PathEditor).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(editor, value);
        }

        private sealed class TestPathEditorContext : IPathEditorContext, IPathEditorContextServicesAccessor
        {
            private readonly IPathEditorServices services;

            public IMapRenderer Renderer => null;

            public IMapViewport Viewport => null;

            public ToolboxContentMode ContentMode { get; set; }

            public PathEditorBase PathEditor { get; set; }

            IPathEditorServices IPathEditorContextServicesAccessor.Services => services;

            public TestPathEditorContext(TrackWorld trackWorld)
            {
                services = new PathEditorServices(trackWorld);
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
                throw new NotSupportedException();
            }

            protected override TrackSegmentSectionBase<TrainPathSegmentBase> InitializeSection(TrackWorld trackWorld, int trackNodeIndex)
            {
                throw new NotSupportedException();
            }

            protected override TrackSegmentSectionBase<TrainPathSegmentBase> InitializeSection(TrackWorld trackWorld, int trackNodeIndex,
                in PointD startLocation, in PointD endLocation)
            {
                throw new NotSupportedException();
            }

            public override double DistanceSquared(in PointD point)
            {
                return double.NaN;
            }
        }

        private sealed record TestTrainPathPoint : TrainPathPointBase
        {
            public TestTrainPathPoint(PathNodeType nodeType)
                : base(PointD.None, nodeType)
            {
            }

            public TestTrainPathPoint(in PointD location)
                : base(location, PathNodeType.Intermediate)
            {
            }
        }

        private static TrackWorld CreateInitializedTrackWorld() => TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
    }
}
