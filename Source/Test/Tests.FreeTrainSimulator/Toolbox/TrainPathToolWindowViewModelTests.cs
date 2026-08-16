using System;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class TrainPathToolWindowViewModelTests
    {
        private static TrainPathToolWindow CreateBridge(Action<Action> invoker)
        {
            return CreateBridge(invoker, () => { }, () => { });
        }

        [TestMethod]
        public void WhenSetStartHereCommandExecutedThenBridgeCommandIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
            using TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler);
            SetCommandAvailability(viewModel, "canPlaceStartAnchor", true);

            viewModel.SetStartHereCommand.Execute(null);

            Assert.AreEqual(1, invocations);
            Assert.AreEqual("Select a valid track location for the start anchor.", viewModel.StatusMessage);
        }

        [TestMethod]
        public void WhenSetEndHereCommandDisabledThenItCannotExecute()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
            using TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler);

            Assert.IsFalse(viewModel.SetEndHereCommand.CanExecute(null));
        }

        [TestMethod]
        public void WhenCommitPlacementCommandExecutedThenBridgeCommandIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
            using TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler);
            SetCommandAvailability(viewModel, "canCommitPlacement", true);

            viewModel.CommitPlacementCommand.Execute(null);

            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenStartPlacementIsCanceledThenStatusIdentifiesStartAnchor()
        {
            TrainPathToolWindow bridge = CreateBridge(_ => { });
            using ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
            using TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler);
            SetCommandAvailability(viewModel, "canCancelPlacement", true);
            SetCommandAvailability(viewModel, "placementMode", PathEditorPlacementMode.StartAnchor);

            viewModel.CancelPlacementCommand.Execute(null);

            Assert.AreEqual("Start anchor placement canceled.", viewModel.StatusMessage);
        }

        private static TrainPathToolWindow CreateBridge(Action<Action> invoker, Action createPathAction, Action savePathAction)
        {
            return new TrainPathToolWindow(() => null, () => null, invoker, createPathAction, savePathAction, _ => { }, () => { }, () => { });
        }

        [TestMethod]
        public void WhenSelectedPathSetThenBridgeSelectPathIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedPath = new TrainPathListItemViewModel("path-1", "First Path", PathValidationState.NotValidated)
                })
                {
                    Assert.AreEqual(1, invocations);
                }
            }
        }

        [TestMethod]
        public void WhenSelectedPathClearedThenBridgeSelectPathIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedPath = new TrainPathListItemViewModel("path-1", "First Path", PathValidationState.NotValidated)
                })
                {
                    trainPathToolWindowViewModel.SelectedPath = null;
                }
            }

            Assert.AreEqual(2, invocations);
        }

        [TestMethod]
        public void WhenUndoCommandExecutedThenBridgeUndoIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    SetCommandAvailability(trainPathToolWindowViewModel, "canUndo", true);

                    trainPathToolWindowViewModel.UndoCommand.Execute(null);

                    Assert.AreEqual(1, invocations);
                }
            }
        }

        [TestMethod]
        public void WhenRedoCommandExecutedThenBridgeRedoIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    SetCommandAvailability(trainPathToolWindowViewModel, "canRedo", true);

                    trainPathToolWindowViewModel.RedoCommand.Execute(null);

                    Assert.AreEqual(1, invocations);
                }
            }
        }

        [TestMethod]
        public void WhenNewPathCommandExecutedThenStartPlacementIsMarshaled()
        {
            int invocations = 0;
            int createActions = 0;
            TrainPathToolWindow bridge = CreateBridge(action => { invocations++; action(); }, () => createActions++, () => { });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    SetCommandAvailability(trainPathToolWindowViewModel, "canCreatePath", true);

                    trainPathToolWindowViewModel.NewPathCommand.Execute(null);

                    Assert.AreEqual(1, invocations);
                    Assert.AreEqual(1, createActions);
                }
            }
        }

        [TestMethod]
        public void WhenNewPathCommandExecutedThenProgressiveRouteGuidanceIsShown()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action(), () => { }, () => { });
            using ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
            using TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler);
            SetCommandAvailability(viewModel, "canCreatePath", true);

            viewModel.NewPathCommand.Execute(null);

            Assert.AreEqual("Click track to set the start; continue clicking to add route points, then double-click to finish.", viewModel.StatusMessage);
        }

        [TestMethod]
        public void WhenSavePathCommandExecutedThenBridgeSavePathIsMarshaled()
        {
            int invocations = 0;
            int saveActions = 0;
            TrainPathToolWindow bridge = CreateBridge(action => { invocations++; action(); }, () => { }, () => saveActions++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    SetCommandAvailability(trainPathToolWindowViewModel, "canSavePath", true);

                    trainPathToolWindowViewModel.SavePathCommand.Execute(null);

                    Assert.AreEqual(1, invocations);
                    Assert.AreEqual(1, saveActions);
                }
            }
        }

        [TestMethod]
        public void WhenMoveSelectedNodeCommandExecutedThenBridgeMoveNodeIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedNode = new TrainPathNodeItemViewModel(2, PathNodeType.Intermediate, true)
                })
                {
                    trainPathToolWindowViewModel.MoveSelectedNodeCommand.Execute(null);

                    Assert.AreEqual(2, invocations);
                    Assert.Contains("node 2", trainPathToolWindowViewModel.StatusMessage);
                }
            }
        }

        [TestMethod]
        public void WhenCommitMoveNodeCommandExecutedThenBridgeCommitMoveIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    SetCommandAvailability(trainPathToolWindowViewModel, "canCommitMoveNode", true);

                    trainPathToolWindowViewModel.CommitMoveNodeCommand.Execute(null);

                    Assert.AreEqual(1, invocations);
                    Assert.AreEqual("Commit move requested.", trainPathToolWindowViewModel.StatusMessage);
                }
            }
        }

        [TestMethod]
        public void WhenMoveHasNoValidPreviewThenCommitMoveCommandCannotExecute()
        {
            TrainPathToolWindow bridge = CreateBridge(_ => { });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    SetCommandAvailability(trainPathToolWindowViewModel, "canCancelMoveNode", true);

                    Assert.IsFalse(trainPathToolWindowViewModel.CommitMoveNodeCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenRepairSelectedNodeCommandExecutedThenBridgeRepairNodeIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedNode = new TrainPathNodeItemViewModel(2, PathNodeType.Intermediate, true)
                })
                {
                    trainPathToolWindowViewModel.RepairSelectedNodeCommand.Execute(null);

                    Assert.AreEqual(2, invocations);
                    Assert.Contains("node 2", trainPathToolWindowViewModel.StatusMessage);
                }
            }
        }

        [TestMethod]
        public void WhenCancelMoveNodeCommandExecutedThenBridgeCancelMoveIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    SetCommandAvailability(trainPathToolWindowViewModel, "canCancelMoveNode", true);

                    trainPathToolWindowViewModel.CancelMoveNodeCommand.Execute(null);

                    Assert.AreEqual(1, invocations);
                    Assert.AreEqual("Node move canceled.", trainPathToolWindowViewModel.StatusMessage);
                }
            }
        }

        [TestMethod]
        public void WhenMoveModeEndsThenMoveGuidanceStatusIsCleared()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedNode = new TrainPathNodeItemViewModel(2, PathNodeType.Intermediate, true)
                })
                {
                    trainPathToolWindowViewModel.MoveSelectedNodeCommand.Execute(null);
                    SetCommandAvailability(trainPathToolWindowViewModel, "canCancelMoveNode", true);
                    typeof(TrainPathToolWindowViewModel).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trainPathToolWindowViewModel, null);

                    Assert.AreEqual(string.Empty, trainPathToolWindowViewModel.StatusMessage);
                }
            }
        }

        [TestMethod]
        public void WhenHistoryUnavailableThenUndoRedoCommandsCannotExecute()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    Assert.IsFalse(trainPathToolWindowViewModel.UndoCommand.CanExecute(null));
                    Assert.IsFalse(trainPathToolWindowViewModel.RedoCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenSelectedPathSetToSameInstanceThenBridgeIsNotCalledAgain()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            TrainPathListItemViewModel path = new TrainPathListItemViewModel("path-1", "First Path", PathValidationState.NotValidated);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedPath = path
                })
                {
                    trainPathToolWindowViewModel.SelectedPath = path;
                }
            }
            Assert.AreEqual(1, invocations);
        }

        [TestMethod]
        public void WhenSelectedPathSetThenStatusMessageIsCleared()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedPath = new TrainPathListItemViewModel("path-1", "First Path", PathValidationState.NotValidated)
                })
                {
                    Assert.AreEqual(string.Empty, trainPathToolWindowViewModel.StatusMessage);
                }
            }
        }

        [TestMethod]
        public void WhenSelectedNodeSetThenBridgeHighlightNodeIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedNode = new TrainPathNodeItemViewModel(2, PathNodeType.Junction, true)
                })
                {
                    Assert.AreEqual(1, invocations);
                }
            }
        }

        [TestMethod]
        public void WhenSelectedNodeClearedThenBridgeHighlightNodeIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedNode = new TrainPathNodeItemViewModel(2, PathNodeType.Junction, true)
                })
                {
                    trainPathToolWindowViewModel.SelectedNode = null;
                }
            }

            Assert.AreEqual(2, invocations);
        }

        [TestMethod]
        public void WhenSearchTextMatchesPathThenPathRemainsVisible()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    TrainPathListItemViewModel match = new TrainPathListItemViewModel("p1", "Northbound", PathValidationState.NotValidated);
                    trainPathToolWindowViewModel.Paths.Add(match);

                    trainPathToolWindowViewModel.SearchText = "north";

                    Assert.IsTrue(match.IsVisible);
                }
            }
        }

        [TestMethod]
        public void WhenSearchTextDoesNotMatchPathThenPathIsHidden()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    TrainPathListItemViewModel other = new TrainPathListItemViewModel("p2", "Southbound", PathValidationState.NotValidated);
                    trainPathToolWindowViewModel.Paths.Add(other);

                    trainPathToolWindowViewModel.SearchText = "north";

                    Assert.IsFalse(other.IsVisible);
                }
            }
        }

        [TestMethod]
        public void WhenNodeRowUpdatedThenNodeDetailsAreUpdated()
        {
            TrainPathNodeItemViewModel node = new TrainPathNodeItemViewModel(new TrainPathNodeRow(1, PathNodeType.Wait, true, 7, 2, 3, 45, null));

            node.Update(new TrainPathNodeRow(4, PathNodeType.Invalid, false, 9, -1, -1, null, "NotOnTrack", 11, 5, 0.75));

            Assert.AreEqual(4, node.Index);
            Assert.AreEqual(PathNodeType.Invalid, node.NodeType);
            Assert.IsFalse(node.Valid);
            Assert.AreEqual(9, node.TrackNodeIndex);
            Assert.AreEqual(-1, node.NextMainNode);
            Assert.AreEqual(-1, node.NextSidingNode);
            Assert.IsNull(node.WaitTime);
            Assert.AreEqual("NotOnTrack", node.Validation);
            Assert.AreEqual(11, node.NearestTrackNodeIndex);
            Assert.AreEqual(5, node.NearestTrackSectionIndex);
            Assert.AreEqual(0.75, node.NearestTrackDistanceMeters);
        }

        [TestMethod]
        public void WhenSelectedNodeHasNearestTrackDiagnosticsThenDetailRowsContainDiagnostics()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler)
                {
                    SelectedNode = new TrainPathNodeItemViewModel(new TrainPathNodeRow(2, PathNodeType.End, false, 0, -1, -1, null, "NotOnTrack", 42, 3, 1.25))
                })
                {
                    Assert.AreEqual("42", trainPathToolWindowViewModel.SelectedNodeDetailRows.Single(row => row.Name == "Nearest Track Node").Value);
                    Assert.AreEqual("3", trainPathToolWindowViewModel.SelectedNodeDetailRows.Single(row => row.Name == "Nearest Track Section").Value);
                    Assert.AreEqual("1.25 m", trainPathToolWindowViewModel.SelectedNodeDetailRows.Single(row => row.Name == "Nearest Track Distance").Value);
                }
            }
        }

        [TestMethod]
        public void WhenNodeTypeContainsWaitFlagThenNodeReportsWaitPoint()
        {
            TrainPathNodeItemViewModel node = new TrainPathNodeItemViewModel(new TrainPathNodeRow(1, PathNodeType.Intermediate | PathNodeType.Wait, true, 7, 2, 3, 45, null));

            Assert.IsTrue(node.HasWaitPoint);
            Assert.IsFalse(node.HasReversalPoint);
        }

        [TestMethod]
        public void WhenNodeTypeContainsReversalFlagThenNodeReportsReversalPoint()
        {
            TrainPathNodeItemViewModel node = new TrainPathNodeItemViewModel(new TrainPathNodeRow(1, PathNodeType.Intermediate | PathNodeType.Reversal, true, 7, 2, 3, null, null));

            Assert.IsTrue(node.HasReversalPoint);
            Assert.IsFalse(node.HasWaitPoint);
        }

        [TestMethod]
        public void WhenRouteCandidateSelectedThenBridgePreviewIsMarshaled()
        {
            int marshaledInvocations = 0;
            TrainPathToolWindow bridge = CreateBridge(action => { marshaledInvocations++; action(); });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    trainPathToolWindowViewModel.SelectedRouteCandidate = new TrainPathRouteCandidateItemViewModel(new TrainPathRouteCandidateRow(1, 4, 0, "candidate"));

                    Assert.AreEqual(1, marshaledInvocations);
                }
            }
        }

        [TestMethod]
        public void WhenNoRouteCandidateSelectedThenAcceptCommandCannotExecute()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    Assert.IsFalse(trainPathToolWindowViewModel.AcceptRouteCandidateCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenRouteCandidateSelectedThenAcceptCommandCanExecute()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    trainPathToolWindowViewModel.SelectedRouteCandidate = new TrainPathRouteCandidateItemViewModel(new TrainPathRouteCandidateRow(1, 4, 0, "candidate"));

                    Assert.IsTrue(trainPathToolWindowViewModel.AcceptRouteCandidateCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenRouteCandidateRowUpdatedThenCandidateDetailsAreUpdated()
        {
            TrainPathRouteCandidateItemViewModel candidate = new TrainPathRouteCandidateItemViewModel(new TrainPathRouteCandidateRow(1, 4, 0, "first"));

            candidate.Update(new TrainPathRouteCandidateRow(2, 6, 1, "second"));

            Assert.AreEqual(2, candidate.FromNodeIndex);
            Assert.AreEqual(6, candidate.ToNodeIndex);
            Assert.AreEqual(1, candidate.CandidateIndex);
            Assert.AreEqual("second", candidate.Description);
        }

        [TestMethod]
        public void WhenNodeDiagnosticSelectedThenBridgeHighlightIsMarshaled()
        {
            int marshaledInvocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => marshaledInvocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.SelectedDiagnostic = new TrainPathDiagnosticItemViewModel(new TrainPathDiagnosticRow(
                        PathRouteDiagnosticSeverity.Error, PathRouteDiagnosticCode.AnchorNotOnTrack, "Node is off track.",
                        2, -1, -1, "Repair the node.", true));

                    Assert.AreEqual(1, marshaledInvocations);
                }
            }
        }

        [TestMethod]
        public void WhenAmbiguousDiagnosticSelectedThenMatchingRouteCandidateIsSelected()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    TrainPathRouteCandidateItemViewModel matchingCandidate = new TrainPathRouteCandidateItemViewModel(
                        new TrainPathRouteCandidateRow(1, 4, 0, "matching"));
                    viewModel.RouteCandidates.Add(new TrainPathRouteCandidateItemViewModel(new TrainPathRouteCandidateRow(0, 1, 0, "other")));
                    viewModel.RouteCandidates.Add(matchingCandidate);

                    viewModel.SelectedDiagnostic = new TrainPathDiagnosticItemViewModel(new TrainPathDiagnosticRow(
                        PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.AmbiguousRoute, "Several routes are available.",
                        -1, 1, 4, "Choose a route candidate.", false));

                    Assert.AreSame(matchingCandidate, viewModel.SelectedRouteCandidate);
                    Assert.AreEqual(3, viewModel.SelectedTabIndex);
                }
            }
        }

        [TestMethod]
        public void WhenRepairableDiagnosticSelectedThenRepairCommandMarshalsRepair()
        {
            int marshaledInvocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => marshaledInvocations++);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.SelectedDiagnostic = new TrainPathDiagnosticItemViewModel(new TrainPathDiagnosticRow(
                        PathRouteDiagnosticSeverity.Error, PathRouteDiagnosticCode.AnchorNotOnTrack, "Node is off track.",
                        2, -1, -1, "Repair the node.", true));

                    viewModel.RepairDiagnosticCommand.Execute(null);

                    Assert.AreEqual(2, marshaledInvocations);
                }
            }
        }

        private static void SetCommandAvailability(TrainPathToolWindowViewModel viewModel, string fieldName, object value)
        {
            typeof(TrainPathToolWindowViewModel).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(viewModel, value);
        }
    }
}
