using System.Collections.Immutable;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PathEditing;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

namespace Tests.FreeTrainSimulator.Toolbox.PathEditing
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
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
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.Via, -1), CreateNode(PathNodeType.End, -1)),
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
        public async Task WhenEmptyPathIsLoadedThenEditorEntersRepairModeWithoutRuntimePath()
        {
            PathModel source = new PathModel { Id = "empty", Name = "Empty", PathNodes = ImmutableArray<PathNode>.Empty };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld())))
            {
                bool initialized = await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false);

                Assert.IsTrue(initialized);
                Assert.IsTrue(editor.IsRepairMode);
                Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                Assert.IsNull(editor.TrainPath);
            }
        }

        [TestMethod]
        public void WhenSecondReversalWouldBeSetThenMaterializedTraversalRejectsItWithoutChangingHistory()
        {
            using (PathEditor editor = CreateEditor(CreateMainEventPath(PathNodeType.Reversal, PathNodeType.None)))
            {
                PathModel source = editor.TryCaptureCurrentPathModel();
                bool dirty = editor.HasUnsavedChanges;

                PathEditResult result = editor.SetReversalPoint(2);

                Assert.AreEqual((false, false, source, false, dirty),
                    (result.Success, editor.CanSetReversalPoint(2), editor.TryCaptureCurrentPathModel(), editor.CanUndo, editor.HasUnsavedChanges));
            }
        }

        [TestMethod]
        public void WhenReversalIsInsertedBeforeExistingReversalThenTheLaterViolationStillBlocksCommit()
        {
            using (PathEditor editor = CreateEditor(CreateMainEventPath(PathNodeType.None, PathNodeType.Reversal)))
            {
                PathModel source = editor.TryCaptureCurrentPathModel();

                PathEditResult result = editor.SetReversalPoint(1);

                Assert.AreEqual((false, source), (result.Success, editor.TryCaptureCurrentPathModel()));
            }
        }

        [TestMethod]
        public void WhenWaitSeparatesTwoReversalsThenWaitDoesNotResetTheMaterializedEventGuard()
        {
            using (PathEditor editor = CreateEditor(CreateMainEventPath(PathNodeType.Reversal, PathNodeType.Wait)))
            {
                PathEditResult result = editor.SetReversalPoint(3);

                Assert.IsFalse(result.Success);
            }
        }

        [TestMethod]
        public void WhenImportedPathAlreadyHasRepeatedReversalsThenAnUnrelatedWaitRemainsEditable()
        {
            using (PathEditor editor = CreateEditor(CreateMainEventPath(PathNodeType.Reversal, PathNodeType.Reversal)))
            {
                PathEditResult result = editor.SetWaitPoint(3, 25);

                Assert.IsTrue(result.Success && editor.TryCaptureCurrentPathModel().PathNodes[3].WaitInfo.WaitTime == 25);
            }
        }

        [TestMethod]
        public void WhenFirstEventIsAlreadyUnsupportedThenSettingAnOrdinaryReversalIsNotBlanketBlocked()
        {
            PathModel source = CreateMainEventPath(PathNodeType.None, PathNodeType.None);
            source = source with { PathNodes = source.PathNodes.SetItem(0, source.PathNodes[0] with { NodeType = PathNodeType.Start | PathNodeType.Wait }) };
            using (PathEditor editor = CreateEditor(source))
            {
                PathEditResult result = editor.SetReversalPoint(1);

                Assert.IsTrue(result.Success);
            }
        }

        [TestMethod]
        public void WhenWaitCapabilityIsPolledThenItTracksTheCurrentModelAfterReversalEdit()
        {
            using (PathEditor editor = CreateEditor(CreateMainEventPath(PathNodeType.None, PathNodeType.None)))
            {
                Assert.IsTrue(editor.CanSetWaitPoint(1));
                Assert.IsTrue(editor.SetReversalPoint(1).Success);

                Assert.IsFalse(editor.CanSetWaitPoint(1));
            }
        }

        [TestMethod]
        [DataRow(PathNodeType.Wait, 17)]
        [DataRow(PathNodeType.Reversal, 0)]
        public async Task WhenUnsupportedFirstEventIsMovedAlongOrdinaryTrackThenPreviewAndCommitRetainItAndUndoRestoresIt(PathNodeType eventType, int waitTime)
        {
            TrackWorld world = CreatePlacementTrackWorld(out TrackSegmentBase segment);
            PathModel source = CreateEditablePath();
            source = source with
            {
                PathNodes = source.PathNodes.SetItem(0, source.PathNodes[0] with
                {
                    NodeType = PathNodeType.Start | eventType,
                    WaitInfo = new PathNodeWaitInfo { WaitTime = waitTime },
                }),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false));
                Assert.IsFalse(editor.IsRepairMode);
                Assert.IsTrue(editor.BeginMoveNode(0));

                editor.UpdatePointerLocation(new PointD(25, 0), segment);

                Assert.AreEqual((true, source, false, false),
                    (editor.CanCommitMoveNode, editor.TryCaptureCurrentPathModel(), editor.CanUndo, editor.HasUnsavedChanges));

                PathEditResult result = editor.CommitMoveNode();
                PathModel moved = editor.TryCaptureCurrentPathModel();
                Assert.AreEqual((true, PointD.ToWorldLocation(segment.SnapToSegment(new PointD(25, 0))), true, source.PathNodes[0].WaitInfo, true, true),
                    (result.Success, moved.PathNodes[0].Location, moved.PathNodes[0].NodeType.Includes(eventType), moved.PathNodes[0].WaitInfo,
                        editor.CanUndo, editor.HasUnsavedChanges));
                PathPersistenceValidationResult validation = editor.ValidateCurrentPathForPersistence();
                Assert.AreEqual((false, PathRouteDiagnosticCode.UnsupportedFirstMainEvent, 0),
                    (validation.PersistenceAllowed, validation.HighestActionableDiagnostic.Code, validation.HighestActionableDiagnostic.NodeIndex));

                Assert.IsTrue(editor.Undo());
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.AreEqual((false, true), (editor.CanUndo, editor.CanRedo));
            }
        }

        [TestMethod]
        public async Task WhenFirstJunctionWaitIsMovedToOrdinaryTrackThenPartialRepairCommitsButStrictSaveRemainsBlocked()
        {
            Tile tile = new Tile(1, 0);
            TrackWorld world = CreateEventJunctionTrackWorld(out TrackSegmentBase segment);
            PathNode target = CreateNodeAt(tile, 25, PathNodeType.None, -1);
            PathModel source = new PathModel
            {
                Id = "first-junction-wait",
                PathNodes = ImmutableArray.Create(
                    CreateAnchoredNode(world, 2, PathNodeType.Start | PathNodeType.Junction | PathNodeType.Wait, 1, -1) with
                    { WaitInfo = new PathNodeWaitInfo { WaitTime = 17 } },
                    CreateNodeAt(tile, 100, PathNodeType.End, -1)),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false));
                Assert.IsFalse(editor.IsRepairMode);
                Assert.IsTrue(editor.BeginMoveNode(0));

                editor.UpdatePointerLocation(PointD.FromWorldLocation(target.Location), segment);

                Assert.AreEqual((true, source, false), (editor.CanCommitMoveNode, editor.TryCaptureCurrentPathModel(), editor.CanUndo));

                PathEditResult result = editor.CommitMoveNode();
                PathNode moved = editor.TryCaptureCurrentPathModel().PathNodes[0];
                Assert.AreEqual((true, PointD.ToWorldLocation(segment.SnapToSegment(PointD.FromWorldLocation(target.Location))), 1, PathNodeType.Start | PathNodeType.Wait, source.PathNodes[0].WaitInfo),
                    (result.Success, moved.Location, moved.NodeIndex, moved.NodeType, moved.WaitInfo));
                PathPersistenceValidationResult validation = editor.ValidateCurrentPathForPersistence();
                Assert.AreEqual((false, PathRouteDiagnosticCode.UnsupportedFirstMainEvent, 0),
                    (validation.PersistenceAllowed, validation.HighestActionableDiagnostic.Code, validation.HighestActionableDiagnostic.NodeIndex));

                Assert.IsTrue(editor.Undo());
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.AreEqual((false, true), (editor.CanUndo, editor.CanRedo));
            }
        }

        [TestMethod]
        public async Task WhenUnsupportedSecondReversalIsRelocatedThenPreviewAndCommitRetainTheViolationAndUndoRestoresIt()
        {
            TrackWorld world = CreatePlacementTrackWorld(out TrackSegmentBase segment);
            PathModel source = new PathModel
            {
                Id = "reversal-occurrences",
                PathNodes = ImmutableArray.Create(CreateNodeAt(0, PathNodeType.Start, 1),
                    CreateNodeAt(80, PathNodeType.Via | PathNodeType.Reversal, 2),
                    CreateNodeAt(20, PathNodeType.Via | PathNodeType.Reversal, 3), CreateNodeAt(100, PathNodeType.End, -1)),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false));
                Assert.IsFalse(editor.IsRepairMode);
                Assert.IsTrue(editor.BeginMoveNode(2));

                editor.UpdatePointerLocation(new PointD(25, 0), segment);

                Assert.AreEqual((true, source, false), (editor.CanCommitMoveNode, editor.TryCaptureCurrentPathModel(), editor.CanUndo));

                PathEditResult result = editor.CommitMoveNode();
                PathModel moved = editor.TryCaptureCurrentPathModel();
                Assert.AreEqual((true, PointD.ToWorldLocation(segment.SnapToSegment(new PointD(25, 0))), true, true, true),
                    (result.Success, moved.PathNodes[2].Location, moved.PathNodes[1].NodeType.Includes(PathNodeType.Reversal),
                        moved.PathNodes[2].NodeType.Includes(PathNodeType.Reversal), editor.CanUndo));
                PathPersistenceValidationResult validation = editor.ValidateCurrentPathForPersistence();
                Assert.AreEqual((false, PathRouteDiagnosticCode.MultipleReversalsInContinuousVector, 2),
                    (validation.PersistenceAllowed, validation.HighestActionableDiagnostic.Code, validation.HighestActionableDiagnostic.NodeIndex));

                Assert.IsTrue(editor.Undo());
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.AreEqual((false, true), (editor.CanUndo, editor.CanRedo));
            }
        }

        [TestMethod]
        public async Task WhenExistingWaitJunctionIsInferredDuringMaterializationThenOrdinaryWaitCapabilityMatchesTheActualEdit()
        {
            Tile tile = new Tile(1, 0);
            TrackWorld world = CreateEventJunctionTrackWorld(out _);
            PathModel source = new PathModel
            {
                Id = "inferred-junction-wait",
                PathNodes = ImmutableArray.Create(
                    CreateAnchoredNode(world, 3, PathNodeType.Start, 1, -1),
                    CreateAnchoredNode(world, 2, PathNodeType.Via | PathNodeType.Wait, 2, -1) with
                    { WaitInfo = new PathNodeWaitInfo { WaitTime = 17 } },
                    CreateNodeAt(tile, 40, PathNodeType.Via, 3), CreateNodeAt(tile, 100, PathNodeType.End, -1)),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false));
                PathPersistenceValidationResult materialization = PathPersistenceValidationPolicy.ResolveAndMaterialize(source, world);
                Assert.AreEqual((true, false, true), (materialization.PersistenceAllowed, source.PathNodes[1].NodeType.Includes(PathNodeType.Junction),
                    materialization.PathModel.PathNodes[1].NodeType.Includes(PathNodeType.Junction)));

                bool canSet = editor.CanSetWaitPoint(2);

                Assert.AreEqual((true, source, false, false), (canSet, editor.TryCaptureCurrentPathModel(), editor.CanUndo, editor.HasUnsavedChanges));

                PathEditResult result = editor.SetWaitPoint(2, 25);

                Assert.AreEqual((canSet, 25, 17, true, true),
                    (result.Success, editor.TryCaptureCurrentPathModel().PathNodes[2].WaitInfo.WaitTime,
                        editor.TryCaptureCurrentPathModel().PathNodes[1].WaitInfo.WaitTime, editor.CanUndo, editor.HasUnsavedChanges));
                PathPersistenceValidationResult validation = editor.ValidateCurrentPathForPersistence();
                Assert.AreEqual((false, PathRouteDiagnosticCode.UnsupportedJunctionEvent, 1),
                    (validation.PersistenceAllowed, validation.HighestActionableDiagnostic.Code, validation.HighestActionableDiagnostic.NodeIndex));

                Assert.IsTrue(editor.Undo());
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.AreEqual((false, true), (editor.CanUndo, editor.CanRedo));
            }
        }

        [TestMethod]
        public void WhenEventDiagnosticsArePolledThenTheyAreCachedAndInvalidatedByRepair()
        {
            using (PathEditor editor = CreateEditor(CreateMainEventPath(PathNodeType.Wait | PathNodeType.Reversal, PathNodeType.None)))
            {
                ImmutableArray<PathRouteDiagnostic> first = editor.GetSimulatorEventDiagnostics();
                ImmutableArray<PathRouteDiagnostic> second = editor.GetSimulatorEventDiagnostics();
                Assert.AreEqual(first, second);
                Assert.AreEqual(PathRouteDiagnosticCode.UnsupportedCombinedWaitReversal, first.Single().Code);

                Assert.IsTrue(editor.ClearWaitPoint(1).Success);

                Assert.IsTrue(editor.GetSimulatorEventDiagnostics().IsEmpty);
            }
        }

        [TestMethod]
        public void WhenFatalDraftHasFirstWaitThenBoundedDiagnosticsAndClearRemainAvailable()
        {
            PathModel source = CreateEditablePath();
            source = source with
            {
                PathNodes = source.PathNodes.SetItem(0, source.PathNodes[0] with { NodeType = PathNodeType.Start | PathNodeType.Wait, NextMainNode = 10 }),
            };
            using (PathEditor editor = CreateEditor(source))
            {
                Assert.IsTrue(editor.IsRepairMode && editor.CanClearWaitPoint(0));
                Assert.AreEqual((PathRouteDiagnosticCode.UnsupportedFirstMainEvent, 0),
                    (editor.GetSimulatorEventDiagnostics().Single().Code, editor.GetSimulatorEventDiagnostics().Single().NodeIndex));

                PathEditResult result = editor.ClearWaitPoint(0);

                Assert.IsTrue(result.Success && editor.IsRepairMode);
            }
        }

        [TestMethod]
        public void WhenImportedFirstReversalIsInvalidThenClearDoesNotRequireAnnotationEligibility()
        {
            PathModel source = CreateEditablePath();
            source = source with { PathNodes = source.PathNodes.SetItem(0, source.PathNodes[0] with { NodeType = PathNodeType.Start | PathNodeType.Reversal }) };
            using (PathEditor editor = CreateEditor(source))
            {
                Assert.IsTrue(editor.CanClearReversalPoint(0));

                PathEditResult result = editor.ClearReversalPoint(0);

                Assert.IsTrue(result.Success && !editor.TryCaptureCurrentPathModel().PathNodes[0].NodeType.Includes(PathNodeType.Reversal));
            }
        }

        [TestMethod]
        public void WhenSnapRepairsUnsupportedDraftThenSimulatorCompatibilityDoesNotVetoMaterialization()
        {
            PathModel source = CreateEditablePath();
            source = source with { PathNodes = source.PathNodes.SetItem(0, source.PathNodes[0] with { NodeType = PathNodeType.Start | PathNodeType.Wait }) };

            PathEditResult result = PathEditor.SnapPathToTrack(source, TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld());

            Assert.IsTrue(result.Success && result.PathModel.PathNodes[0].NodeType.Includes(PathNodeType.Wait));
        }

        [TestMethod]
        public void WhenPassingPathViaIsRemovedThenAnUnrelatedUnsupportedEventDoesNotBlockRepair()
        {
            TrackWorld world = CreateEventSidingTrackWorld();
            PathModel source = CreateResolvablePassingBranchPath(world);
            source = source with { PathNodes = source.PathNodes.SetItem(0, source.PathNodes[0] with { NodeType = PathNodeType.Start | PathNodeType.Wait }) };
            source = PathModelEditor.InsertViaPoint(source, 1, source.PathNodes[1], false).PathModel;
            using (PathEditor editor = CreateEditor(source, world))
            {
                PathEditResult result = editor.RemoveViaPoint(2);

                Assert.IsTrue(result.Success && result.PathModel.PathNodes[0].NodeType.Includes(PathNodeType.Wait), result.Message);
            }
        }

        [TestMethod]
        public void WhenPassingPathIsTruncatedAtRejoinThenAnUnrelatedUnsupportedEventDoesNotBlockRepair()
        {
            TrackWorld world = CreateEventSidingTrackWorld();
            PathModel source = CreateResolvablePassingBranchPath(world);
            source = source with
            {
                PathNodes = source.PathNodes
                    .SetItem(0, source.PathNodes[0] with { NodeType = PathNodeType.Start | PathNodeType.Wait })
                    .SetItem(2, source.PathNodes[2] with { NodeType = PathNodeType.Via, NextMainNode = 4 })
                    .Add(source.PathNodes[2]),
            };
            using (PathEditor editor = CreateEditor(source, world))
            {
                PathEditResult result = editor.RemoveRestOfPath(2);

                Assert.IsTrue(result.Success && result.PathModel.PathNodes[0].NodeType.Includes(PathNodeType.Wait)
                    && result.PathModel.PathNodes.Any(node => node.NextSidingNode >= 0), result.Message);
            }
        }

        [TestMethod]
        public void WhenRouteExtensionWouldAutomaticallyAddASecondContinuousReversalThenPlacementIsRejected()
        {
            TrackWorld world = CreatePlacementTrackWorld(out _);
            PathModel source = new PathModel
            {
                PathNodes = ImmutableArray.Create(CreateNodeAt(0, PathNodeType.Start, 1),
                    CreateNodeAt(80, PathNodeType.Via | PathNodeType.Reversal, 2), CreateNodeAt(20, PathNodeType.End, -1)),
            };
            using (PathEditor editor = CreateEditor(source, world))
            {
                Assert.IsTrue(editor.ContinuePathCommand().Success);
                PathModel baseline = editor.TryCaptureCurrentPathModel();

                PathEditorCommandResult result = editor.AddRoutePointHereCommand(CreateNodeAt(60, PathNodeType.None, -1), false, false);

                Assert.AreEqual((false, baseline, false), (result.Success, editor.TryCaptureCurrentPathModel(), editor.CanUndo));
            }
        }

        [TestMethod]
        public void WhenPassingBranchRejoinIsSelectedThenConflictingEditsAreUnavailable()
        {
            PathModel source = CreateTruncationPassingBranchPath() with
            {
                PathNodes = CreateTruncationPassingBranchPath().PathNodes.Select(node => node with { NextSidingNode = -1 }).ToImmutableArray(),
            };
            using (PathEditor editor = CreateEditor(source))
            {
                Assert.IsTrue(editor.BeginPassingBranchCommand(1).Success);

                Assert.IsFalse(editor.CanRemoveViaPoint(2));
                Assert.IsFalse(editor.CanRemoveRestOfPath(2));
                Assert.IsFalse(editor.AddViaPointHereCommand(2, CreateNodeAt(50, PathNodeType.None, -1), false).Success);
            }
        }

        [TestMethod]
        public void WhenPassingBranchRejoinIsSelectedThenOnlyLaterMainRouteNodeCanComplete()
        {
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSidingTrackWorld();
            PathModel source = CreateSidingEndpointPath(trackWorld);
            using (PathEditor editor = CreateEditor(source, trackWorld))
            {
                Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);

                Assert.IsFalse(editor.CanCompletePassingBranch(0));
                Assert.IsTrue(editor.CanCompletePassingBranch(1));
            }
        }

        [TestMethod]
        public void WhenPassingRejoinHasNoPhysicalAlternativeThenCompletionIsUnavailable()
        {
            using (PathEditor editor = CreateEditor(CreateEditablePath()))
            {
                Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);

                Assert.IsFalse(editor.CanCompletePassingBranch(1));
            }
        }

        [TestMethod]
        public void WhenPassingBranchSourceHasDisconnectedNodeThenCompletionIsUnavailable()
        {
            PathModel source = CreateEditablePath() with
            {
                PathNodes = CreateEditablePath().PathNodes.Add(CreateNodeAt(50, PathNodeType.Via, -1)),
            };
            using (PathEditor editor = CreateEditor(source))
            {
                Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);

                Assert.IsFalse(editor.CanCompletePassingBranch(1));
                Assert.IsFalse(editor.CanCompletePassingBranch(2));
            }
        }

        [TestMethod]
        public async Task WhenCyclicPathIsLoadedThenEditorEntersRepairModeWithoutRuntimePath()
        {
            PathModel source = new PathModel
            {
                Id = "cycle",
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.Via, 0), CreateNode(PathNodeType.End, -1)),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld())))
            {
                bool initialized = await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false);

                Assert.IsTrue(initialized);
                Assert.IsTrue(editor.IsRepairMode);
                Assert.IsNull(editor.TrainPath);
                Assert.IsFalse(editor.CanContinuePath);
                Assert.IsFalse(editor.ContinuePathCommand().Success);
                Assert.IsFalse(editor.IsBuildingRoute);
            }
        }

        [TestMethod]
        public async Task WhenRepairAddsMissingEndThenEditorTransitionsToNormalModeAndUndoRestoresRepairMode()
        {
            PathModel source = new PathModel
            {
                Id = "missing-end",
                PathNodes = ImmutableArray.Create(CreateNodeAt(0, PathNodeType.Start, -1)),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld())))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false));
                editor.SelectAuthoredNode(0);
                Assert.AreEqual(0, editor.SelectedAuthoredNodeIndex);

                PathEditorCommandResult repair = editor.SetEndAnchorCommand(CreateNodeAt(100, PathNodeType.None, -1), false);

                Assert.IsTrue(repair.Success);
                Assert.IsFalse(editor.IsRepairMode);
                Assert.IsNotNull(editor.TrainPath);
                Assert.AreEqual(0, editor.SelectedAuthoredNodeIndex);
                Assert.IsTrue(editor.HasUnsavedChanges);
                Assert.IsTrue(editor.CanUndo);

                Assert.IsTrue(editor.Undo());
                Assert.IsTrue(editor.IsRepairMode);
                Assert.IsNull(editor.TrainPath);
                Assert.AreEqual(0, editor.SelectedAuthoredNodeIndex);
                Assert.IsTrue(editor.Redo());
                Assert.IsFalse(editor.IsRepairMode);
            }
        }

        [TestMethod]
        public async Task WhenPathHasInvalidLinkThenEditorEntersRepairModeWithoutRuntimePath()
        {
            PathModel source = new PathModel
            {
                Id = "invalid-link",
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 9), CreateNode(PathNodeType.End, -1)),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld())))
            {
                bool initialized = await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false);

                Assert.IsTrue(initialized);
                Assert.IsTrue(editor.IsRepairMode);
                Assert.IsNull(editor.TrainPath);
            }
        }

        [TestMethod]
        public async Task WhenPathHasNoEndThenEditorEntersRepairModeWithoutRuntimePath()
        {
            PathModel source = new PathModel
            {
                Id = "missing-end",
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, -1)),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld())))
            {
                bool initialized = await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false);

                Assert.IsTrue(initialized);
                Assert.IsTrue(editor.IsRepairMode);
                Assert.IsNull(editor.TrainPath);
            }
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

            string context = PathEditor.BuildSnapshotContext(path, trainPath, true, false, true, false);

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
            string context = PathEditor.BuildSnapshotContext(null, null, false, false, false, true);

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
            // Regression: undoing after an endpoint was set restores a partial path (Start + via,
            // no End yet). The TrainPathBase constructor must not throw for an incomplete path during editing;
            // completeness is validated separately by PathRouteResolver.
            PathModel partialPath = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.Via, -1)),
            };

            TestTrainPath trainPath = new TestTrainPath(partialPath);

            Assert.IsNotNull(trainPath);
        }

        [TestMethod]
        public void WhenNewPathIsInitializedThenNoPlacementModeIsActive()
        {
            using (PathEditor editor = CreateNewEditor())
            {
                Assert.IsFalse(editor.IsPlacementActive);
            }
        }

        [TestMethod]
        public void WhenNewPathIsInitializedThenItIsIdentifiedAsDiscardable()
        {
            using (PathEditor editor = CreateNewEditor())
            {
                Assert.IsTrue(editor.IsNewPath);
            }
        }

        [TestMethod]
        public void WhenInitialStartAnchorIsCommittedThenBuildRouteBegins()
        {
            using (PathEditor editor = CreateNewEditor())
            {
                PathEditorCommandResult result = editor.SetStartAnchorCommand(CreateNodeAt(0, PathNodeType.None, -1), false);

                Assert.IsTrue(result.Success);
                Assert.IsTrue(editor.IsBuildingRoute);
            }
        }

        [TestMethod]
        public void WhenBuildRouteIsCanceledThenCommittedRoutePointsAreRetained()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                Assert.IsFalse(editor.IsBuildingRoute);
                _ = editor.ContinuePathCommand();

                bool canceled = editor.CancelPlacement();

                Assert.IsTrue(canceled);
                Assert.IsFalse(editor.IsBuildingRoute);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenBuildRouteIsFinishedThenProvisionalEndIsRetained()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.ContinuePathCommand();

                PathEditorCommandResult result = editor.FinishPathCommand();

                Assert.IsTrue(result.Success);
                Assert.IsFalse(editor.IsBuildingRoute);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenContinuePathBeginsThenBuildRouteModeIsActiveWithoutMutatingThePath()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                PathEditorCommandResult result = editor.ContinuePathCommand();

                Assert.IsTrue(result.Success);
                Assert.IsTrue(editor.IsBuildingRoute);
                Assert.IsFalse(editor.EditMode);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public async Task WhenCleanPathIsReopenedThenContinuationDoesNotDirtyItUntilRoutePointIsCommitted()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld())))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false));
                Assert.IsFalse(editor.HasUnsavedChanges);
                Assert.IsTrue(editor.CanContinuePath);

                Assert.IsTrue(editor.ContinuePathCommand().Success);
                Assert.IsFalse(editor.CanContinuePath);
                Assert.IsFalse(editor.HasUnsavedChanges);
                Assert.IsTrue(editor.CancelPlacement());
                Assert.IsTrue(editor.CanContinuePath);
                Assert.IsFalse(editor.HasUnsavedChanges);

                Assert.IsTrue(editor.ContinuePathCommand().Success);
                PathEditResult preview = InvokeAddRoutePoint(source, CreateNodeAt(200, PathNodeType.None, -1));
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);

                Assert.IsTrue(editor.CommitPlacement().Success);
                Assert.IsTrue(editor.HasUnsavedChanges);
            }
        }

        [TestMethod]
        public async Task WhenLoadedPathHasDisconnectedNodeThenStartPlacementPreservesCommittedState()
        {
            PathModel source = CreateEditablePath() with
            {
                PathNodes = CreateEditablePath().PathNodes.Add(CreateNodeAt(50, PathNodeType.Via, -1)),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld())))
            {
                _ = await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false);
                (PathModel, bool HasUnsavedChanges, bool CanUndo, bool CanRedo) originalState = (editor.TryCaptureCurrentPathModel(), editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo);
                _ = editor.BeginStartAnchorPlacementCommand();

                _ = editor.SetStartAnchorCommand(CreateNodeAt(25, PathNodeType.None, -1), false);

                Assert.AreEqual(originalState, (editor.TryCaptureCurrentPathModel(), editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo));
            }
        }

        [TestMethod]
        public async Task WhenLoadedPathHasDisconnectedNodeThenStartPlacementReportsPreservationFailure()
        {
            PathModel source = CreateEditablePath() with
            {
                PathNodes = CreateEditablePath().PathNodes.Add(CreateNodeAt(50, PathNodeType.Via, -1)),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld())))
            {
                _ = await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false);
                _ = editor.BeginStartAnchorPlacementCommand();

                PathEditorCommandResult result = editor.SetStartAnchorCommand(CreateNodeAt(25, PathNodeType.None, -1), false);

                Assert.Contains(nameof(PathRouteDiagnosticCode.UnreachableNode), result.Message);
            }
        }

        [TestMethod]
        public async Task WhenDisconnectedNodeWouldRequireRouteChoiceThenStartPlacementDoesNotCreatePendingCandidate()
        {
            PathModel source = new PathModel
            {
                Id = "disconnected-ambiguous-path",
                Name = "Disconnected Ambiguous Path",
                PathNodes = ImmutableArray.Create(
                    CreateNodeAt(100, PathNodeType.Start, 1),
                    CreateNodeAt(200, PathNodeType.End, -1) with { NodeIndex = 2 },
                    CreateNodeAt(125, PathNodeType.Via, -1)),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(CreateAmbiguousTrackWorld())))
            {
                _ = await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false);
                _ = editor.BeginStartAnchorPlacementCommand();

                _ = editor.SetStartAnchorCommand(CreateNodeAt(110, PathNodeType.None, -1), false);

                Assert.IsFalse(editor.HasPendingAmbiguousSpanCommit);
            }
        }

        [TestMethod]
        [DataRow(0, 25)]
        [DataRow(1, 75)]
        public async Task WhenLoadedPathHasDisconnectedNodeThenMoveReportsPreservationFailure(int nodeIndex, int targetX)
        {
            PathModel source = CreateEditablePath() with
            {
                PathNodes = CreateEditablePath().PathNodes.Add(CreateNodeAt(50, PathNodeType.Via, -1)),
            };
            TrackWorld trackWorld = CreatePlacementTrackWorld(out TrackSegmentBase segment);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                _ = await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false);
                _ = editor.BeginMoveNode(nodeIndex);
                editor.UpdatePointerLocation(new PointD(targetX, 0), segment);

                PathEditResult result = editor.CommitMoveNode();

                Assert.Contains(nameof(PathRouteDiagnosticCode.UnreachableNode), result.Message);
            }
        }

        [TestMethod]
        [DataRow(0, 25)]
        [DataRow(1, 75)]
        public async Task WhenMoveIsRefusedThenCommittedContentAndExistingHistoryArePreserved(int nodeIndex, int targetX)
        {
            PathModel source = CreateEditablePath() with
            {
                PathNodes = CreateEditablePath().PathNodes.Add(CreateNodeAt(50, PathNodeType.Via | PathNodeType.Wait, -1)
                    with
                { WaitInfo = new PathNodeWaitInfo { WaitTime = 17 } }),
            };
            TrackWorld trackWorld = CreatePlacementTrackWorld(out TrackSegmentBase segment);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                _ = await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false);
                _ = editor.SetMetadataCommand("First edit", "Start", "End", false);
                _ = editor.SetMetadataCommand("Second edit", "Start", "End", false);
                _ = editor.Undo();
                (PathModel, bool HasUnsavedChanges, bool CanUndo, bool CanRedo) originalState = (editor.TryCaptureCurrentPathModel(), editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo);
                _ = editor.BeginMoveNode(nodeIndex);
                editor.UpdatePointerLocation(new PointD(targetX, 0), segment);

                _ = editor.CommitMoveNode();

                Assert.AreEqual(originalState, (editor.TryCaptureCurrentPathModel(), editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo));
            }
        }

        [TestMethod]
        public async Task WhenRefusedMoveIsCanceledThenCleanLoadedStateIsRestored()
        {
            PathModel source = CreateEditablePath() with
            {
                PathNodes = CreateEditablePath().PathNodes.Add(CreateNodeAt(50, PathNodeType.Via, -1)),
            };
            TrackWorld trackWorld = CreatePlacementTrackWorld(out TrackSegmentBase segment);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                _ = await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false);
                (PathModel, bool HasUnsavedChanges, bool CanUndo, bool CanRedo, bool EditMode, bool IsPlacementActive) originalState = (editor.TryCaptureCurrentPathModel(), editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo,
                    editor.EditMode, editor.IsPlacementActive);
                _ = editor.BeginMoveNode(0);
                editor.UpdatePointerLocation(new PointD(25, 0), segment);
                _ = editor.CommitMoveNode();

                _ = editor.CancelPlacement();

                Assert.AreEqual(originalState, (editor.TryCaptureCurrentPathModel(), editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo,
                    editor.EditMode, editor.IsPlacementActive));
            }
        }

        [TestMethod]
        public void WhenPathHasNoEndAnchorThenContinuationIsUnavailable()
        {
            using (PathEditor editor = CreateEditor(CreateEditablePath() with
            {
                PathNodes = ImmutableArray.Create(CreateNodeAt(0, PathNodeType.Start, -1)),
            }))
            {
                Assert.IsFalse(editor.CanContinuePath);
            }
        }

        [TestMethod]
        public void WhenBuildRouteIsDraggedThenPreviewedRoutePointIsNotCommitted()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.ContinuePathCommand();
                PathEditResult preview = InvokeAddRoutePoint(source, CreateNodeAt(200, PathNodeType.None, -1));
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);

                editor.MouseDragged(new UserCommandArgs(), KeyModifiers.None);
                editor.MouseReleasedLeft(new UserCommandArgs(), KeyModifiers.None);

                Assert.IsTrue(editor.IsBuildingRoute);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public async Task WhenPointerIsPressedAfterCanceledBuildRoutePanThenDragLatchClearsWithoutChangingCommittedState()
        {
            PathModel source = CreateEditablePath();
            UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand> commandController = new UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand>();
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()), commandController, action => action(), commit => Task.FromResult(commit())))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false));
                Assert.IsTrue(editor.ContinuePathCommand().Success);
                PathEditResult preview = InvokeAddRoutePoint(source, CreateNodeAt(200, PathNodeType.None, -1));
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);
                PathModel committedModel = editor.TryCaptureCurrentPathModel();
                TriggerPointerCommand(commandController, CommonUserCommand.PointerPressed, new PointerCommandArgs());
                TriggerPointerCommand(commandController, CommonUserCommand.PointerDragged, new PointerCommandArgs());
                Assert.IsTrue((bool)typeof(PathEditor).GetField("editorDragged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));

                TriggerPointerCommand(commandController, CommonUserCommand.PointerPressed, new PointerCommandArgs());

                Assert.IsFalse((bool)typeof(PathEditor).GetField("editorDragged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
                Assert.AreSame(committedModel, editor.TryCaptureCurrentPathModel());
                Assert.IsFalse(editor.HasUnsavedChanges);
                Assert.IsFalse(editor.CanUndo);
                Assert.IsFalse(editor.CanRedo);
                Assert.IsTrue(editor.IsBuildingRoute);
                Assert.AreSame(preview.PathModel, typeof(PathEditor).GetField("movePreviewModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
            }
        }

        [TestMethod]
        public async Task WhenClickFollowsCanceledBuildRoutePanThenPreviewIsCommittedExactlyOnceAndBuildSessionRemainsActive()
        {
            PathModel source = CreateEditablePath();
            UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand> commandController = new UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand>();
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()), commandController, action => action(), commit => Task.FromResult(commit())))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false));
                Assert.IsTrue(editor.ContinuePathCommand().Success);
                PathEditResult preview = InvokeAddRoutePoint(source, CreateNodeAt(200, PathNodeType.None, -1));
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);
                TriggerPointerCommand(commandController, CommonUserCommand.PointerPressed, new PointerCommandArgs());
                TriggerPointerCommand(commandController, CommonUserCommand.PointerDragged, new PointerCommandArgs());
                TriggerPointerCommand(commandController, CommonUserCommand.PointerPressed, new PointerCommandArgs());
                UserCommandArgs release = new PointerCommandArgs();

                TriggerPointerCommand(commandController, CommonUserCommand.PointerReleased, release);

                Assert.IsTrue(release.Handled);
                Assert.AreSequenceEqual(preview.PathModel.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.HasCount(3, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.IsTrue(editor.HasUnsavedChanges);
                Assert.IsTrue(editor.IsBuildingRoute);
                Assert.IsTrue(editor.CanUndo);
                Assert.IsFalse(editor.CanRedo);
                Assert.IsTrue(editor.Undo());
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.IsFalse(editor.CanUndo);
                Assert.IsTrue(editor.CanRedo);
            }
        }

        [TestMethod]
        [DataRow(CommonUserCommand.PointerPressed, true)]
        [DataRow(CommonUserCommand.PointerReleased, true)]
        [DataRow(CommonUserCommand.PointerDragged, false)]
        public void WhenInteractiveEditorIsDisposedThenPointerHandlerIsRemoved(CommonUserCommand command, bool dragged)
        {
            UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand> commandController = new UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand>();
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()), commandController,
                action => action(), commit => Task.FromResult(commit())))
            {
                editor.InitializeNewPath();
                SetPrivateField(editor, "editorDragged", true);
                TriggerPointerCommand(commandController, CommonUserCommand.PointerPressed, new PointerCommandArgs());
                Assert.IsFalse((bool)typeof(PathEditor).GetField("editorDragged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
                editor.Dispose();
                SetPrivateField(editor, "editorDragged", dragged);

                TriggerPointerCommand(commandController, command, new PointerCommandArgs());

                Assert.AreEqual(dragged, (bool)typeof(PathEditor).GetField("editorDragged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
            }
        }

        [TestMethod]
        public void WhenRoutePointIsCommittedThenBuildRouteRemainsActive()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.ContinuePathCommand();
                PathEditResult preview = InvokeAddRoutePoint(source, CreateNodeAt(200, PathNodeType.None, -1));
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);

                PathEditResult committed = editor.CommitPlacement();

                Assert.IsTrue(committed.Success);
                Assert.IsTrue(editor.IsBuildingRoute);
                Assert.HasCount(3, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenBuildRouteIsDoubleClickedThenPathFinishesWithoutCommittingDuplicatePoint()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.ContinuePathCommand();
                PathEditResult preview = InvokeAddRoutePoint(source, CreateNodeAt(200, PathNodeType.None, -1));
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);
                SetPrivateField(editor, "lastRoutePointClickTick", Environment.TickCount64);

                editor.MouseReleasedLeft(new PointerCommandArgs(), KeyModifiers.None);

                Assert.IsFalse(editor.IsBuildingRoute);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenRoutePointIsCommittedThenPreviousEndpointBecomesVia()
        {
            PathModel source = CreateEditablePath();

            PathEditResult result = InvokeAddRoutePoint(source, CreateNodeAt(200, PathNodeType.None, -1));

            Assert.IsTrue(result.PathModel.PathNodes[1].NodeType.Includes(PathNodeType.Via));
            Assert.IsFalse(result.PathModel.PathNodes[1].NodeType.Includes(PathNodeType.End));
        }

        [TestMethod]
        public void WhenBuildRouteContinuesThenGeneratedDisplayNodesAreNotReusedAsAuthoredPoints()
        {
            PathModel materialized = new PathModel
            {
                Id = "materialized",
                PathNodes = ImmutableArray.Create(
                    CreateNodeAt(0, PathNodeType.Start, 1),
                    CreateNodeAt(25, PathNodeType.Via, 2),
                    CreateNodeAt(50, PathNodeType.Via, 3),
                    CreateNodeAt(100, PathNodeType.End, -1)),
            };
            PathModel authored = new PathModel(materialized)
            {
                PathNodes = ImmutableArray.Create(
                    CreateNodeAt(0, PathNodeType.Start, 1),
                    CreateNodeAt(100, PathNodeType.End, -1)),
            };
            using (PathEditor editor = CreateEditor(materialized))
            {
                SetPrivateField(editor, "routeAuthoringModel", authored);

                PathEditorCommandResult result = editor.ContinuePathCommand();

                PathModel moveSource = (PathModel)typeof(PathEditor).GetField("moveSourceModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                Assert.IsTrue(result.Success);
                Assert.HasCount(2, moveSource.PathNodes);
            }
        }

        [TestMethod]
        public void WhenBuildRouteBeginsThenPointerInputModeRemainsActive()
        {
            TestPathEditorContext context = new TestPathEditorContext(CreateInitializedTrackWorld());
            using (PathEditor editor = new PathEditor(context))
            {
                editor.InitializeNewPath();
                _ = editor.SetStartAnchorCommand(CreateNodeAt(0, PathNodeType.None, -1), false);

                Assert.AreEqual(ToolboxContentMode.EditPath, context.ContentMode);
            }
        }

        [TestMethod]
        public void WhenPointerHasNearestTrackSegmentThenPreviewKeepsSegmentAnchor()
        {
            TrackWorld trackWorld = CreateInitializedTrackWorld();
            TestPathEditorContext context = new TestPathEditorContext(trackWorld);
            using (PathEditor editor = new PathEditor(context))
            {
                editor.InitializeNewPath();
                TestTrackSegment segment = new TestTrackSegment(new PointD(0, 0), new PointD(100, 0));

                editor.UpdatePointerLocation(segment.Location, segment);

                TrainPathPointBase activePoint = (TrainPathPointBase)typeof(PathEditorBase).GetProperty("ActivePathPoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                Assert.AreSame(segment, activePoint.ConnectedSegments.Single());
            }
        }

        [TestMethod]
        public void WhenLaterHoverIsUnresolvedThenLastValidPreviewRemainsCommittable()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.ContinuePathCommand();
                PathEditResult preview = InvokeAddRoutePoint(source, CreateNodeAt(200, PathNodeType.None, -1));
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);

                editor.UpdatePointerLocation(PointD.None, null);

                Assert.IsTrue(editor.CanCommitPlacement);
            }
        }

        [TestMethod]
        public void WhenInteractivePreviewIsCanceledThenStaleResultIsNotPublished()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.ContinuePathCommand();
                PathEditResult preview = InvokeAddRoutePoint(source, CreateNodeAt(200, PathNodeType.None, -1));
                PathNode previewAnchor = CreateNodeAt(200, PathNodeType.None, -1);
                SetPrivateField(editor, "resolvingPreviewAnchor", previewAnchor);
                SetPrivateField(editor, "interactivePreviewGeneration", 1);
                using (CancellationTokenSource cancellation = new CancellationTokenSource())
                {
                    SetPrivateField(editor, "interactivePreviewCancellation", cancellation);
                    cancellation.Cancel();

                    _ = editor.CancelPlacement();
                    PathSpanCommitResult resolved = PathSpanCommitResult.Resolved("Resolved stale preview.", preview.PathModel, preview.ChangedNodeIndexes);
                    typeof(PathEditor).GetMethod("CompleteInteractivePreview", BindingFlags.Instance | BindingFlags.NonPublic).
                        Invoke(editor, new object[] { 1, source, PathEditorPlacementMode.BuildRoute, previewAnchor, preview, Task.FromResult(resolved) });
                }


                Assert.IsFalse(editor.CanCommitPlacement);
            }
        }

        [TestMethod]
        public void WhenStoppedThenPointerCannotUpdateOrLaunchPreview()
        {
            using (PathEditor editor = CreateEditor(CreateEditablePath()))
            {
                _ = editor.ContinuePathCommand();
                object point = typeof(PathEditorBase).GetProperty("ActivePathPoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                Task preview = (Task)typeof(PathEditor).GetField("interactivePreviewTask", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);

                editor.Stop();
                editor.Stop();
                editor.UpdatePointerLocation(new PointD(50, 0), new TestTrackSegment(new PointD(0, 0), new PointD(100, 0)));

                Assert.AreEqual((point, preview, false),
                    (typeof(PathEditorBase).GetProperty("ActivePathPoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor),
                    typeof(PathEditor).GetField("interactivePreviewTask", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor), editor.IsPlacementActive));
            }
        }

        [TestMethod]
        public void WhenStoppedThenPlacementCannotRestart()
        {
            using (PathEditor editor = CreateEditor(CreateEditablePath()))
            {
                editor.Stop();

                Assert.AreEqual((false, false, false, false), (editor.BeginStartAnchorPlacementCommand().Success,
                    editor.BeginEndAnchorPlacementCommand().Success, editor.BeginMoveNode(0), editor.ContinuePathCommand().Success));
            }
        }

        [TestMethod]
        public void WhenStoppedThenContinueDoesNotRestorePresentation()
        {
            using (PathEditor editor = CreateEditor(CreateEditablePath()))
            {
                PathModel source = editor.TryCaptureCurrentPathModel();
                editor.Stop();

            PathEditorCommandResult result = editor.ContinuePathCommand();

                Assert.AreEqual((false, true, source, false, false),
                    (result.Success, editor.EditMode, editor.TryCaptureCurrentPathModel(), editor.CanContinuePath, editor.CanMoveNode(0)));
            }
        }

        [TestMethod]
        public void WhenStoppedThenPlacementCommitAndReleaseLeaveModelUnchanged()
        {
            using (PathEditor editor = CreateEditor(CreateEditablePath()))
            {
                _ = editor.ContinuePathCommand();
                PathModel source = editor.TryCaptureCurrentPathModel();
                SetPrivateField(editor, "movePreviewModel", InvokeAddRoutePoint(source, CreateNodeAt(200, PathNodeType.None, -1)).PathModel);

                editor.Stop();
                editor.MouseDragged(null, KeyModifiers.None);
                editor.MouseReleasedLeft(null, KeyModifiers.None);
                PathEditResult result = editor.CommitPlacement();

                Assert.AreEqual((false, source), (result.Success, editor.TryCaptureCurrentPathModel()));
            }
        }

        [TestMethod]
        public void WhenStoppedThenDirectAnchorPlacementLeavesModelUnchanged()
        {
            using (PathEditor editor = CreateEditor(CreateEditablePath()))
            {
                PathModel source = editor.TryCaptureCurrentPathModel();
                editor.Stop();

                PathEditorCommandResult result = editor.SetStartAnchorCommand(CreateNodeAt(50, PathNodeType.None, -1), false);

                Assert.AreEqual((false, source), (result.Success, editor.TryCaptureCurrentPathModel()));
            }
        }

        [TestMethod]
        public async Task WhenPreviewAdmissionIsRejectedThenWorkerIsNotLaunched()
        {
            InvalidOperationException rejection = new InvalidOperationException("Session stopped.");
            UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand> controller = new UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand>();
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(CreateInitializedTrackWorld()), controller,
                _ => Task.CompletedTask, commit => Task.FromResult(commit()), _ => Task.FromException(rejection)))
            {
                editor.InitializeNewPath();
                PathModel source = editor.TryCaptureCurrentPathModel();
                typeof(PathEditor).GetMethod("StartInteractivePreview", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor,
                    new object[] { CreateNodeAt(50, PathNodeType.None, -1), PathEditResult.Succeeded("Preview", source, ImmutableArray<int>.Empty) });
                Task preview = (Task)typeof(PathEditor).GetField("interactivePreviewTask", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);

                _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => preview).ConfigureAwait(false);

                Assert.IsNull(typeof(PathEditor).GetField("interactivePreviewCancellation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
            }
        }

        private sealed record TestTrackSegment : TrackSegmentBase
        {
            public TestTrackSegment(in PointD start, in PointD end)
                : base(start, end)
            {
            }
        }

        [TestMethod]
        public void WhenPlacementPreviewIsCommittedThroughTheKeyboardCommandThenItMaterializesOnce()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.ContinuePathCommand();
                PathEditResult preview = InvokeAddRoutePoint(source, CreateNodeAt(200, PathNodeType.None, -1));
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);

                PathEditorCommandResult result = editor.CommitPlacementCommand();

                Assert.IsTrue(result.Success);
                Assert.HasCount(3, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.IsTrue(editor.CanUndo);
            }
        }

        [TestMethod]
        public void WhenAffectedSpansAreSelectedThenOnlySpansBoundedByAnEditedNodeAreReturned()
        {
            PathRouteResolution resolution = CreateResolution(new ResolvedPathSpan(0, 1, PathRouteSpanStatus.Resolved), new ResolvedPathSpan(1, 2, PathRouteSpanStatus.Resolved), new ResolvedPathSpan(2, 3, PathRouteSpanStatus.Resolved));

            ImmutableArray<ResolvedPathSpan> affected = InvokeAffectedSpans(resolution, ImmutableArray.Create(2));

            Assert.HasCount(2, affected);
            Assert.IsTrue(affected.All(span => span.FromNodeIndex == 2 || span.ToNodeIndex == 2));
        }

        [TestMethod]
        public void WhenNoNodeIsEditedThenAllSpansAreAffected()
        {
            PathRouteResolution resolution = CreateResolution(new ResolvedPathSpan(0, 1, PathRouteSpanStatus.Resolved), new ResolvedPathSpan(1, 2, PathRouteSpanStatus.Resolved));

            ImmutableArray<ResolvedPathSpan> affected = InvokeAffectedSpans(resolution, ImmutableArray<int>.Empty);

            Assert.HasCount(2, affected);
        }

        [TestMethod]
        public void WhenAffectedSpanRetracesCommittedRouteThenImplicitRouteBackIsDetected()
        {
            // Both adjacent spans are reported as affected when a route point is appended, so the check must
            // compare each affected span against the edges of the spans preceding it, not against all spans.
            PathModel pathModel = new PathModel
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.Via, 2), CreateNode(PathNodeType.End, -1)),
            };
            ResolvedPathSpan outgoing = new ResolvedPathSpan(0, 1, PathRouteSpanStatus.Resolved, ImmutableArray.Create(9, 3, 7, 4, 5));
            ResolvedPathSpan returning = new ResolvedPathSpan(1, 2, PathRouteSpanStatus.Resolved, ImmutableArray.Create(5, 4, 7, 3, 9));
            ImmutableArray<ResolvedPathSpan> spans = ImmutableArray.Create(outgoing, returning);

            bool routeBack = PathEditor.HasImplicitRouteBack(pathModel, spans, spans);

            Assert.IsTrue(routeBack);
        }

        [TestMethod]
        public void WhenAffectedSpanStartsAtReversalThenRouteBackIsAllowed()
        {
            PathModel pathModel = new PathModel
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Start, 1), CreateNode(PathNodeType.Via | PathNodeType.Reversal, 2), CreateNode(PathNodeType.End, -1)),
            };
            ResolvedPathSpan committed = new ResolvedPathSpan(0, 1, PathRouteSpanStatus.Resolved, ImmutableArray.Create(9, 3, 7, 4, 5));
            ResolvedPathSpan affected = new ResolvedPathSpan(1, 2, PathRouteSpanStatus.Resolved, ImmutableArray.Create(5, 4, 7, 3, 9));

            bool routeBack = PathEditor.HasImplicitRouteBack(pathModel, ImmutableArray.Create(committed, affected), ImmutableArray.Create(affected));

            Assert.IsFalse(routeBack);
        }

        [TestMethod]
        public void WhenEndAnchorSpanResolvesThenAnchorIsCommitted()
        {
            using (PathEditor editor = CreateEditorWithStartAnchor())
            {
                PathEditorCommandResult result = editor.SetEndAnchorCommand(CreateNodeAt(100, PathNodeType.None, -1), false);

                Assert.IsTrue(result.Success, result.Message);
                Assert.IsTrue(HasNodeType(editor.TryCaptureCurrentPathModel(), PathNodeType.End));
            }
        }

        [TestMethod]
        public void WhenEndAnchorIsBeyondJunctionThenCommitMaterializesTheTargetSideExit()
        {
            using (PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateTargetSideTrackWorld()))
            {
                PathEditorCommandResult result = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);

                Assert.IsTrue(result.Success);
                Assert.IsTrue(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NodeIndex == 3));
            }
        }

        [TestMethod]
        public void WhenEndAnchorSpanIsAmbiguousThenCommittedPathAndHistoryRemainUnchanged()
        {
            PathModel source = CreateAmbiguousEndpointPath();
            using (PathEditor editor = CreateEditor(source, CreateAmbiguousTrackWorld()))
            {

                PathEditorCommandResult result = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);

                Assert.IsFalse(result.Success);
                Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit);
                Assert.IsFalse(editor.CanUndo);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenPendingAmbiguousCandidateIsPreviewedThenCommittedPathDoesNotChange()
        {
            using (PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld()))
            {
                _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);

                PathEditResult result = editor.PreviewPendingRouteCandidate(0, 1);

                Assert.IsTrue(result.Success);
                Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit);
                Assert.IsFalse(HasNodeType(editor.TryCaptureCurrentPathModel(), PathNodeType.End));
                Assert.IsFalse(editor.CanUndo);
            }
        }

        // Interaction-state load sequencing tests.
        [TestMethod]
        public void WhenPendingCandidateIsPreviewedThenChangeNotificationSeesItsIdentity()
        {
            using (PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld()))
            {
                _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);
                (int, int) notifiedIdentity = (-1, -1);
                editor.OnPathUpdated += (_, _) => notifiedIdentity = (editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex);

                _ = editor.PreviewPendingRouteCandidate(0, 1);

                Assert.AreEqual((0, 1), notifiedIdentity);
            }
        }

        [TestMethod]
        public void WhenPendingCandidatePreviewFailsThenPreviousIdentityIsRetained()
        {
            using (PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld()))
            {
                _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);
                _ = editor.PreviewPendingRouteCandidate(0, 1);

                _ = editor.PreviewPendingRouteCandidate(0, 99);

                Assert.AreEqual((0, 1), (editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
            }
        }

        [TestMethod]
        public void WhenPendingCandidateIsCanceledDirectlyThenNotificationSeesClearedIdentity()
        {
            using (PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld()))
            {
                _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);
                _ = editor.PreviewPendingRouteCandidate(0, 1);
                (bool, int, int) notifiedState = (true, 0, 1);
                editor.OnPathUpdated += (_, _) => notifiedState = (editor.HasPendingAmbiguousSpanCommit, editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex);

                editor.CancelPendingRouteCandidate();
                Assert.AreEqual((false, -1, -1), notifiedState);
            }
        }

        [TestMethod]
        public void WhenPendingCandidateIsAcceptedDirectlyThenIdentityIsCleared()
        {
            using (PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld()))
            {
                _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);
                _ = editor.PreviewPendingRouteCandidate(0, 1);

                _ = editor.AcceptPendingRouteCandidate(0, 1);

                Assert.AreEqual((false, -1, -1), (editor.HasPendingAmbiguousSpanCommit, editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
            }
        }

        [TestMethod]
        public void WhenNewPathResetsPendingCandidateThenInteractionIdentityIsEmpty()
        {
            using (PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld()))
            {
                _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);
                _ = editor.PreviewPendingRouteCandidate(0, 1);

                editor.InitializeNewPath();

                Assert.AreEqual((false, -1, -1), (editor.HasPendingAmbiguousSpanCommit, editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
            }
        }

        [TestMethod]
        public async Task WhenSecondPathLoadCompletesFirstThenFirstCannotReplaceIt()
        {
            RouteModel route = CreateLoadTestRoute();
            TaskCompletionSource<PathModelHeader> firstCompletion = CreatePathLoadCompletion();
            TaskCompletionSource<PathModelHeader> secondCompletion = CreatePathLoadCompletion();
            PathModelHeader firstHeader = CreatePendingPathHeader(route, "first-path", firstCompletion.Task);
            PathModelHeader secondHeader = CreatePendingPathHeader(route, "second-path", secondCompletion.Task);
            PathModel firstPath = CreateLoadedPath(route, firstHeader.Id);
            PathModel secondPath = CreateLoadedPath(route, secondHeader.Id);
            using (PathEditor editor = CreateNewEditor())
            {
                try
                {
                    Task<bool> firstLoad = editor.InitializePathAsync(firstHeader, CancellationToken.None);
                    Task<bool> secondLoad = editor.InitializePathAsync(secondHeader, CancellationToken.None);

                    secondCompletion.SetResult(secondPath);
                    _ = await secondLoad.ConfigureAwait(false);
                    firstCompletion.SetResult(firstPath);
                    _ = await firstLoad.ConfigureAwait(false);

                    Assert.AreSame(secondPath, editor.TryCaptureCurrentPathModel());
                }
                finally
                {
                    RemovePendingPath(route, firstHeader.Id);
                    RemovePendingPath(route, secondHeader.Id);
                }
            }
        }

        [TestMethod]
        public async Task WhenPathIsUnloadedDuringLoadThenLateCompletionCannotReopenIt()
        {
            RouteModel route = CreateLoadTestRoute();
            TaskCompletionSource<PathModelHeader> completion = CreatePathLoadCompletion();
            PathModelHeader header = CreatePendingPathHeader(route, "unloaded-path", completion.Task);
            PathModel loadedPath = CreateLoadedPath(route, header.Id);
            using (PathEditor editor = CreateNewEditor())
            {
                try
                {
                    Task<bool> load = editor.InitializePathAsync(header, CancellationToken.None);

                    _ = await editor.InitializePathAsync(null, CancellationToken.None).ConfigureAwait(false);
                    completion.SetResult(loadedPath);
                    _ = await load.ConfigureAwait(false);

                    Assert.IsNull(editor.TryCaptureCurrentPathModel());
                }
                finally
                {
                    RemovePendingPath(route, header.Id);
                }
            }
        }

        [TestMethod]
        public async Task WhenNewPathStartsDuringLoadThenLateCompletionCannotReplaceIt()
        {
            RouteModel route = CreateLoadTestRoute();
            TaskCompletionSource<PathModelHeader> completion = CreatePathLoadCompletion();
            PathModelHeader header = CreatePendingPathHeader(route, "superseded-path", completion.Task);
            PathModel loadedPath = CreateLoadedPath(route, header.Id);
            using (PathEditor editor = CreateNewEditor())
            {
                try
                {
                    Task<bool> load = editor.InitializePathAsync(header, CancellationToken.None);

                    editor.InitializeNewPath();
                    completion.SetResult(loadedPath);
                    _ = await load.ConfigureAwait(false);

                    Assert.IsTrue(editor.IsNewPath);
                }
                finally
                {
                    RemovePendingPath(route, header.Id);
                }
            }
        }

        [TestMethod]
        public async Task WhenNewPathStartsAfterLoadCommitIsQueuedThenStaleCommitCannotReplaceIt()
        {
            TaskCompletionSource<Func<bool>> queuedCommitSource = new TaskCompletionSource<Func<bool>>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> commitCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand> commandController = new UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand>();

            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()), commandController,
                action => action(), commit =>
                {
                    queuedCommitSource.TrySetResult(commit);
                    return commitCompletion.Task;
                }))
            {
                PathModel loadedPath = CreateEditablePath() with { Id = "queued-load-path" };
                Task<bool> load = editor.InitializePathAsync(loadedPath, CancellationToken.None);
                Func<bool> queuedCommit = await queuedCommitSource.Task.ConfigureAwait(false);

                editor.InitializeNewPath();
                commitCompletion.SetResult(queuedCommit());
                _ = await load.ConfigureAwait(false);

                Assert.IsTrue(editor.IsNewPath);
            }
        }

        [TestMethod]
        public async Task WhenEditorIsDisposedAfterLoadCommitIsQueuedThenCommitIsRejected()
        {
            TaskCompletionSource<Func<bool>> queuedCommitSource = new TaskCompletionSource<Func<bool>>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> commitCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand> commandController = new UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand>();
            PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()), commandController,
                action => action(), commit =>
                {
                    queuedCommitSource.TrySetResult(commit);
                    return commitCompletion.Task;
                });
            Task<bool> load = editor.InitializePathAsync(CreateEditablePath() with { Id = "disposed-load-path" }, CancellationToken.None);
            Func<bool> queuedCommit = await queuedCommitSource.Task.ConfigureAwait(false);

            editor.Dispose();
            commitCompletion.SetResult(queuedCommit());
            bool loaded = await load.ConfigureAwait(false);

            Assert.IsFalse(loaded);
        }

        [TestMethod]
        public void WhenPendingAmbiguousCandidateExistsThenMetadataEditIsRejected()
        {
            using (PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld()))
            {
                _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);
                PathModel committedModel = editor.TryCaptureCurrentPathModel();

                PathEditorCommandResult result = editor.SetMetadataCommand("Changed", "Changed Start", "Changed End", false);

                Assert.IsFalse(result.Success);
                Assert.AreSame(committedModel, editor.TryCaptureCurrentPathModel());
                Assert.Contains("Accept or cancel", result.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        [TestMethod]
        public async Task WhenPendingAmbiguousCandidateSourceIsStaleThenAcceptancePreservesCurrentPath()
        {
            using (PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld()))
            {
                _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);
                PathModel replacement = CreateEditablePath() with { Id = "replacement-path" };
                Assert.IsTrue(await editor.InitializePathAsync(replacement, CancellationToken.None).ConfigureAwait(false));

                PathEditResult result = editor.AcceptPendingRouteCandidate(0, 0);

                Assert.IsFalse(result.Success);
                Assert.AreSame(replacement, editor.TryCaptureCurrentPathModel());
                Assert.IsFalse(editor.HasPendingAmbiguousSpanCommit);
            }
        }

        [TestMethod]
        public void WhenAlternatePointerIsReleasedThenPathEditorLeavesInputUnhandled()
        {
            UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand> commandController = new UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand>();
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld()), commandController,
                action => action(), commit => Task.FromResult(commit())))
            {
                editor.InitializeNewPath();
                UserCommandArgs commandArgs = new UserCommandArgs();

                MethodInfo trigger = typeof(UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand>)
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(method => method.Name == "Trigger"
                        && method.GetParameters()[0].ParameterType == typeof(CommonUserCommand));
                trigger.Invoke(commandController, new object[] { CommonUserCommand.AlternatePointerReleased, commandArgs, new GameTime(), KeyModifiers.None });

                Assert.IsFalse(commandArgs.Handled);
                Assert.IsFalse(editor.CanUndo);
            }
        }

        [TestMethod]
        public void WhenSpanGenerationFailsThenCommittedStateAndHistoryRemainUnchanged()
        {
            PathModel source = CreateNonRejoiningPassingPath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.SetWaitPointCommand(1, 10);
                _ = editor.Undo();
                SetPrivateField(editor, "unsavedChanges", false);
                PathModel committedModel = editor.TryCaptureCurrentPathModel();
                bool canUndo = editor.CanUndo;
                bool canRedo = editor.CanRedo;

                PathEditorCommandResult result = editor.SetEndAnchorCommand(CreateNodeAt(90, PathNodeType.None, -1), false);

                Assert.IsFalse(result.Success);
                Assert.AreSame(committedModel, editor.TryCaptureCurrentPathModel());
                Assert.IsFalse(editor.HasUnsavedChanges);
                Assert.AreEqual(canUndo, editor.CanUndo);
                Assert.AreEqual(canRedo, editor.CanRedo);
            }
        }

        [TestMethod]
        public void WhenPassingBranchRejoinIsSelectedThenGenericCancellationIsAvailableAndSafe()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                bool dirty = editor.HasUnsavedChanges;
                bool canUndo = editor.CanUndo;
                bool canRedo = editor.CanRedo;

                Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);
                bool canCancel = editor.CanCancelPathInteraction;
                PathEditorCommandResult cancel = editor.CancelPathInteractionCommand();

                Assert.IsTrue(canCancel);
                Assert.IsTrue(cancel.Success);
                Assert.AreEqual(PassingBranchAuthoringPhase.Idle, editor.PassingBranchPhase);
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual(dirty, editor.HasUnsavedChanges);
                Assert.AreEqual(canUndo, editor.CanUndo);
                Assert.AreEqual(canRedo, editor.CanRedo);
            }
        }

        [TestMethod]
        public async Task WhenPathIsLoadedDuringPassingBranchRejoinThenSelectionCannotModifyReplacement()
        {
            PathModel replacement = CreateEditablePath() with { Id = "replacement-path" };
            using (PathEditor editor = CreateEditor(CreateEditablePath()))
            {
                Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);

                Assert.IsTrue(await editor.InitializePathAsync(replacement, CancellationToken.None).ConfigureAwait(false));
                PathEditorCommandResult result = editor.CompletePassingBranchCommand(1);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(PassingBranchAuthoringPhase.Idle, editor.PassingBranchPhase);
                Assert.AreSame(replacement, editor.TryCaptureCurrentPathModel());
                Assert.IsFalse(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NextSidingNode >= 0));
                Assert.IsFalse(editor.HasUnsavedChanges);
                Assert.IsFalse(editor.CanUndo);
            }
        }

        [TestMethod]
        public void WhenNewPathIsCreatedDuringPassingBranchRejoinThenSelectionIsCleared()
        {
            using (PathEditor editor = CreateEditor(CreateEditablePath()))
            {
                Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);

                editor.InitializeNewPath();
                PathEditorCommandResult result = editor.CompletePassingBranchCommand(1);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(PassingBranchAuthoringPhase.Idle, editor.PassingBranchPhase);
                Assert.IsTrue(editor.IsNewPath);
                Assert.IsFalse(editor.CanUndo);
            }
        }

        [TestMethod]
        public async Task WhenPathIsUnloadedDuringPassingBranchRejoinThenSelectionIsCleared()
        {
            using (PathEditor editor = CreateEditor(CreateEditablePath()))
            {
                Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);

                Assert.IsTrue(await editor.InitializePathAsync(null, CancellationToken.None).ConfigureAwait(false));
                PathEditorCommandResult result = editor.CompletePassingBranchCommand(1);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(PassingBranchAuthoringPhase.Idle, editor.PassingBranchPhase);
                Assert.IsNull(editor.TryCaptureCurrentPathModel());
                Assert.IsFalse(editor.HasUnsavedChanges);
                Assert.IsFalse(editor.CanUndo);
            }
        }

        [TestMethod]
        public void WhenPassingBranchSourceChangesThenCompletionIsRejected()
        {
            PathModel replacement = CreateEditablePath() with { Id = "replacement-snapshot" };
            using (PathEditor editor = CreateEditor(CreateEditablePath()))
            {
                Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);
                typeof(PathEditor).GetMethod("RestoreSnapshot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { replacement });
                PathModel restoredReplacement = editor.TryCaptureCurrentPathModel();

                PathEditorCommandResult result = editor.CompletePassingBranchCommand(1);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(PassingBranchAuthoringPhase.Idle, editor.PassingBranchPhase);
                Assert.AreSame(restoredReplacement, editor.TryCaptureCurrentPathModel());
                Assert.IsFalse(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NextSidingNode >= 0));
            }
        }

        [TestMethod]
        public void WhenMainSpanIsUnresolvedThenPassingCompletionReportsItsNodeIndexes()
        {
            PathNode unresolvedStart = CreateUnroutableNode() with { NodeType = PathNodeType.Start, NextMainNode = 1 };
            PathModel source = new PathModel
            {
                Id = "unresolved-main-path",
                PathNodes = ImmutableArray.Create(
                    unresolvedStart,
                    CreateNodeAt(0, PathNodeType.Via, 2),
                    CreateNodeAt(100, PathNodeType.End, -1)),
            };
            using (PathEditor editor = CreateEditor(source))
            {
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                Assert.IsTrue(editor.BeginPassingBranchCommand(1).Success);

                PathEditorCommandResult result = editor.CompletePassingBranchCommand(2);

                Assert.IsFalse(result.Success);
                Assert.Contains("span from node 0 to node 1 is unresolved", result.Message, StringComparison.OrdinalIgnoreCase);
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.IsFalse(editor.CanUndo);
            }
        }

        [TestMethod]
        public void WhenResolvedPassingBranchIsCreatedThenUndoAndRedoRestoreItsLifecycle()
        {
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSidingTrackWorld();
            PathModel source = CreateSidingEndpointPath(trackWorld);
            using (PathEditor editor = CreateEditor(source, trackWorld))
            {
                PathEditorCommandResult begin = editor.BeginPassingBranchCommand(0);
                PathEditorCommandResult complete = editor.CompletePassingBranchCommand(1);

                Assert.IsTrue(begin.Success);
                Assert.IsTrue(complete.Success, complete.Message);
                Assert.IsTrue(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NextSidingNode >= 0));
                Assert.IsTrue(editor.Undo());
                Assert.IsFalse(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NextSidingNode >= 0));
                Assert.IsTrue(editor.Redo());
                Assert.IsTrue(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NextSidingNode >= 0));
            }
        }

        [TestMethod]
        public void WhenPathIsTruncatedBeforePassingBranchRejoinThenBranchAndTailAreRemovedAsOneEdit()
        {
            PathModel source = CreateTruncationPassingBranchPath();
            using (PathEditor editor = CreateEditor(source))
            {
                PathEditResult result = editor.RemoveRestOfPath(2);

                Assert.IsTrue(result.Success, result.Message);
                Assert.IsFalse(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NextSidingNode >= 0));
                Assert.IsTrue(editor.HasUnsavedChanges);
                Assert.IsTrue(editor.Undo());
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.IsTrue(editor.Redo());
                Assert.IsFalse(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NextSidingNode >= 0));
            }
        }

        [TestMethod]
        public void WhenBranchPreservingTruncationFailsPersistenceValidationThenEditorStateRemainsUnchanged()
        {
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSidingTrackWorld();
            PathModel source = CreateSidingTruncationPassingBranchPath(trackWorld);
            using (PathEditor editor = CreateEditor(source, trackWorld))
            {
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                PathEditResult result = editor.RemoveRestOfPath(2);

                Assert.IsFalse(result.Success);
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.IsFalse(editor.CanUndo);
            }
        }

        [TestMethod]
        public void WhenPathIsTruncatedAtPassingBranchInteriorThenEditorStateRemainsUnchanged()
        {
            PathModel source = CreateTruncationPassingBranchPath();
            using (PathEditor editor = CreateEditor(source))
            {
                editor.SelectAuthoredNode(6);
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                bool dirty = editor.HasUnsavedChanges;
                bool canUndo = editor.CanUndo;
                bool canRedo = editor.CanRedo;

                PathEditResult result = editor.RemoveRestOfPath(6);

                Assert.IsFalse(result.Success);
                Assert.IsFalse(editor.CanRemoveRestOfPath(6));
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual(6, editor.SelectedAuthoredNodeIndex);
                Assert.AreEqual(dirty, editor.HasUnsavedChanges);
                Assert.AreEqual(canUndo, editor.CanUndo);
                Assert.AreEqual(canRedo, editor.CanRedo);
            }
        }

        [TestMethod]
        public void WhenRouteCandidateIsPendingThenPathTruncationIsUnavailableAndRejected()
        {
            PathModel source = CreateTruncationPassingBranchPath();
            using (PathEditor editor = CreateEditor(source))
            {
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                SetPrivateField(editor, "pendingPassingBranchCandidate", new PendingPassingBranchCandidate(baseline, 1, 3,
                    new ResolvedPathSpan(1, 3, PathRouteSpanStatus.Ambiguous)));

                bool canRemove = editor.CanRemoveRestOfPath(2);
                PathEditResult result = editor.RemoveRestOfPath(2);

                Assert.IsFalse(canRemove);
                Assert.IsFalse(result.Success);
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.IsTrue(editor.HasPendingPassingBranchCandidate);
            }
        }

        [TestMethod]
        public void WhenTruncationRemapsCutoffThenEditorSelectsTheNewEnd()
        {
            PathModel source = CreateNoncanonicalLinearPath();
            using (PathEditor editor = CreateEditor(source))
            {
                PathEditResult result = editor.RemoveRestOfPath(2);

                Assert.IsTrue(result.Success, result.Message);
                int selectedNodeIndex = editor.SelectedAuthoredNodeIndex;
                Assert.IsGreaterThanOrEqualTo(0, selectedNodeIndex);
                Assert.IsTrue(editor.TryCaptureCurrentPathModel().PathNodes[selectedNodeIndex].NodeType.Includes(PathNodeType.End));
                Assert.IsTrue(editor.Undo());
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenBranchedPathMainNodeIsMovedThenNormalMoveRemainsAvailable()
        {
            PathModel source = CreateSupportedPassingBranchPath();
            PathNode anchor = CreateNodeAt(50, PathNodeType.None, -1);

            PathEditResult result = InvokeMoveSelectedAuthoredNode(source, 1, anchor);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(anchor.Location, result.PathModel.PathNodes[1].Location);
        }

        [TestMethod]
        public void WhenPassingBranchInteriorIsMovedThenBranchSpecificMoveIsUsed()
        {
            PathModel source = CreateSupportedPassingBranchPath();
            PathNode anchor = CreateNodeAt(75, PathNodeType.None, -1);

            PathEditResult result = InvokeMoveSelectedAuthoredNode(source, 3, anchor);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(anchor.Location, result.PathModel.PathNodes[3].Location);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(2)]
        public void WhenPassingBranchEndpointIsMovedThenMoveIsRejected(int nodeIndex)
        {
            PathModel source = CreateSupportedPassingBranchPath();

            PathEditResult result = InvokeMoveSelectedAuthoredNode(source, nodeIndex, CreateNodeAt(25, PathNodeType.None, -1));

            Assert.IsFalse(result.Success);
            Assert.AreSame(source, result.PathModel);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(2)]
        public void WhenPassingBranchEndpointIsRemovedAsViaThenEditorStateRemainsUnchanged(int nodeIndex)
        {
            PathModel source = CreateSupportedPassingBranchPath();
            using (PathEditor editor = CreateEditor(source))
            {
                editor.SelectAuthoredNode(nodeIndex);
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                bool dirty = editor.HasUnsavedChanges;
                bool canUndo = editor.CanUndo;
                bool canRedo = editor.CanRedo;

                PathEditResult result = editor.RemoveViaPoint(nodeIndex);

                Assert.IsFalse(result.Success);
                Assert.IsFalse(editor.CanRemoveViaPoint(nodeIndex));
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual(nodeIndex, editor.SelectedAuthoredNodeIndex);
                Assert.AreEqual(dirty, editor.HasUnsavedChanges);
                Assert.AreEqual(canUndo, editor.CanUndo);
                Assert.AreEqual(canRedo, editor.CanRedo);
            }
        }

        [TestMethod]
        public void WhenPassingBranchInteriorRemovalEliminatesPhysicalDistinctionThenEditorStateRemainsUnchanged()
        {
            PathModel source = CreateSupportedPassingBranchPath();
            using (PathEditor editor = CreateEditor(source))
            {
                editor.SelectAuthoredNode(3);
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                bool dirty = editor.HasUnsavedChanges;
                bool canUndo = editor.CanUndo;
                bool canRedo = editor.CanRedo;

                PathEditResult result = editor.RemoveViaPoint(3);

                Assert.IsFalse(result.Success);
                Assert.IsFalse(editor.CanRemoveViaPoint(3));
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual(3, editor.SelectedAuthoredNodeIndex);
                Assert.AreEqual(dirty, editor.HasUnsavedChanges);
                Assert.AreEqual(canUndo, editor.CanUndo);
                Assert.AreEqual(canRedo, editor.CanRedo);
            }
        }

        [TestMethod]
        public void WhenPassingBranchInteriorRemovalFailsSaveVerificationThenEditorStateRemainsUnchanged()
        {
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSidingTrackWorld();
            PathModel source = CreateResolvablePassingBranchPath(trackWorld);
            using (PathEditor editor = CreateEditor(source, trackWorld))
            {
                editor.SelectAuthoredNode(3);
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                bool dirty = editor.HasUnsavedChanges;
                bool canUndo = editor.CanUndo;
                bool canRedo = editor.CanRedo;

                PathEditResult result = editor.RemoveViaPoint(3);

                Assert.IsFalse(result.Success);
                Assert.IsFalse(editor.CanRemoveViaPoint(3));
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual(3, editor.SelectedAuthoredNodeIndex);
                Assert.AreEqual(dirty, editor.HasUnsavedChanges);
                Assert.AreEqual(canUndo, editor.CanUndo);
                Assert.AreEqual(canRedo, editor.CanRedo);
            }
        }

        [TestMethod]
        public void WhenPendingPassingCandidateIsCanceledGenericallyThenPreviewAndHistoryStayUnchanged()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                ResolvedPathSpan span = new ResolvedPathSpan(0, 1, PathRouteSpanStatus.Ambiguous);
                SetPrivateField(editor, "pendingPassingBranchCandidate", new PendingPassingBranchCandidate(baseline, 0, 1, span));
                bool dirty = editor.HasUnsavedChanges;
                bool canUndo = editor.CanUndo;
                bool canRedo = editor.CanRedo;

                PathEditorCommandResult cancel = editor.CancelPathInteractionCommand();

                Assert.IsTrue(cancel.Success);
                Assert.AreEqual(PassingBranchAuthoringPhase.Idle, editor.PassingBranchPhase);
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual(dirty, editor.HasUnsavedChanges);
                Assert.AreEqual(canUndo, editor.CanUndo);
                Assert.AreEqual(canRedo, editor.CanRedo);
            }
        }

        [TestMethod]
        public void WhenPassingCandidateSourceChangesThenPreviewClearsStaleCandidate()
        {
            PathModel source = CreateEditablePath();
            PathModel replacement = source with { Id = "replacement-preview-path" };
            using (PathEditor editor = CreateEditor(source))
            {
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                SetPrivateField(editor, "pendingPassingBranchCandidate", new PendingPassingBranchCandidate(baseline, 0, 1,
                    new ResolvedPathSpan(0, 1, PathRouteSpanStatus.Ambiguous)));
                typeof(PathEditor).GetMethod("RestoreSnapshot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { replacement });
                PathModel restoredReplacement = editor.TryCaptureCurrentPathModel();

                PathEditResult result = editor.PreviewRouteCandidate(0, 0);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(PassingBranchAuthoringPhase.Idle, editor.PassingBranchPhase);
                Assert.AreSame(restoredReplacement, editor.TryCaptureCurrentPathModel());
                Assert.IsFalse(editor.CanUndo);
            }
        }

        [TestMethod]
        public void WhenPassingCandidateSourceChangesThenAcceptanceClearsStaleCandidate()
        {
            PathModel source = CreateEditablePath();
            PathModel replacement = source with { Id = "replacement-acceptance-path" };
            using (PathEditor editor = CreateEditor(source))
            {
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                SetPrivateField(editor, "pendingPassingBranchCandidate", new PendingPassingBranchCandidate(baseline, 0, 1,
                    new ResolvedPathSpan(0, 1, PathRouteSpanStatus.Ambiguous)));
                typeof(PathEditor).GetMethod("RestoreSnapshot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { replacement });
                PathModel restoredReplacement = editor.TryCaptureCurrentPathModel();

                PathEditResult result = editor.AcceptRouteCandidate(0, 0);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(PassingBranchAuthoringPhase.Idle, editor.PassingBranchPhase);
                Assert.AreSame(restoredReplacement, editor.TryCaptureCurrentPathModel());
                Assert.IsFalse(editor.CanUndo);
            }
        }

        [TestMethod]
        public void WhenPassingBranchIsRemovedThenUndoRestoresIt()
        {
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSidingTrackWorld();
            PathModel source = CreateSidingEndpointPath(trackWorld);
            using (PathEditor editor = CreateEditor(source, trackWorld))
            {
                Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);
                Assert.IsTrue(editor.CompletePassingBranchCommand(1).Success);

                PathEditorCommandResult remove = editor.RemovePassingBranchCommand(0);

                Assert.IsTrue(remove.Success, remove.Message);
                Assert.IsFalse(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NextSidingNode >= 0));
                Assert.IsTrue(editor.Undo());
                Assert.IsTrue(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NextSidingNode >= 0));
            }
        }

        [TestMethod]
        public void WhenRouteCandidatesAreCycledThenSpaceEquivalentAcceptsThePreviewedCandidate()
        {
            using (PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld()))
            {
                _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);

                PathEditorCommandResult cycleResult = editor.CycleRouteCandidateCommand(1);
                PathEditorCommandResult acceptResult = editor.AcceptPreviewedRouteCandidateCommand();

                Assert.IsTrue(cycleResult.Success);
                Assert.IsTrue(acceptResult.Success);
                Assert.IsTrue(HasNodeType(editor.TryCaptureCurrentPathModel(), PathNodeType.End));
                Assert.IsTrue(editor.CanUndo);
            }
        }

        [TestMethod]
        public void WhenPendingRouteCandidateSelectionIsCanceledThenEscapeEquivalentLeavesSourceUnchanged()
        {
            PathModel source = CreateAmbiguousEndpointPath();
            using (PathEditor editor = CreateEditor(source, CreateAmbiguousTrackWorld()))
            {
                _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);

                PathEditorCommandResult result = editor.CancelPathInteractionCommand();

                Assert.IsTrue(result.Success);
                Assert.IsFalse(editor.HasPendingAmbiguousSpanCommit);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenPendingAmbiguousCandidateIsAcceptedThenOneUndoRestoresTheSource()
        {
            using (PathEditor editor = CreateEditor(CreateAmbiguousEndpointPath(), CreateAmbiguousTrackWorld()))
            {
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
        }

        [TestMethod]
        public void WhenPendingAmbiguousCandidateIsCanceledThenSourceAndHistoryRemainUnchanged()
        {
            PathModel source = CreateAmbiguousEndpointPath();
            using (PathEditor editor = CreateEditor(source, CreateAmbiguousTrackWorld()))
            {
                _ = editor.SetEndAnchorCommand(CreateNodeAt(200, PathNodeType.None, -1) with { NodeIndex = 2 }, false);

                editor.CancelPendingRouteCandidate();

                Assert.IsFalse(editor.HasPendingAmbiguousSpanCommit);
                Assert.IsFalse(editor.CanUndo);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenEndAnchorSpanResolvesThenOneUndoRestoresTheStartOnlyPath()
        {
            using (PathEditor editor = CreateEditorWithStartAnchor())
            {
                _ = editor.SetEndAnchorCommand(CreateNodeAt(100, PathNodeType.None, -1), false);

                bool undone = editor.Undo();

                Assert.IsTrue(undone);
                Assert.IsFalse(HasNodeType(editor.TryCaptureCurrentPathModel(), PathNodeType.End));
                Assert.IsFalse(editor.CanUndo);
            }
        }

        [TestMethod]
        public void WhenEndAnchorSpanCannotBeRoutedThenAnchorIsNotCommitted()
        {
            using (PathEditor editor = CreateEditorWithStartAnchor())
            {
                PathModel beforeCommit = editor.TryCaptureCurrentPathModel();

                PathEditorCommandResult result = editor.SetEndAnchorCommand(CreateUnroutableNode(), false);

                Assert.IsFalse(result.Success);
                Assert.AreSequenceEqual(beforeCommit.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenEndAnchorSpanCannotBeRoutedThenNoUndoSnapshotIsRecorded()
        {
            using (PathEditor editor = CreateEditorWithStartAnchor())
            {
                _ = editor.SetEndAnchorCommand(CreateUnroutableNode(), false);

                Assert.IsFalse(editor.CanUndo);
            }
        }

        [TestMethod]
        public void WhenPointerMovesOverEmptyNewPathThenLegacyEndpointMutationDoesNotRun()
        {
            using (PathEditor editor = CreateNewEditor())
            {
                editor.UpdatePointerLocation(new PointD(25, 0), null);

                Assert.IsEmpty(editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.IsFalse(editor.IsPlacementActive);
            }
        }

        [TestMethod]
        public void WhenInitialStartAnchorPlacementIsCommittedThenBuildRouteBegins()
        {
            using (PathEditor editor = CreateNewEditor())
            {
                _ = editor.BeginStartAnchorPlacementCommand();
                PathModel source = editor.TryCaptureCurrentPathModel();
                PathEditResult preview = PathModelEditor.SetStartAnchor(source, CreateNodeAt(25, PathNodeType.None, -1), false);
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);

                _ = editor.CommitPlacement();

                Assert.IsTrue(editor.IsBuildingRoute);
            }
        }

        [TestMethod]
        public void WhenRouteBuildingAfterInitialStartIsCanceledThenStartAnchorIsRetained()
        {
            using (PathEditor editor = CreateNewEditor())
            {
                _ = editor.BeginStartAnchorPlacementCommand();
                PathModel source = editor.TryCaptureCurrentPathModel();
                PathEditResult preview = PathModelEditor.SetStartAnchor(source, CreateNodeAt(25, PathNodeType.None, -1), false);
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);
                _ = editor.CommitPlacement();

                _ = editor.CancelPlacement();

                Assert.IsTrue(editor.TryCaptureCurrentPathModel().PathNodes[0].NodeType.Includes(PathNodeType.Start));
            }
        }

        [TestMethod]
        public void WhenInitialStartAnchorIsCommittedThenOneUndoRestoresEmptyPath()
        {
            using (PathEditor editor = CreateNewEditor())
            {
                _ = editor.BeginStartAnchorPlacementCommand();
                PathModel source = editor.TryCaptureCurrentPathModel();
                PathEditResult preview = PathModelEditor.SetStartAnchor(source, CreateNodeAt(25, PathNodeType.None, -1), false);
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);
                _ = editor.CommitPlacement();

                _ = editor.Undo();

                Assert.IsEmpty(editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenStartAnchorPlacementBeginsThenCommittedPathIsUnchanged()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                PathEditorCommandResult result = editor.BeginStartAnchorPlacementCommand();

                Assert.IsTrue(result.Success);
                Assert.IsTrue(editor.IsPlacingStartAnchor);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenStartAnchorPlacementIsCanceledThenDirtyStateAndHistoryAreUnchanged()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                SetPrivateField(editor, "unsavedChanges", false);
                _ = editor.BeginStartAnchorPlacementCommand();

                bool canceled = editor.CancelPlacement();

                Assert.IsTrue(canceled);
                Assert.IsFalse(editor.HasUnsavedChanges);
                Assert.IsFalse(editor.CanUndo);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenStartAnchorPlacementIsCommittedThenOneUndoRestoresSource()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
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
        }

        [TestMethod]
        public void WhenUndoneStartAnchorPlacementIsRedoneThenPlacedAnchorReturns()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.BeginStartAnchorPlacementCommand();
                PathEditResult preview = PathModelEditor.SetStartAnchor(source, CreateNodeAt(25, PathNodeType.None, -1), false);
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);
                _ = editor.CommitPlacement();
                _ = editor.Undo();

                bool redone = editor.Redo();

                Assert.IsTrue(redone);
                Assert.AreEqual(preview.PathModel.PathNodes[0].Location, editor.TryCaptureCurrentPathModel().PathNodes[0].Location);
            }
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
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.BeginEndAnchorPlacementCommand();
                PathEditResult preview = PathModelEditor.SetEndAnchor(source, CreateNodeAt(100, PathNodeType.None, -1), false);
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);

                PathEditResult committed = editor.CommitPlacement();

                Assert.AreSame(committed.PathModel, editor.TryCaptureCurrentPathModel());
                Assert.IsTrue(editor.HasUnsavedChanges);
            }
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
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.BeginEndAnchorPlacementCommand();
                PathEditResult preview = PathModelEditor.SetEndAnchor(source, CreateNodeAt(100, PathNodeType.None, -1), false);
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);
                _ = editor.CommitPlacement();

                PathRouteResolution resolution = editor.ResolveCurrent(editor.TryCaptureCurrentPathModel());

                Assert.IsFalse(HasDiagnostic(resolution, PathRouteDiagnosticCode.MissingEndNode));
            }
        }

        [TestMethod]
        public void WhenStartAnchorIsSetDirectlyThenItCommitsWithoutPlacementMode()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                PathEditorCommandResult result = editor.SetStartAnchorCommand(CreateNodeAt(25, PathNodeType.None, -1), false);

                Assert.IsTrue(result.Success);
                Assert.IsFalse(editor.IsPlacementActive);
                Assert.AreEqual(25f, editor.TryCaptureCurrentPathModel().PathNodes[0].Location.Location.X);
                Assert.IsTrue(editor.CanPlaceEndAnchor);
            }
        }

        [TestMethod]
        public void WhenPlacementReleaseFollowsADragThenAnchorIsNotCommitted()
        {
            // Panning the map during placement must not drop the anchor at the drag-release location.
            using (PathEditor editor = CreateNewEditor())
            {
                _ = editor.BeginStartAnchorPlacementCommand();
                PathModel source = editor.TryCaptureCurrentPathModel();
                PathEditResult preview = PathModelEditor.SetStartAnchor(source, CreateNodeAt(25, PathNodeType.None, -1), false);
                SetPrivateField(editor, "movePreviewModel", preview.PathModel);

                editor.MouseDragged(new UserCommandArgs(), KeyModifiers.None);
                editor.MouseReleasedLeft(new UserCommandArgs(), KeyModifiers.None);

                Assert.IsTrue(editor.IsPlacingStartAnchor);
            }
        }

        [TestMethod]
        public void WhenAnchoredViaPlacementBeginsThenInsertedNodeUsesProvidedAnchor()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                PathNode anchor = CreateNodeAt(50, PathNodeType.Via, -1);

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
                _ = editor.BeginViaPointPlacementAt(0, CreateNodeAt(50, PathNodeType.Via, -1));

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
                _ = editor.BeginViaPointPlacementAt(0, CreateNodeAt(50, PathNodeType.Via, -1));
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
            Assert.HasCount(3, editor.TryCaptureCurrentPathModel().PathNodes);
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
                    CreateNodeAt(200, PathNodeType.End, -1) with { NodeIndex = 2 }),
            };
            using (PathEditor editor = CreateEditor(source, CreateAmbiguousTrackWorld()))
            {
                PathEditorCommandResult result = editor.AddViaPointHereCommand(0, CreateNodeAt(125, PathNodeType.None, -1) with { NodeIndex = 1 }, false);

                Assert.IsFalse(result.Success);
                Assert.Contains("equal-cost", result.Message);
                Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit);
                Assert.IsFalse(editor.CanUndo);
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
            }
        }

        [TestMethod]
        public void WhenPathHasEndButNoStartThenTrainPathConstructionSucceeds()
        {
            PathModel partialPath = new PathModel()
            {
                PathNodes = ImmutableArray.Create(CreateNode(PathNodeType.Via, 1), CreateNode(PathNodeType.End, -1)),
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
        public void WhenLayoutIsUnchangedThenReResolveIsNearIdempotent()
        {
            // Re-resolve is a whole-path re-snap ("fix broken path"). On an unchanged layout it must not churn
            // the node graph: snapping an already-snapped path has to produce the same nodes again.
            TrackWorld trackWorld = TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
            PathModel source = CreateEditablePath();

            PathEditResult first = PathEditor.SnapPathToTrack(source, trackWorld);
            PathEditResult second = PathEditor.SnapPathToTrack(first.PathModel, trackWorld);

            Assert.IsTrue(first.Success);
            Assert.IsTrue(second.Success);
            Assert.AreSequenceEqual(first.PathModel.PathNodes, second.PathModel.PathNodes);
        }

        [TestMethod]
        public async Task WhenSaveCompletesAfterANewerEditThenTheNewerModelRemainsDirty()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                PathPersistenceValidationResult saved = new PathPersistenceValidationResult(true, source with { Name = "Saved Path" }, null, default, default, null, null);
                TaskCompletionSource<PathPersistenceValidationResult> persistence = new TaskCompletionSource<PathPersistenceValidationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                PathSaveOperation operation = CreatePendingSaveOperation(editor, source, persistence.Task);

                PathEditorCommandResult edit = editor.SetStartAnchorCommand(CreateNodeAt(25, PathNodeType.None, -1), false);
                PathModel newerModel = editor.TryCaptureCurrentPathModel();
                string activePathId = editor.PathId;
                bool canUndo = editor.CanUndo;
                int pathChangedEvents = 0;
                editor.OnPathChanged += (_, _) => pathChangedEvents++;
                persistence.SetResult(saved);
                _ = await PathSaveOperationConsumer.ConsumeAsync(editor, operation, action => action()).ConfigureAwait(false);

                Assert.IsTrue(edit.Success);
                Assert.AreSame(newerModel, editor.TryCaptureCurrentPathModel());
                Assert.IsTrue(editor.HasUnsavedChanges);
                Assert.AreEqual(canUndo, editor.CanUndo);
                Assert.AreEqual(activePathId, editor.PathId);
                Assert.AreEqual(0, pathChangedEvents);
                Assert.IsFalse(editor.IsSaveInProgress);
            }
        }

        [TestMethod]
        public async Task WhenSaveAsCompletesThenEditorSwitchesToTheNewPersistedIdentity()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.SetStartAnchorCommand(CreateNodeAt(25, PathNodeType.None, -1), false);
                PathModel capturedSource = editor.TryCaptureCurrentPathModel();
                PathModel savedCopy = capturedSource with { Id = "saved-copy", Name = "Saved Copy" };
                PathSaveOperation operation = CreatePendingSaveOperation(editor, capturedSource, Task.FromResult(new PathPersistenceValidationResult(true, savedCopy, null, default, default, null, null)));

                _ = await PathSaveOperationConsumer.ConsumeAsync(editor, operation, action => action()).ConfigureAwait(false);

                Assert.AreEqual("saved-copy", editor.PathId);
                Assert.AreEqual("saved-copy", editor.TrainPath.PathModel.Id);
                Assert.AreEqual("Saved Copy", editor.TryCaptureCurrentPathModel().Name);
                Assert.IsFalse(editor.HasUnsavedChanges);
            }
        }

        [TestMethod]
        public void WhenPassingBranchRejoinSelectionIsCanceledThenModelAndHistoryRemainUnchanged()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                bool dirty = editor.HasUnsavedChanges;
                bool canUndo = editor.CanUndo;
                bool canRedo = editor.CanRedo;

                PathEditorCommandResult begin = editor.BeginPassingBranchCommand(0);
                PathEditorCommandResult cancel = editor.CancelPathInteractionCommand();

                Assert.IsTrue(begin.Success);
                Assert.AreEqual(PassingBranchAuthoringPhase.Idle, editor.PassingBranchPhase);
                Assert.IsTrue(cancel.Success);
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual(dirty, editor.HasUnsavedChanges);
                Assert.AreEqual(canUndo, editor.CanUndo);
                Assert.AreEqual(canRedo, editor.CanRedo);
            }
        }

        [TestMethod]
        public void WhenSaveAsOverwriteIsDeclinedThenEditorStateRemainsUnchanged()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.SetStartAnchorCommand(CreateNodeAt(25, PathNodeType.None, -1), false);
                Assert.IsTrue(editor.Undo());
                PathModel currentModel = editor.TryCaptureCurrentPathModel();
                bool dirty = editor.HasUnsavedChanges;
                bool canUndo = editor.CanUndo;
                bool canRedo = editor.CanRedo;
                TrainPathSaveRequest request = new TrainPathSaveRequest(new PathModelHeader { Id = "existing-copy", Name = "Copy" }, source.Id, false);

                bool canSubmit = request.CanSubmit(true);

                Assert.IsFalse(canSubmit);
                Assert.AreSame(currentModel, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual(dirty, editor.HasUnsavedChanges);
                Assert.AreEqual(canUndo, editor.CanUndo);
                Assert.AreEqual(canRedo, editor.CanRedo);
            }
        }

        [TestMethod]
        public async Task WhenRejectedDuplicateSaveIsConsumedThenItCannotClearTheActiveSaveOrPermitAThirdSave()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                TaskCompletionSource<PathPersistenceValidationResult> firstPersistence = new TaskCompletionSource<PathPersistenceValidationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                PathSaveOperation first = CreatePendingSaveOperation(editor, source, firstPersistence.Task);
                PathSaveOperation duplicate = editor.BeginSave(source);

                PathPersistenceValidationResult duplicateResult = await PathSaveOperationConsumer.ConsumeAsync(editor, duplicate, action => action()).ConfigureAwait(false);
                PathSaveOperation third = editor.BeginSave(source);

                Assert.IsTrue(editor.IsSaveInProgress);
                Assert.IsFalse(duplicateResult.PersistenceAllowed);
                Assert.IsFalse(third.Acquired);

                firstPersistence.SetResult(new PathPersistenceValidationResult(true, source, null, default, default, null, null));
                _ = await PathSaveOperationConsumer.ConsumeAsync(editor, first, action => action()).ConfigureAwait(false);

                Assert.IsFalse(editor.IsSaveInProgress);
            }
        }

        [TestMethod]
        public async Task WhenCleanLoadedPathIsSavingThenContinuationIsUnavailableUntilSaveEnds()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld())))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false));
                Assert.IsTrue(editor.CanContinuePath);

                TaskCompletionSource<PathPersistenceValidationResult> persistence = new TaskCompletionSource<PathPersistenceValidationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                PathSaveOperation save = CreatePendingSaveOperation(editor, source, persistence.Task);

                Assert.IsTrue(editor.IsSaveInProgress);
                Assert.IsFalse(editor.CanContinuePath);
                Assert.IsFalse(editor.ContinuePathCommand().Success);
                Assert.IsFalse(editor.IsBuildingRoute);

                persistence.SetResult(new PathPersistenceValidationResult(true, source, null, default, default, null, null));
                _ = await PathSaveOperationConsumer.ConsumeAsync(editor, save, action => action()).ConfigureAwait(false);

                Assert.IsFalse(editor.IsSaveInProgress);
                Assert.IsTrue(editor.CanContinuePath);
            }
        }

        [TestMethod]
        public async Task WhenPersistenceThrowsUnexpectedExceptionThenSaveAvailabilityAndEditorStateAreRestored()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                _ = editor.SetStartAnchorCommand(CreateNodeAt(25, PathNodeType.None, -1), false);
                PathModel committedModel = editor.TryCaptureCurrentPathModel();
                bool dirty = editor.HasUnsavedChanges;
                bool canUndo = editor.CanUndo;
                bool canRedo = editor.CanRedo;
                PathSaveOperation operation = CreatePendingSaveOperation(editor, committedModel, Task.FromException<PathPersistenceValidationResult>(new NotSupportedException("Test persistence failure.")));

                await PathSaveOperationConsumer.ConsumeAsync(editor, operation, action => action()).ContinueWith(completedSave =>
                {
                    Assert.IsTrue(completedSave.IsFaulted);
                }, TestContext.CancellationToken, TaskContinuationOptions.None, TaskScheduler.Default).ConfigureAwait(false);

                Assert.IsFalse(editor.IsSaveInProgress);
                Assert.AreSame(committedModel, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual(dirty, editor.HasUnsavedChanges);
                Assert.AreEqual(canUndo, editor.CanUndo);
                Assert.AreEqual(canRedo, editor.CanRedo);
            }
        }

        [TestMethod]
        public async Task WhenSaveResultIsConsumedThenEditorCompletionRunsThroughTheGameThreadBridge()
        {
            PathModel source = CreateEditablePath();
            using (PathEditor editor = CreateEditor(source))
            {
                int bridgeThreadId = -1;
                int completionThreadId = -1;
                editor.OnPathChanged += (_, _) => completionThreadId = Environment.CurrentManagedThreadId;
                PathModel capturedSource = editor.TryCaptureCurrentPathModel();
                PathSaveOperation operation = CreatePendingSaveOperation(editor, capturedSource,
                Task.FromResult(new PathPersistenceValidationResult(true, capturedSource, null, default, default, null, null)));

                _ = await PathSaveOperationConsumer.ConsumeAsync(editor, operation, async action =>
                {
                    bridgeThreadId = Environment.CurrentManagedThreadId;
                    await action().ConfigureAwait(false);
                }).ConfigureAwait(false);

                Assert.AreEqual(bridgeThreadId, completionThreadId);
            }
        }

        [TestMethod]
        public void WhenWarningOnlyPathIsReResolvedThenItUsesTheSaveValidationPolicy()
        {
            PathModel source = new PathModel()
            {
                PathNodes = ImmutableArray.Create(
                    CreateNodeAt(100, PathNodeType.Start, 1) with { NodeIndex = 1 },
                    CreateNodeAt(400, PathNodeType.End, -1) with { NodeIndex = 4 }),
            };
            TrackWorld trackWorld = CreateAmbiguousTrackWorld();

            PathPersistenceValidationResult saveValidation = PathPersistenceValidationPolicy.ValidateForPersistence(source, trackWorld);
            PathEditResult reResolved = PathEditor.SnapPathToTrack(source, trackWorld);

            Assert.IsTrue(saveValidation.PersistenceAllowed);
            Assert.IsTrue(reResolved.Success);
            Assert.AreSequenceEqual(saveValidation.PathModel.PathNodes, reResolved.PathModel.PathNodes);
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
                    CreatePassingNode(PathNodeType.Via, -1, 1)),
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

        private static PathModel CreateSidingEndpointPath(TrackWorld trackWorld)
        {
            return new PathModel
            {
                Id = "siding-endpoint-path",
                Name = "Siding Endpoint Path",
                PathNodes = ImmutableArray.Create(
                    CreateAnchoredNode(trackWorld, 1, PathNodeType.Start, 1, -1),
                    CreateAnchoredNode(trackWorld, 4, PathNodeType.End, -1, -1)),
            };
        }

        private static PathModel CreateSidingTruncationPassingBranchPath(TrackWorld trackWorld)
        {
            return new PathModel
            {
                Id = "siding-truncation-path",
                Name = "Siding Truncation Path",
                PathNodes = ImmutableArray.Create(
                    CreateAnchoredNode(trackWorld, 1, PathNodeType.Start, 1, 4),
                    CreateAnchoredNode(trackWorld, 4, PathNodeType.Via, 2, -1),
                    CreateAnchoredNode(trackWorld, 6, PathNodeType.Via, 3, -1),
                    CreateAnchoredNode(trackWorld, 2, PathNodeType.End, -1, -1),
                    CreateAnchoredNode(trackWorld, 5, PathNodeType.Via, -1, 2)),
            };
        }

        private static PathModel CreateSupportedPassingBranchPath()
        {
            return new PathModel
            {
                Id = "passing-path",
                Name = "Passing Path",
                PathNodes = ImmutableArray.Create(
                    CreateNodeAt(0, PathNodeType.Start, 1) with { NextSidingNode = 3 },
                    CreateNodeAt(40, PathNodeType.Via, 2),
                    CreateNodeAt(100, PathNodeType.End, -1),
                    CreateNodeAt(60, PathNodeType.Via, -1) with { NextSidingNode = 2 }),
            };
        }

        private static PathModel CreateResolvablePassingBranchPath(TrackWorld trackWorld)
        {
            return new PathModel
            {
                Id = "resolvable-passing-path",
                Name = "Resolvable Passing Path",
                PathNodes = ImmutableArray.Create(
                    CreateAnchoredNode(trackWorld, 1, PathNodeType.Start, 1, 3),
                    CreateAnchoredNode(trackWorld, 4, PathNodeType.Via, 2, -1),
                    CreateAnchoredNode(trackWorld, 2, PathNodeType.End, -1, -1),
                    CreateAnchoredNode(trackWorld, 5, PathNodeType.Via, -1, 2)),
            };
        }

        private static PathModel CreateTruncationPassingBranchPath()
        {
            return new PathModel
            {
                Id = "truncation-passing-path",
                Name = "Truncation Passing Path",
                PathNodes = ImmutableArray.Create(
                    CreateNodeAt(0, PathNodeType.Start, 1),
                    CreateNodeAt(20, PathNodeType.Via, 2) with { NextSidingNode = 6 },
                    CreateNodeAt(40, PathNodeType.Via, 3),
                    CreateNodeAt(60, PathNodeType.Via, 4),
                    CreateNodeAt(80, PathNodeType.Via, 5),
                    CreateNodeAt(100, PathNodeType.End, -1),
                    CreateNodeAt(50, PathNodeType.Via, -1) with { NextSidingNode = 7 },
                    CreateNodeAt(70, PathNodeType.Via, -1) with { NextSidingNode = 3 }),
            };
        }

        private static PathModel CreateNoncanonicalLinearPath()
        {
            return new PathModel
            {
                Id = "noncanonical-linear-path",
                Name = "Noncanonical Linear Path",
                PathNodes = ImmutableArray.Create(
                    CreateNodeAt(0, PathNodeType.Start, 2),
                    CreateNodeAt(100, PathNodeType.End, -1),
                    CreateNodeAt(50, PathNodeType.Via, 1)),
            };
        }

        private static PathNode CreateAnchoredNode(TrackWorld trackWorld, int trackNodeIndex, PathNodeType nodeType, int nextMainNode, int nextSidingNode)
        {
            return new PathNode(trackWorld.TrackDatabase.TrackNodes[trackNodeIndex].Location)
            {
                NodeType = nodeType,
                NodeIndex = trackNodeIndex,
                NextMainNode = nextMainNode,
                NextSidingNode = nextSidingNode,
            };
        }

        private static PathEditResult InvokeMoveSelectedAuthoredNode(PathModel pathModel, int nodeIndex, PathNode anchor)
        {
            return (PathEditResult)typeof(PathEditor).GetMethod("MoveSelectedAuthoredNode", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { pathModel, nodeIndex, anchor, false });
        }

        private static PathModel CreateNonRejoiningPassingPath()
        {
            return new PathModel
            {
                Id = "broken-passing-path",
                Name = "Broken Passing Path",
                PathNodes = ImmutableArray.Create(
                    CreateNodeAt(0, PathNodeType.Start, 1) with { NextSidingNode = 2 },
                    CreateNodeAt(50, PathNodeType.Via, 3),
                    CreateNodeAt(25, PathNodeType.Via, -1),
                    CreateNodeAt(100, PathNodeType.End, -1)),
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

        private static TrackWorld CreatePlacementTrackWorld(out TrackSegmentBase segment)
        {
            return CreatePlacementTrackWorld(new Tile(0, 0), out segment);
        }

        [TestMethod]
        public async Task WhenEndpointsAndTailAreEditedThenPhysicalLengthTracksCommitUndoAndRedo()
        {
            TrackWorld world = CreatePlacementTrackWorld(new Tile(1, 0), out TrackSegmentBase segment);
            WorldLocation At(float x) => new WorldLocation(new Tile(1, 0), new Vector3(x, 0, 0));
            PathModel source = new PathModel
            {
                Id = "physical-length-editing",
                PathNodes = ImmutableArray.Create(
                    new PathNode(At(10)) { NodeType = PathNodeType.Start, NodeIndex = 1, NextMainNode = 1, NextSidingNode = -1 },
                    new PathNode(At(50)) { NodeType = PathNodeType.Via, NodeIndex = 1, NextMainNode = 2, NextSidingNode = -1 },
                    new PathNode(At(90)) { NodeType = PathNodeType.End, NodeIndex = 1, NextMainNode = -1, NextSidingNode = -1 }),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false));
                Assert.AreEqual(80f, editor.TrainPath.Length, 0.001f);
                Assert.IsTrue(editor.BeginMoveNode(0));
                editor.UpdatePointerLocation(PointD.FromWorldLocation(At(25)), segment);
                Assert.AreEqual(80f, editor.TrainPath.Length, 0.001f, "Preview must not change committed distance.");
                Assert.IsTrue(editor.CommitMoveNode().Success);
                Assert.AreEqual(65f, editor.TrainPath.Length, 0.001f);
                Assert.IsTrue(editor.Undo());
                Assert.AreEqual(80f, editor.TrainPath.Length, 0.001f);
                Assert.IsTrue(editor.Redo());
                Assert.AreEqual(65f, editor.TrainPath.Length, 0.001f);

                Assert.IsTrue(editor.BeginMoveNode(2));
                editor.UpdatePointerLocation(PointD.FromWorldLocation(At(75)), segment);
                Assert.IsTrue(editor.CancelPlacement());
                Assert.AreEqual(65f, editor.TrainPath.Length, 0.001f);
                Assert.IsTrue(editor.RemoveRestOfPath(1).Success);
                Assert.AreEqual(25f, editor.TrainPath.Length, 0.001f);
                Assert.IsTrue(editor.Undo());
                Assert.AreEqual(65f, editor.TrainPath.Length, 0.001f);
                Assert.IsTrue(editor.Redo());
                Assert.AreEqual(25f, editor.TrainPath.Length, 0.001f);
            }
        }

        private static TrackWorld CreatePlacementTrackWorld(Tile tile, out TrackSegmentBase segment)
        {
            WorldLocation start = new WorldLocation(tile, Vector3.Zero);
            WorldLocation end = new WorldLocation(tile, new Vector3(100, 0, 0));
            VectorSectionNode section = new VectorSectionNode(start, tile, new Vector3(0, MathHelper.PiOver2, 0), end)
            {
                NodeIndex = 1,
            };
            VectorNode vectorNode = new VectorNode(start, tile, end)
            {
                NodeIndex = 1,
                VectorSections = ImmutableArray.Create(section),
            };
            TrackDatabase trackDatabase = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, vectorNode),
                TrackNodeConnectors = ImmutableArray.Create(
                    new TrackNodeConnectorIndex { TrackNodeConnectors = ImmutableArray<TrackNodeConnector>.Empty },
                    new TrackNodeConnectorIndex { TrackNodeConnectors = ImmutableArray<TrackNodeConnector>.Empty }),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(trackDatabase);
            TrackSectionModel trackSections = new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1, new TrackSection
                {
                    SectionIndex = 1,
                    Gauge = 1.435f,
                    Length = 100,
                }),
            };
            TrackWorld trackWorld = TrackWorld.Initialize(null, new TrackModel { TrackDatabase = trackDatabase }, trackSections);
            segment = trackWorld.SegmentSections[1].SectionSegments.Single();
            return trackWorld;
        }

        private static TrackWorld CreateEventJunctionTrackWorld(out TrackSegmentBase segment)
        {
            Tile tile = new Tile(1, 0);
            TrackWorld placementWorld = CreatePlacementTrackWorld(tile, out segment);
            WorldLocation junctionLocation = new WorldLocation(tile, Vector3.Zero);
            WorldLocation approachLocation = new WorldLocation(tile, new Vector3(-100, 0, 0));
            VectorSectionNode approachSection = new VectorSectionNode(approachLocation, tile,
                new Vector3(0, MathHelper.PiOver2, 0), junctionLocation) { NodeIndex = 1 };
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, placementWorld.TrackDatabase.TrackNodes[1],
                    new JunctionNode(junctionLocation, tile, Vector3.Zero) { NodeIndex = 2 },
                    new VectorNode(approachLocation, tile, junctionLocation)
                    {
                        NodeIndex = 3,
                        VectorSections = ImmutableArray.Create(approachSection),
                    }),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(),
                    new TrackNodeConnectorIndex
                    {
                        NodeIndex = 1,
                        TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnector { Link = 2, ConnectorType = ConnectorType.InPin }),
                    },
                    new TrackNodeConnectorIndex
                    {
                        NodeIndex = 2,
                        TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnector { Link = 3, ConnectorType = ConnectorType.InPin },
                            new TrackNodeConnector { Link = 1, ConnectorType = ConnectorType.OutPin }),
                    },
                    new TrackNodeConnectorIndex
                    {
                        NodeIndex = 3,
                        TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnector { Link = 2, ConnectorType = ConnectorType.OutPin }),
                    }),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            TrackSectionModel sections = new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1,
                    new TrackSection { SectionIndex = 1, Gauge = 1.435f, Length = 100 }),
            };
            TrackWorld world = TrackWorld.Initialize(null, new TrackModel { TrackDatabase = database }, sections);
            segment = world.SegmentSections[1].SectionSegments.Single();
            return world;
        }

        private static TrackWorld CreateEventSidingTrackWorld()
        {
            // Unlike topology-only tests, editor repairs revalidate generated junctions through the spatial index,
            // which excludes Tile.Zero.
            Tile tile = new Tile(1, 0);
            VectorNode RenderableVector(int index)
            {
                VectorNode node = CreateVectorNode(index, tile);
                return node with
                {
                    VectorSections = ImmutableArray.Create(new VectorSectionNode(node.Location, tile,
                        new Vector3(0, MathHelper.PiOver2, 0), new WorldLocation(tile, new Vector3(index * 100 + 50, 0, 0)))
                    { NodeIndex = 1 }),
                };
            }
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    RenderableVector(1), RenderableVector(2), CreateJunctionNode(3, tile),
                    RenderableVector(4), RenderableVector(5), CreateJunctionNode(6, tile)),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(),
                    CreateConnectors(1, 3), CreateConnectors(2, 6), CreateConnectors(3, 1, 4, 5),
                    CreateConnectors(4, 3, 6), CreateConnectors(5, 3, 6), CreateConnectors(6, 2, 4, 5)),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            return TrackWorld.Initialize(null, new TrackModel { TrackDatabase = database }, new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1,
                    new TrackSection { SectionIndex = 1, Gauge = 1.435f, Length = 50 }),
            });
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

        private static RouteModel CreateLoadTestRoute()
        {
            RouteModel route = new RouteModel(WorldLocation.None)
            {
                Id = $"load-route-{Guid.NewGuid():N}",
                Name = "Load Test Route",
            };
            route.Initialize(null);
            return route;
        }

        private static TaskCompletionSource<PathModelHeader> CreatePathLoadCompletion() => new TaskCompletionSource<PathModelHeader>(TaskCreationOptions.RunContinuationsAsynchronously);

        private static PathModelHeader CreatePendingPathHeader(RouteModelHeader route, string pathId, Task<PathModelHeader> loadTask)
        {
            PathModelHeader header = new PathModelHeader { Id = pathId, Name = pathId };
            header.Initialize(route);
            TestPathModelHandler.SetLoadTask(route.Hierarchy(pathId), loadTask);
            return header;
        }

        private static PathModel CreateLoadedPath(RouteModelHeader route, string pathId)
        {
            PathModel pathModel = CreateEditablePath() with { Id = pathId, Name = pathId };
            pathModel.Initialize(route);
            return pathModel;
        }

        private static void RemovePendingPath(RouteModelHeader route, string pathId) => TestPathModelHandler.RemoveLoadTask(route.Hierarchy(pathId));

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
            return CreateEditor(new PathModel
            {
                Id = "start-only-path",
                Name = "Start Only Path",
                PathNodes = ImmutableArray.Create(CreateNodeAt(0, PathNodeType.Start, -1)),
            });
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

        private static TrackWorld CreateTargetSideTrackWorld()
        {
            TrackDatabase trackDatabase = new TrackDatabase()
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null, CreateVectorNode(1), CreateVectorNode(2), CreateJunctionNode(3), CreateJunctionNode(4), CreateVectorNode(5)),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(), CreateConnectors(1, 3, 4), CreateConnectors(2, 3),
                    CreateConnectors(3, 1, 2), CreateConnectors(4, 1, 5), CreateConnectors(5, 4)),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(trackDatabase);
            TrackModel trackModel = new TrackModel() { TrackDatabase = trackDatabase };

            return TrackWorld.Initialize(null, trackModel, new TrackSectionModel());
        }

        private static VectorNode CreateVectorNode(int nodeIndex)
        {
            return CreateVectorNode(nodeIndex, new Tile(0, 0));
        }

        private static VectorNode CreateVectorNode(int nodeIndex, Tile tile)
        {
            WorldLocation start = new WorldLocation(tile, new Vector3(nodeIndex * 100, 0, 0));
            WorldLocation end = new WorldLocation(tile, new Vector3((nodeIndex * 100) + 50, 0, 0));
            return new VectorNode(start, tile, end) { NodeIndex = nodeIndex };
        }

        private static JunctionNode CreateJunctionNode(int nodeIndex)
        {
            return CreateJunctionNode(nodeIndex, new Tile(0, 0));
        }

        private static JunctionNode CreateJunctionNode(int nodeIndex, Tile tile)
        {
            return new JunctionNode(new WorldLocation(tile, new Vector3(nodeIndex * 100, 0, 0)),
                tile, Vector3.Zero)
            { NodeIndex = nodeIndex };
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

        private static PathEditResult InvokeAddRoutePoint(PathModel pathModel, PathNode anchor)
        {
            MethodInfo method = typeof(PathEditor).GetMethod("AddRoutePoint", BindingFlags.Static | BindingFlags.NonPublic);
            return (PathEditResult)method.Invoke(null, new object[] { pathModel, anchor, false });
        }

        [TestMethod]
        public void WhenPathHasEndThenPrecedingEndNodeIsTheLastCommittedRoutePoint()
        {
            // The automatic reversal marks the route point committed just before the end anchor, which is the
            // node linking to it on the main chain rather than simply the second-to-last array entry.
            PathModel pathModel = new PathModel
            {
                PathNodes = ImmutableArray.Create(
                    CreateNodeAt(0, PathNodeType.Start, 2),
                    CreateNodeAt(100, PathNodeType.End, -1),
                    CreateNodeAt(50, PathNodeType.Via, 1)),
            };

            int precedingIndex = InvokePrecedingEndNodeIndex(pathModel);

            Assert.AreEqual(2, precedingIndex);
        }

        [TestMethod]
        public void WhenPathHasNoEndThenPrecedingEndNodeIsNotFound()
        {
            PathModel pathModel = new PathModel
            {
                PathNodes = ImmutableArray.Create(CreateNodeAt(0, PathNodeType.Start, -1)),
            };

            int precedingIndex = InvokePrecedingEndNodeIndex(pathModel);

            Assert.AreEqual(-1, precedingIndex);
        }

        private static int InvokePrecedingEndNodeIndex(PathModel pathModel)
        {
            MethodInfo method = typeof(PathEditor).GetMethod("PrecedingEndNodeIndex", BindingFlags.Static | BindingFlags.NonPublic);
            return (int)method.Invoke(null, new object[] { pathModel });
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

        private static PathModel CreateMainEventPath(PathNodeType firstEvent, PathNodeType secondEvent)
        {
            return new PathModel
            {
                Id = "main-event-path",
                PathNodes = ImmutableArray.Create(CreateNodeAt(0, PathNodeType.Start, 1),
                    CreateNodeAt(20, PathNodeType.Via | firstEvent, 2), CreateNodeAt(40, PathNodeType.Via | secondEvent, 3),
                    CreateNodeAt(60, PathNodeType.Via, 4), CreateNodeAt(100, PathNodeType.End, -1)),
            };
        }

        private static PathNode CreateNodeAt(float x, PathNodeType nodeType, int nextMainNode)
        {
            return CreateNodeAt(new Tile(0, 0), x, nodeType, nextMainNode);
        }

        private static PathNode CreateNodeAt(Tile tile, float x, PathNodeType nodeType, int nextMainNode)
        {
            return new PathNode(new WorldLocation(tile, new Vector3(x, 0, 0)))
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

        private static void TriggerPointerCommand(UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand> commandController,
            CommonUserCommand command, UserCommandArgs commandArgs)
        {
            MethodInfo trigger = typeof(UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand>)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(method => method.Name == "Trigger"
                    && method.GetParameters()[0].ParameterType == typeof(CommonUserCommand));
            trigger.Invoke(commandController, new object[] { command, commandArgs, new GameTime(), KeyModifiers.None });
        }

        private static PathSaveOperation CreatePendingSaveOperation(PathEditor editor, PathModel sourceModel,
            Task<PathPersistenceValidationResult> persistenceTask)
        {
            PathSaveOperation operation = new PathSaveOperation(true, sourceModel, sourceModel.Id, persistenceTask);
            SetPrivateField(editor, "activeSaveOperation", operation);
            return operation;
        }

        private sealed class TestPathEditorContext : IPathEditorContext, IPathEditorContextServicesAccessor
        {
            private readonly IPathEditorServices services;

            public IMapRenderer Renderer => null;

            public IMapViewport Viewport => null;

            public ToolboxContentMode ContentMode { get; set; }

            public PathEditorBase PathEditor { get; set; }

            public void RequestRedraw() { }

            IPathEditorServices IPathEditorContextServicesAccessor.Services => services;

            public TestPathEditorContext(TrackWorld trackWorld)
            {
                services = new PathEditorServices(trackWorld);
            }
        }

        /// <summary>Non-instantiable test accessor for the base handler's protected static model cache.</summary>
        private abstract class TestPathModelHandler : ContentHandlerBase<PathModelHeader>
        {
            public static void SetLoadTask(string key, Task<PathModelHeader> loadTask)
                => modelTaskCache[key] = loadTask;

            public static void RemoveLoadTask(string key)
                => modelTaskCache.TryRemove(key, out _);
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
                : base(location, PathNodeType.Via)
            {
            }
        }

        private static TrackWorld CreateInitializedTrackWorld() => TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();

        public TestContext TestContext { get; set; }
    }
}
