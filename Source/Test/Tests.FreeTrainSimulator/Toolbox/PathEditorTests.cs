using System.Collections.Immutable;
using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
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
        }

        private static TrackWorld CreateInitializedTrackWorld() => TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
    }
}
