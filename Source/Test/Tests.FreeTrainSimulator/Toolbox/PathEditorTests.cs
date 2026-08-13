using System.Collections.Immutable;
using System;
using System.Linq;
using System.Reflection;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;
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
        public void WhenNewPathIsInitializedThenNoPlacementModeIsActive()
        {
            using PathEditor editor = CreateNewEditor();

            Assert.IsFalse(editor.IsPlacementActive);
        }

        [TestMethod]
        public void WhenExtendPathBeginsThenExplicitResolverBackedModeIsActiveWithoutMutatingThePath()
        {
            PathModel source = CreateEditablePath();
            using PathEditor editor = CreateEditor(source);

            PathEditorCommandResult result = editor.ExtendPathCommand();

            Assert.IsTrue(result.Success);
            Assert.IsTrue(editor.IsExtendingPath);
            Assert.IsFalse(editor.EditMode);
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        public void WhenExtendPathIsDraggedThenPreviewedSpanIsNotCommitted()
        {
            PathModel source = CreateEditablePath();
            using PathEditor editor = CreateEditor(source);
            _ = editor.ExtendPathCommand();
            PathEditResult preview = InvokeExtendPathToAnchor(source, CreateNodeAt(200, PathNodeType.None, -1));
            SetPrivateField(editor, "movePreviewModel", preview.PathModel);

            editor.MouseDragged(new UserCommandArgs(), KeyModifiers.None);
            editor.MouseReleasedLeft(new UserCommandArgs(), KeyModifiers.None);

            Assert.IsTrue(editor.IsExtendingPath);
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        public void WhenExtendPathSpanIsCommittedThenTheNextSpanRemainsInExplicitExtendMode()
        {
            PathModel source = CreateEditablePath();
            using PathEditor editor = CreateEditor(source);
            _ = editor.ExtendPathCommand();
            PathEditResult preview = InvokeExtendPathToAnchor(source, CreateNodeAt(200, PathNodeType.None, -1));
            SetPrivateField(editor, "movePreviewModel", preview.PathModel);

            PathEditResult committed = editor.CommitPlacement();

            Assert.IsTrue(committed.Success);
            Assert.IsTrue(editor.IsExtendingPath);
            Assert.AreEqual(3, editor.TryCaptureCurrentPathModel().PathNodes.Length);
        }

        [TestMethod]
        public void WhenPlacementPreviewIsCommittedThroughTheKeyboardCommandThenItMaterializesOnce()
        {
            PathModel source = CreateEditablePath();
            using PathEditor editor = CreateEditor(source);
            _ = editor.ExtendPathCommand();
            PathEditResult preview = InvokeExtendPathToAnchor(source, CreateNodeAt(200, PathNodeType.None, -1));
            SetPrivateField(editor, "movePreviewModel", preview.PathModel);

            PathEditorCommandResult result = editor.CommitPlacementCommand();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, editor.TryCaptureCurrentPathModel().PathNodes.Length);
            Assert.IsTrue(editor.CanUndo);
        }

        [TestMethod]
        public void WhenAffectedSpansAreSelectedThenOnlySpansBoundedByAnEditedNodeAreReturned()
        {
            PathRouteResolution resolution = CreateResolution(
                new ResolvedPathSpan(0, 1, PathRouteSpanStatus.Resolved),
                new ResolvedPathSpan(1, 2, PathRouteSpanStatus.Resolved),
                new ResolvedPathSpan(2, 3, PathRouteSpanStatus.Resolved));

            ImmutableArray<ResolvedPathSpan> affected = InvokeAffectedSpans(resolution, ImmutableArray.Create(2));

            Assert.AreEqual(2, affected.Length);
            Assert.IsTrue(affected.All(span => span.FromNodeIndex == 2 || span.ToNodeIndex == 2));
        }

        [TestMethod]
        public void WhenNoNodeIsEditedThenAllSpansAreAffected()
        {
            PathRouteResolution resolution = CreateResolution(
                new ResolvedPathSpan(0, 1, PathRouteSpanStatus.Resolved),
                new ResolvedPathSpan(1, 2, PathRouteSpanStatus.Resolved));

            ImmutableArray<ResolvedPathSpan> affected = InvokeAffectedSpans(resolution, ImmutableArray<int>.Empty);

            Assert.AreEqual(2, affected.Length);
        }

        [TestMethod]
        public void WhenEndAnchorSpanResolvesThenAnchorIsCommitted()
        {
            using PathEditor editor = CreateEditorWithStartAnchor();

            PathEditorCommandResult result = editor.SetEndAnchorCommand(CreateNodeAt(100, PathNodeType.None, -1), false);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(HasNodeType(editor.TryCaptureCurrentPathModel(), PathNodeType.End));
        }

        [TestMethod]
        public void WhenEndAnchorSpanIsAmbiguousThenCommittedPathAndHistoryRemainUnchanged()
        {
            PathModel source = CreateAmbiguousEndpointPath();
            using PathEditor editor = CreateEditor(source, CreateAmbiguousTrackWorld());

            PathEditorCommandResult result = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit);
            Assert.IsFalse(editor.CanUndo);
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        public void WhenPendingAmbiguousCandidateIsPreviewedThenCommittedPathDoesNotChange()
        {
            using PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld());
            _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);

            PathEditResult result = editor.PreviewPendingRouteCandidate(0, 1);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit);
            Assert.IsFalse(HasNodeType(editor.TryCaptureCurrentPathModel(), PathNodeType.End));
            Assert.IsFalse(editor.CanUndo);
        }

        [TestMethod]
        public void WhenRouteCandidatesAreCycledThenSpaceEquivalentAcceptsThePreviewedCandidate()
        {
            using PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld());
            _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);

            PathEditorCommandResult cycleResult = editor.CycleRouteCandidateCommand(1);
            PathEditorCommandResult acceptResult = editor.AcceptPreviewedRouteCandidateCommand();

            Assert.IsTrue(cycleResult.Success);
            Assert.IsTrue(acceptResult.Success);
            Assert.IsTrue(HasNodeType(editor.TryCaptureCurrentPathModel(), PathNodeType.End));
            Assert.IsTrue(editor.CanUndo);
        }

        [TestMethod]
        public void WhenPendingRouteCandidateSelectionIsCanceledThenEscapeEquivalentLeavesSourceUnchanged()
        {
            PathModel source = CreateAmbiguousEndpointPath();
            using PathEditor editor = CreateEditor(source, CreateAmbiguousTrackWorld());
            _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);

            PathEditorCommandResult result = editor.CancelPathInteractionCommand();

            Assert.IsTrue(result.Success);
            Assert.IsFalse(editor.HasPendingAmbiguousSpanCommit);
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        public void WhenPendingAmbiguousCandidateIsAcceptedThenOneUndoRestoresTheSource()
        {
            using PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld());
            _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);

            PathEditResult result = editor.AcceptPendingRouteCandidate(0, 1);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(editor.HasPendingAmbiguousSpanCommit);
            Assert.IsTrue(HasNodeType(editor.TryCaptureCurrentPathModel(), PathNodeType.End));
            Assert.IsTrue(editor.CanUndo);
            Assert.IsTrue(editor.Undo());
            Assert.IsFalse(HasNodeType(editor.TryCaptureCurrentPathModel(), PathNodeType.End));
            Assert.IsFalse(editor.CanUndo);
        }

        [TestMethod]
        public void WhenPendingAmbiguousCandidateIsCanceledThenSourceAndHistoryRemainUnchanged()
        {
            PathModel source = CreateAmbiguousEndpointPath();
            using PathEditor editor = CreateEditor(source, CreateAmbiguousTrackWorld());
            _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);

            editor.CancelPendingRouteCandidate();

            Assert.IsFalse(editor.HasPendingAmbiguousSpanCommit);
            Assert.IsFalse(editor.CanUndo);
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        public void WhenEndAnchorSpanResolvesThenOneUndoRestoresTheStartOnlyPath()
        {
            using PathEditor editor = CreateEditorWithStartAnchor();
            _ = editor.SetEndAnchorCommand(CreateNodeAt(100, PathNodeType.None, -1), false);

            bool undone = editor.Undo();

            Assert.IsTrue(undone);
            Assert.IsFalse(HasNodeType(editor.TryCaptureCurrentPathModel(), PathNodeType.End));
            Assert.IsFalse(editor.CanUndo);
        }

        [TestMethod]
        public void WhenEndAnchorSpanCannotBeRoutedThenAnchorIsNotCommitted()
        {
            using PathEditor editor = CreateEditorWithStartAnchor();
            PathModel beforeCommit = editor.TryCaptureCurrentPathModel();

            PathEditorCommandResult result = editor.SetEndAnchorCommand(CreateUnroutableNode(), false);

            Assert.IsFalse(result.Success);
            Assert.AreSequenceEqual(beforeCommit.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        public void WhenEndAnchorSpanCannotBeRoutedThenNoUndoSnapshotIsRecorded()
        {
            using PathEditor editor = CreateEditorWithStartAnchor();

            _ = editor.SetEndAnchorCommand(CreateUnroutableNode(), false);

            Assert.IsFalse(editor.CanUndo);
        }

        [TestMethod]
        public void WhenPointerMovesOverEmptyNewPathThenLegacyEndpointMutationDoesNotRun()
        {
            using PathEditor editor = CreateNewEditor();

            editor.UpdatePointerLocation(new PointD(25, 0), null);

            Assert.IsEmpty(editor.TryCaptureCurrentPathModel().PathNodes);
            Assert.IsFalse(editor.IsPlacementActive);
        }

        [TestMethod]
        public void WhenInitialStartAnchorPlacementIsCommittedThenEndAnchorPlacementBegins()
        {
            using PathEditor editor = CreateNewEditor();
            _ = editor.BeginStartAnchorPlacementCommand();
            PathModel source = editor.TryCaptureCurrentPathModel();
            PathEditResult preview = PathModelEditor.SetStartAnchor(source, CreateNodeAt(25, PathNodeType.None, -1), false);
            SetPrivateField(editor, "movePreviewModel", preview.PathModel);

            _ = editor.CommitPlacement();

            Assert.IsTrue(editor.IsPlacingEndAnchor);
        }

        [TestMethod]
        public void WhenEndPlacementAfterInitialStartIsCanceledThenStartAnchorIsRetained()
        {
            using PathEditor editor = CreateNewEditor();
            _ = editor.BeginStartAnchorPlacementCommand();
            PathModel source = editor.TryCaptureCurrentPathModel();
            PathEditResult preview = PathModelEditor.SetStartAnchor(source, CreateNodeAt(25, PathNodeType.None, -1), false);
            SetPrivateField(editor, "movePreviewModel", preview.PathModel);
            _ = editor.CommitPlacement();

            _ = editor.CancelPlacement();

            Assert.IsTrue(editor.TryCaptureCurrentPathModel().PathNodes[0].NodeType.Includes(PathNodeType.Start));
        }

        [TestMethod]
        public void WhenInitialStartAnchorIsCommittedThenOneUndoRestoresEmptyPath()
        {
            using PathEditor editor = CreateNewEditor();
            _ = editor.BeginStartAnchorPlacementCommand();
            PathModel source = editor.TryCaptureCurrentPathModel();
            PathEditResult preview = PathModelEditor.SetStartAnchor(source, CreateNodeAt(25, PathNodeType.None, -1), false);
            SetPrivateField(editor, "movePreviewModel", preview.PathModel);
            _ = editor.CommitPlacement();

            _ = editor.Undo();

            Assert.IsEmpty(editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        public void WhenNewPathStartIsSetDirectlyThenEndAnchorPlacementBegins()
        {
            using PathEditor editor = CreateNewEditor();

            PathEditorCommandResult result = editor.SetStartAnchorCommand(CreateNodeAt(25, PathNodeType.None, -1), false);

            Assert.IsTrue(result.Success && editor.IsPlacingEndAnchor);
        }

        [TestMethod]
        public void WhenStartAnchorPlacementBeginsThenCommittedPathIsUnchanged()
        {
            PathModel source = CreateEditablePath();
            using PathEditor editor = CreateEditor(source);

            PathEditorCommandResult result = editor.BeginStartAnchorPlacementCommand();

            Assert.IsTrue(result.Success);
            Assert.IsTrue(editor.IsPlacingStartAnchor);
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        public void WhenStartAnchorPlacementIsCanceledThenDirtyStateAndHistoryAreUnchanged()
        {
            PathModel source = CreateEditablePath();
            using PathEditor editor = CreateEditor(source);
            SetPrivateField(editor, "unsavedChanges", false);
            _ = editor.BeginStartAnchorPlacementCommand();

            bool canceled = editor.CancelPlacement();

            Assert.IsTrue(canceled);
            Assert.IsFalse(editor.HasUnsavedChanges);
            Assert.IsFalse(editor.CanUndo);
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        public void WhenStartAnchorPlacementIsCommittedThenOneUndoRestoresSource()
        {
            PathModel source = CreateEditablePath();
            using PathEditor editor = CreateEditor(source);
            _ = editor.BeginStartAnchorPlacementCommand();
            PathEditResult preview = PathModelEditor.SetStartAnchor(source, CreateNodeAt(25, PathNodeType.None, -1), false);
            SetPrivateField(editor, "movePreviewModel", preview.PathModel);

            PathEditResult committed = editor.CommitPlacement();
            bool undone = editor.Undo();

            Assert.IsTrue(committed.Success);
            Assert.IsTrue(undone);
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            Assert.IsFalse(editor.CanUndo);
        }

        [TestMethod]
        public void WhenUndoneStartAnchorPlacementIsRedoneThenPlacedAnchorReturns()
        {
            PathModel source = CreateEditablePath();
            using PathEditor editor = CreateEditor(source);
            _ = editor.BeginStartAnchorPlacementCommand();
            PathEditResult preview = PathModelEditor.SetStartAnchor(source, CreateNodeAt(25, PathNodeType.None, -1), false);
            SetPrivateField(editor, "movePreviewModel", preview.PathModel);
            _ = editor.CommitPlacement();
            _ = editor.Undo();

            bool redone = editor.Redo();

            Assert.IsTrue(redone);
            Assert.AreEqual(preview.PathModel.PathNodes[0].Location, editor.TryCaptureCurrentPathModel().PathNodes[0].Location);
        }

        [TestMethod]
        public void WhenEndAnchorPlacementCommitsThenAuthoredModelRemainsSnapshotSource()
        {
            PathModel source = new PathModel
            {
                Id = "partial-path",
                Name = "Partial Path",
                PathNodes = ImmutableArray.Create(CreateNodeAt(0, PathNodeType.Start, -1)),
            };
            using PathEditor editor = CreateEditor(source);
            _ = editor.BeginEndAnchorPlacementCommand();
            PathEditResult preview = PathModelEditor.SetEndAnchor(source, CreateNodeAt(100, PathNodeType.None, -1), false);
            SetPrivateField(editor, "movePreviewModel", preview.PathModel);

            PathEditResult committed = editor.CommitPlacement();

            Assert.AreSame(committed.PathModel, editor.TryCaptureCurrentPathModel());
            Assert.IsTrue(editor.HasUnsavedChanges);
        }

        [TestMethod]
        public void WhenEndAnchorPlacementCommitsThenMissingEndDiagnosticIsCleared()
        {
            PathModel source = new PathModel
            {
                Id = "partial-path",
                Name = "Partial Path",
                PathNodes = ImmutableArray.Create(CreateNodeAt(0, PathNodeType.Start, -1)),
            };
            using PathEditor editor = CreateEditor(source);
            _ = editor.BeginEndAnchorPlacementCommand();
            PathEditResult preview = PathModelEditor.SetEndAnchor(source, CreateNodeAt(100, PathNodeType.None, -1), false);
            SetPrivateField(editor, "movePreviewModel", preview.PathModel);
            _ = editor.CommitPlacement();

            PathRouteResolution resolution = editor.ResolveCurrent(editor.TryCaptureCurrentPathModel());

            Assert.IsFalse(HasDiagnostic(resolution, PathRouteDiagnosticCode.MissingEndNode));
        }

        [TestMethod]
        public void WhenStartAnchorIsSetDirectlyThenItCommitsWithoutPlacementMode()
        {
            PathModel source = CreateEditablePath();
            using PathEditor editor = CreateEditor(source);

            PathEditorCommandResult result = editor.SetStartAnchorCommand(CreateNodeAt(25, PathNodeType.None, -1), false);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(editor.IsPlacementActive);
            Assert.AreEqual(25f, editor.TryCaptureCurrentPathModel().PathNodes[0].Location.Location.X);
            Assert.IsTrue(editor.CanPlaceEndAnchor);
        }

        [TestMethod]
        public void WhenPlacementReleaseFollowsADragThenAnchorIsNotCommitted()
        {
            // Panning the map during placement must not drop the anchor at the drag-release location.
            using PathEditor editor = CreateNewEditor();
            _ = editor.BeginStartAnchorPlacementCommand();
            PathModel source = editor.TryCaptureCurrentPathModel();
            PathEditResult preview = PathModelEditor.SetStartAnchor(source, CreateNodeAt(25, PathNodeType.None, -1), false);
            SetPrivateField(editor, "movePreviewModel", preview.PathModel);

            editor.MouseDragged(new UserCommandArgs(), KeyModifiers.None);
            editor.MouseReleasedLeft(new UserCommandArgs(), KeyModifiers.None);

            Assert.IsTrue(editor.IsPlacingStartAnchor);
        }

        [TestMethod]
        public void WhenAnchoredViaPlacementBeginsThenInsertedNodeUsesProvidedAnchor()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                PathNode anchor = CreateNodeAt(50, PathNodeType.Intermediate, -1);

                PathEditResult result = editor.BeginViaPointPlacementAt(0, anchor);

                Assert.IsTrue(result.Success);
                Assert.AreEqual(anchor.Location, editor.TryCaptureCurrentPathModel().PathNodes[1].Location);
            }
        }

        [TestMethod]
        public void WhenPendingViaPlacementIsCanceledThenHistoryAndSourcePathAreRestored()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.BeginViaPointPlacementAt(0, CreateNodeAt(50, PathNodeType.Intermediate, -1));

            bool canceled = editor.CancelMoveNode();

                Assert.IsTrue(canceled);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.IsFalse(editor.CanUndo);
                Assert.IsFalse(editor.CanRedo);
            }
        }

        [TestMethod]
        public void WhenPendingViaPlacementIsCommittedThenSingleUndoRestoresSourcePath()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.BeginViaPointPlacementAt(0, CreateNodeAt(50, PathNodeType.Intermediate, -1));
                PathModel insertedModel = editor.TryCaptureCurrentPathModel();
                SetPrivateField(editor, "movePreviewModel", insertedModel);

                PathEditResult committed = editor.CommitMoveNode();
                bool undone = editor.Undo();

                Assert.IsTrue(committed.Success);
                Assert.IsTrue(undone);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.IsFalse(editor.CanUndo);
            }
        }

        [TestMethod]
        public void WhenViaPointIsAddedHereThenBothAdjacentSpansAreCommittedAsOneUndoableEdit()
        {
            PathModel source = CreateEditablePath();
            using PathEditor editor = CreateEditor(source);

            PathEditorCommandResult result = editor.AddViaPointHereCommand(0, CreateNodeAt(50, PathNodeType.None, -1), false);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, editor.TryCaptureCurrentPathModel().PathNodes.Length);
            Assert.IsTrue(editor.Undo());
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
        }

        [TestMethod]
        public void WhenViaPointSplitHasEqualCostRoutesThenCandidatesAreExposedWithoutCommitting()
        {
            PathModel source = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNodeAt(100, PathNodeType.Start, 1) with { NodeIndex = 1 },
                    CreateNodeAt(400, PathNodeType.End, -1) with { NodeIndex = 4 }),
            };
            using PathEditor editor = CreateEditor(source, CreateAmbiguousTrackWorld());

            PathEditorCommandResult result = editor.AddViaPointHereCommand(0,
                CreateNodeAt(300, PathNodeType.None, -1) with { NodeIndex = 3 }, true);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit);
            Assert.IsFalse(editor.CanUndo);
            Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
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
            return CreateEditor(pathModel, TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld());
        }

        private static PathEditor CreateEditor(PathModel pathModel, TrackWorld trackWorld)
        {
            PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld));
            editor.InitializeNewPath();
            typeof(PathEditor).GetMethod("RestoreSnapshot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { pathModel });
            return editor;
        }

        private static PathEditor CreateNewEditor()
        {
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
            PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld));
            editor.InitializeNewPath();
            return editor;
        }

        // A transient new path carrying only a committed start anchor, the state from which a span commit
        // resolves last-anchor to target. The history is cleared so each test observes only its own snapshot.
        private static PathEditor CreateEditorWithStartAnchor()
        {
            PathEditor editor = CreateNewEditor();
            _ = editor.SetStartAnchorCommand(CreateNodeAt(0, PathNodeType.None, -1), false);
            _ = editor.CancelPlacement();
            typeof(PathEditor).GetMethod("ClearHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, null);
            return editor;
        }

        // An anchor that cannot be anchored to track: its track node index does not exist and its location lies
        // far away from any track, so the affected span stays unresolved.
        private static PathNode CreateUnroutableNode()
        {
            return new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(5000, 0, 0)))
            {
                NodeType = PathNodeType.None,
                NodeIndex = 99,
                NextMainNode = -1,
                NextSidingNode = -1,
            };
        }

        private static PathModel CreateAmbiguousEndpointPath()
        {
            return new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNodeAt(100, PathNodeType.Start, -1)),
            };
        }

        private static TrackWorld CreateAmbiguousTrackWorld()
        {
            TrackDatabase trackDatabase = new TrackDatabase()
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3), CreateJunctionNode(4)),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3, 4), CreateConnectors(2, 3, 4),
                    CreateConnectors(3, 1, 2), CreateConnectors(4, 1, 2)),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(trackDatabase);
            TrackModel trackModel = new TrackModel() { TrackDatabase = trackDatabase };

            return TrackWorld.Initialize(null, trackModel, new TrackSectionModel());
        }

        private static VectorNode CreateVectorNode(int nodeIndex)
        {
            WorldLocation start = new WorldLocation(new Tile(0, 0), new Vector3(nodeIndex * 100, 0, 0));
            WorldLocation end = new WorldLocation(new Tile(0, 0), new Vector3((nodeIndex * 100) + 50, 0, 0));
            return new VectorNode(start, new Tile(0, 0), end) { NodeIndex = nodeIndex };
        }

        private static JunctionNode CreateJunctionNode(int nodeIndex)
        {
            return new JunctionNode(new WorldLocation(new Tile(0, 0), new Vector3(nodeIndex * 100, 0, 0)),
                new Tile(0, 0), Vector3.Zero) { NodeIndex = nodeIndex };
        }

        private static TrackNodeConnectorIndex CreateConnectors(int nodeIndex, params int[] linkedNodeIndexes)
        {
            return new TrackNodeConnectorIndex()
            {
                NodeIndex = nodeIndex,
                TrackNodeConnectors = linkedNodeIndexes.Select(link => new TrackNodeConnector() { Link = link }).ToImmutableArray(),
            };
        }

        private static PathRouteResolution CreateResolution(params ResolvedPathSpan[] spans)
        {
            ResolvedPathRoute route = new ResolvedPathRoute(PathRouteBranchKind.Main, 0, spans[^1].ToNodeIndex, spans.ToImmutableArray());
            return new PathRouteResolution(route, ImmutableArray<PathRouteDiagnostic>.Empty);
        }

        private static ImmutableArray<ResolvedPathSpan> InvokeAffectedSpans(PathRouteResolution resolution, ImmutableArray<int> changedNodeIndexes)
        {
            MethodInfo method = typeof(PathEditor).GetMethod("AffectedSpans", BindingFlags.Static | BindingFlags.NonPublic);
            return (ImmutableArray<ResolvedPathSpan>)method.Invoke(null, new object[] { resolution, changedNodeIndexes });
        }

        private static PathEditResult InvokeExtendPathToAnchor(PathModel pathModel, PathNode anchor)
        {
            MethodInfo method = typeof(PathEditor).GetMethod("ExtendPathToAnchor", BindingFlags.Static | BindingFlags.NonPublic);
            return (PathEditResult)method.Invoke(null, new object[] { pathModel, anchor, false });
        }

        private static bool HasNodeType(PathModel pathModel, PathNodeType nodeType)
        {
            return pathModel.PathNodes.Any(node => node.NodeType.Includes(nodeType));
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
