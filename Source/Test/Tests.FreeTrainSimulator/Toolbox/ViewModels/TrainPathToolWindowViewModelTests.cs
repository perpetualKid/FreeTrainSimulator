using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.Settings;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

namespace Tests.FreeTrainSimulator.Toolbox.ViewModels
{
    [TestClass]
    public class TrainPathToolWindowViewModelTests
    {
        [TestMethod]
        [DataRow(-1, "direct,span,other")]
        [DataRow(0, "direct,span")]
        [DataRow(2, "span")]
        [DataRow(1, "other")]
        public void WhenDiagnosticScopeIsAppliedThenOnlyDirectAndIncidentIssuesAreVisible(int selectedIndex, string expected)
        {
            TrainPathToolWindow bridge = CreateBridge(_ => { });
            SetBridgeSnapshot(bridge, CreateIssueScopeSnapshot(selectedIndex));
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    Assert.AreEqual(expected, string.Join(",", viewModel.VisibleDiagnostics.Select(issue => issue.Message)));
                }
            }
        }

        [TestMethod]
        public void WhenNodeSelectionIsClearedThenAllDiagnosticRowsReturnWithoutChangingTheSource()
        {
            TrainPathToolWindow bridge = CreateBridge(_ => { });
            SetBridgeSnapshot(bridge, CreateIssueScopeSnapshot(0));
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();
                    TrainPathDiagnosticItemViewModel[] source = viewModel.Diagnostics.ToArray();

                    viewModel.UserSelectNode(null);

                    Assert.AreSequenceEqual(source, viewModel.VisibleDiagnostics.ToArray());
                }
            }
        }

        [TestMethod]
        [DataRow(0, "direct")]
        [DataRow(3, null)]
        public void WhenADifferentNodeIsSelectedThenUnrelatedDiagnosticDetailIsReplacedOrCleared(int selectedIndex, string expected)
        {
            TrainPathToolWindow bridge = CreateBridge(_ => { });
            SetBridgeSnapshot(bridge, CreateIssueScopeSnapshot(1));
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    viewModel.UserSelectNode(viewModel.Nodes[selectedIndex]);

                    Assert.AreEqual(expected, viewModel.SelectedDiagnostic?.Message);
                }
            }
        }

        [TestMethod]
        public void WhenScopedDiagnosticsPollThenNoHighlightOrPreviewCommandsAreRequested()
        {
            int commands = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => commands++);
            TrainPathSnapshot snapshot = CreateIssueScopeSnapshot(0);
            SetBridgeSnapshot(bridge, snapshot);
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    SetBridgeSnapshot(bridge, snapshot with { SelectedNodeIndex = 2 });
                    viewModel.Start();
                    viewModel.Start();

                    Assert.AreEqual(0, commands);
                }
            }
        }

        [TestMethod]
        public void WhenAScopedAmbiguousIssueIsExplicitlySelectedThenCandidateNavigationStillRuns()
        {
            int commands = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => commands++);
            SetBridgeSnapshot(bridge, CreateIssueScopeSnapshot(2) with
            {
                RouteCandidates = [new TrainPathRouteCandidateRow(0, 2, 0, "route")],
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    viewModel.UserSelectDiagnostic(viewModel.SelectedDiagnostic);

                    Assert.AreEqual(2, commands);
                }
            }
        }

        private static TrainPathSnapshot CreateIssueScopeSnapshot(int selectedIndex)
        {
            return TrainPathSnapshot.Empty with
            {
                SelectedNodeIndex = selectedIndex,
                Nodes = Enumerable.Range(0, 4).Select(index => new TrainPathNodeRow(index, PathNodeType.Via, true,
                    10, -1, -1, null, null)).ToImmutableArray(),
                Diagnostics = [
                    new TrainPathDiagnosticRow(PathRouteDiagnosticSeverity.Error, PathRouteDiagnosticCode.AnchorNotOnTrack,
                        "direct", 0, -1, -1, string.Empty, true),
                    new TrainPathDiagnosticRow(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.AmbiguousRoute,
                        "span", -1, 0, 2, string.Empty, false),
                    new TrainPathDiagnosticRow(PathRouteDiagnosticSeverity.Error, PathRouteDiagnosticCode.AnchorNotOnTrack,
                        "other", 1, -1, -1, string.Empty, true)],
            };
        }

        private static TrainPathToolWindow CreateBridge(Action<Action> invoker)
        {
            return CreateBridge(invoker, () => { }, () => { });
        }

        [TestMethod]
        [DataRow(0, 0)]
        [DataRow(1, 1)]
        [DataRow(-1, 0)]
        [DataRow(2, 0)]
        public void WhenConstructedWithSavedTabThenTabIsRestoredBeforePolling(int savedTabIndex, int expectedTabIndex)
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler, savedTabIndex))
                {
                    Assert.AreEqual(expectedTabIndex, viewModel.SelectedTabIndex);
                }
            }
        }

        [TestMethod]
        public void WhenRestoredEditorTabPollsEmptySnapshotThenEditorTabIsRetained()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler, TrainPathToolWindowViewModel.EditorTabIndex))
                {
                    viewModel.Start();

                    Assert.AreEqual(TrainPathToolWindowViewModel.EditorTabIndex, viewModel.SelectedTabIndex);
                }
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public async Task WhenLastTabIsCapturedAndProfileReloadedThenNewViewModelRestoresLastTab(int lastTabIndex)
        {
            ProfileModel profile = new ProfileModel("last-tab-" + Guid.NewGuid().ToString("N"));
            ProfileToolboxSettingsModel settings = await profile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).ConfigureAwait(false);
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler, 1 - lastTabIndex))
                {
                    viewModel.SelectedTabIndex = lastTabIndex;
                    settings.PathEditorSelectedTabIndex = viewModel.SelectedTabIndex;
                    _ = await profile.UpdateSettingsModel(settings, CancellationToken.None).ConfigureAwait(false);
                }
            }

            ProfileToolboxSettingsModel restored = await profile.LoadSettingsModel<ProfileToolboxSettingsModel>(CancellationToken.None).ConfigureAwait(false);
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel restartedViewModel = new TrainPathToolWindowViewModel(bridge, scheduler, restored.PathEditorSelectedTabIndex))
                {
                    Assert.AreEqual(lastTabIndex, restartedViewModel.SelectedTabIndex);
                }
            }
        }

        [TestMethod]
        public void WhenRestoredPathsTabStartsWorkflowThenSnapshotStillNavigatesToEditor()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with { PlacementMode = PathEditorPlacementMode.StartAnchor });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler, TrainPathToolWindowViewModel.PathsTabIndex))
                {

                    viewModel.Start();

                    Assert.AreEqual(TrainPathToolWindowViewModel.EditorTabIndex, viewModel.SelectedTabIndex);
                }
            }
        }

        [TestMethod]
        public void WhenCleanPathCanContinueThenContinueCommandIsEnabledWithoutSave()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with { CanContinuePath = true });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    Assert.IsTrue(viewModel.ContinuePathCommand.CanExecute(null));
                    Assert.IsFalse(viewModel.SavePathCommand.CanExecute(null));

                    SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with { CanCancelPathInteraction = true });
                    viewModel.Start();

                    Assert.IsFalse(viewModel.ContinuePathCommand.CanExecute(null));
                    Assert.IsFalse(viewModel.SavePathCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenPassingBranchCandidatePhaseIsAppliedThenOnlyCancelPhaseActionIsEnabled()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                PassingBranchPhase = PassingBranchAuthoringPhase.SelectingCandidate,
                CanCancelPassingBranch = true,
                HasPendingPassingBranchCandidate = true,
            });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    Assert.AreEqual(PassingBranchAuthoringPhase.SelectingCandidate, viewModel.PassingBranchPhase);
                    Assert.IsTrue(viewModel.HasPendingPassingBranchCandidate);
                    Assert.IsTrue(viewModel.CancelPassingBranchCommand.CanExecute(null));
                    Assert.IsFalse(viewModel.BeginPassingBranchCommand.CanExecute(null));
                    Assert.IsFalse(viewModel.CompletePassingBranchCommand.CanExecute(null));
                    Assert.IsFalse(viewModel.RemovePassingBranchCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void WhenPassingRejoinPhaseIsAppliedThenCompleteReflectsSelectedNodeCapability(bool canComplete)
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                PassingBranchPhase = PassingBranchAuthoringPhase.SelectingRejoin,
                Nodes = [new TrainPathNodeRow(2, PathNodeType.Via, true, 1, 3, -1, null, null)],
                SelectedNodeIndex = 2,
                CanCompletePassingBranch = canComplete,
                CanCancelPassingBranch = true,
                CanCancelPathInteraction = true,
                CanMoveSelectedNode = true,
                CanRemoveSelectedViaPoint = true,
            });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    Assert.AreEqual(PassingBranchAuthoringPhase.SelectingRejoin, viewModel.PassingBranchPhase);
                    Assert.AreEqual(canComplete, viewModel.CompletePassingBranchCommand.CanExecute(null));
                    Assert.IsTrue(viewModel.CancelPassingBranchCommand.CanExecute(null));
                    Assert.IsFalse(viewModel.MoveSelectedNodeCommand.CanExecute(null));
                    Assert.IsFalse(viewModel.RemoveViaPointCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenSaveIsBlockedThenDiagnosticTargetHighlightIsMarshaled()
        {
            int marshaledInvocations = 0;
            TrainPathDiagnosticRow diagnostic = new TrainPathDiagnosticRow(
                PathRouteDiagnosticSeverity.Error, PathRouteDiagnosticCode.AnchorNotOnTrack, "Node is off track.",
                2, -1, -1, "Move the node onto track.", true);
            TrainPathToolWindow bridge = CreateBridge(action =>
            {
                marshaledInvocations++;
                action();
            });
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Diagnostics = [diagnostic],
                BlockedSaveMessage = "Path cannot be saved because a node is off track.",
                BlockedSaveDiagnostic = diagnostic,
                BlockedSaveFeedbackVersion = 1,
            });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    Assert.AreEqual(1, marshaledInvocations);
                }
            }
        }

        [TestMethod]
        public void WhenPathInteractionIsCancelableThenConflictingNodeCommandIsDisabled()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Nodes = [new TrainPathNodeRow(0, PathNodeType.Via, true, 1, -1, -1, null, null)],
                SelectedNodeIndex = 0,
                CanRemoveSelectedViaPoint = true,
                CanCancelPathInteraction = true,
            });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    Assert.IsFalse(viewModel.RemoveViaPointCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenPathInteractionIsCancelableThenCancelInteractionCommandIsEnabled()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with { CanCancelPathInteraction = true });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    Assert.IsTrue(viewModel.CancelPathInteractionCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenCandidateInteractionIsActiveThenSelectedCandidateCanBeAccepted()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                RouteCandidates = [new TrainPathRouteCandidateRow(0, 1, 0, "candidate")],
                CanCancelPathInteraction = true,
                HasPendingAmbiguousSpanCommit = true,
            });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();
                    viewModel.UserSelectRouteCandidate(viewModel.RouteCandidates[0]);

                    Assert.IsTrue(viewModel.AcceptRouteCandidateCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenSetEndHereCommandDisabledThenItCannotExecute()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    Assert.IsFalse(viewModel.SetEndHereCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenSaveAsTargetExistsWithoutConfirmationThenRequestCannotSubmit()
        {
            TrainPathSaveRequest request = new TrainPathSaveRequest(new PathModelHeader { Id = "copy", Name = "Copy" }, "original", false);

            Assert.IsTrue(request.IsSaveAs);
            Assert.IsFalse(request.CanSubmit(true));
        }

        [TestMethod]
        public void WhenSaveAsTargetExistsWithConfirmationThenRequestCanSubmit()
        {
            TrainPathSaveRequest request = new TrainPathSaveRequest(new PathModelHeader { Id = "copy", Name = "Copy" }, "original", true);

            Assert.IsTrue(request.CanSubmit(true));
        }

        [TestMethod]
        public void WhenNoRouteIsLoadedThenValidateAllPathsCommandIsDisabled()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    Assert.IsFalse(viewModel.ValidateAllPathsCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenValidateAllPathsCompletesThenCompletionIsReported()
        {
            TestToolingContext context = new TestToolingContext(() => Task.FromResult(ImmutableArray<PathModelHeader>.Empty));
            TrainPathToolWindow bridge = CreateBridge(action => action(), context);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    viewModel.ValidateAllPathsCommand.Execute(null);

                    Assert.AreEqual("Path validation complete.", viewModel.StatusMessage);
                }
            }
        }

        [TestMethod]
        public void WhenValidateAllPathsIsInvalidThenFailureIsReported()
        {
            AssertValidationFailure(new InvalidOperationException("Invalid path store."));
        }

        [TestMethod]
        public void WhenValidateAllPathsCannotReadThenFailureIsReported()
        {
            AssertValidationFailure(new IOException("Path store unavailable."));
        }

        [TestMethod]
        public void WhenValidateAllPathsAccessIsDeniedThenFailureIsReported()
        {
            AssertValidationFailure(new UnauthorizedAccessException("Path store access denied."));
        }

        private static TrainPathToolWindow CreateBridge(Action<Action> invoker, Action createPathAction, Action savePathAction)
        {
            return new TrainPathToolWindow(() => null, () => null, invoker, createPathAction, savePathAction, _ => { }, () => { }, () => { });
        }

        private static TrainPathToolWindow CreateBridge(Action<Action> invoker, ITrainPathToolingContext toolingContext)
        {
            return new TrainPathToolWindow(() => null, () => toolingContext, invoker, () => { }, () => { }, _ => { }, () => { }, () => { });
        }

        [TestMethod]
        public void WhenRepairModeSnapshotIsAppliedThenUnsafeRouteCommandsAreDisabledAndSafeNodeRemovalRemainsEnabled()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                IsRepairMode = true,
                Nodes = System.Collections.Immutable.ImmutableArray.Create(new TrainPathNodeRow(0, PathNodeType.Via, false, 1, -1, -1, null, "Broken link.")),
                SelectedNodeIndex = 0,
                CanMoveSelectedNode = true,
                CanRepairSelectedNode = true,
                CanRemoveSelectedViaPoint = true,
            });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.RouteCandidates.Add(new TrainPathRouteCandidateItemViewModel(new TrainPathRouteCandidateRow(0, 1, 0, "unsafe")));
                    viewModel.UserSelectRouteCandidate(viewModel.RouteCandidates[0]);

                    viewModel.Start();

                    Assert.IsTrue(viewModel.IsRepairMode);
                    Assert.IsTrue(viewModel.AreRepairNodeActionsVisible);
                    Assert.IsFalse(viewModel.AcceptRouteCandidateCommand.CanExecute(null));
                    Assert.IsFalse(viewModel.AddViaPointCommand.CanExecute(null));
                    Assert.IsTrue(viewModel.MoveSelectedNodeCommand.CanExecute(null));
                    Assert.IsTrue(viewModel.RepairSelectedNodeCommand.CanExecute(null));
                    Assert.IsTrue(viewModel.RemoveViaPointCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenNormalModeSnapshotIsAppliedThenRepairNodeActionsAreNotVisible()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    Assert.IsFalse(viewModel.IsRepairMode);
                    Assert.IsFalse(viewModel.AreRepairNodeActionsVisible);
                }
            }
        }

        [TestMethod]
        public void WhenSaveIsBlockedThenActionableDiagnosticIsSelectedAndStatusIsShown()
        {
            TrainPathDiagnosticRow diagnostic = new TrainPathDiagnosticRow(
                PathRouteDiagnosticSeverity.Error, PathRouteDiagnosticCode.AnchorNotOnTrack, "Node is off track.",
                2, -1, -1, "Move the node onto track.", true);
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Diagnostics = [diagnostic],
                BlockedSaveMessage = "Path cannot be saved because a node is off track.",
                BlockedSaveDiagnostic = diagnostic,
                BlockedSaveFeedbackVersion = 1,
            });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    Assert.IsTrue(viewModel.StatusMessageIsWarning);
                    Assert.AreEqual("Path cannot be saved because a node is off track.", viewModel.StatusMessage);
                    Assert.AreEqual(TrainPathToolWindowViewModel.EditorTabIndex, viewModel.SelectedTabIndex);
                    Assert.IsTrue(viewModel.IsIssuesExpanded);
                    Assert.AreEqual(PathRouteDiagnosticCode.AnchorNotOnTrack, viewModel.SelectedDiagnostic?.Code);
                }
            }
        }

        [TestMethod]
        public void WhenSuccessfulCommandResultIsAppliedThenActualMessageIsShown()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                CommandResultMessage = "Path node moved.",
                CommandResultVersion = 1,
            });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    Assert.AreEqual("Path node moved.", viewModel.StatusMessage);
                }
            }
        }

        [TestMethod]
        public void WhenFailedCommandResultIsAppliedThenActualWarningIsShown()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                CommandResultMessage = "Path node cannot be moved.",
                CommandResultIsWarning = true,
                CommandResultVersion = 1,
            });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    Assert.AreEqual("Path node cannot be moved.", viewModel.StatusMessage);
                    Assert.IsTrue(viewModel.StatusMessageIsWarning);
                }
            }
        }

        [TestMethod]
        public void WhenCommitMoveHasNoEditorResultThenNoOptimisticFeedbackIsShown()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    SetCommandAvailability(trainPathToolWindowViewModel, "canCommitMoveNode", true);

                    trainPathToolWindowViewModel.CommitMoveNodeCommand.Execute(null);

                    Assert.AreEqual(string.Empty, trainPathToolWindowViewModel.StatusMessage);
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
        public void WhenMoveModeEndsThenMoveGuidanceStatusIsCleared()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            string guidance = "Select a new track location for node 2.";
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                CanCancelMoveNode = true,
                CommandResultMessage = guidance,
                CommandResultVersion = 1,
            });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();
                    SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
                    {
                        CommandResultMessage = guidance,
                        CommandResultVersion = 1,
                    });

                    viewModel.Start();

                    Assert.AreEqual(string.Empty, viewModel.StatusMessage);
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
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    trainPathToolWindowViewModel.UserSelectPath(path);
                    trainPathToolWindowViewModel.UserSelectPath(path);
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
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    trainPathToolWindowViewModel.UserSelectPath(new TrainPathListItemViewModel("path-1", "First Path", PathValidationState.NotValidated));

                    Assert.AreEqual(string.Empty, trainPathToolWindowViewModel.StatusMessage);
                }
            }
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
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    trainPathToolWindowViewModel.UserSelectNode(new TrainPathNodeItemViewModel(new TrainPathNodeRow(2, PathNodeType.End, false, 0, -1, -1, null, "NotOnTrack", 42, 3, 1.25)));

                    Assert.AreEqual("42", trainPathToolWindowViewModel.SelectedNodeDetailRows.Single(row => row.Name == "Nearest Track Node").Value);
                    Assert.AreEqual("3 (1.25 m)", trainPathToolWindowViewModel.SelectedNodeDetailRows.Single(row => row.Name == "Nearest Track Section").Value);
                    Assert.DoesNotContain(row => row.Name == "Nearest Track Distance", trainPathToolWindowViewModel.SelectedNodeDetailRows);
                }
            }
        }

        [TestMethod]
        [DataRow(0, 0d, "0 (0 m)")]
        [DataRow(3, null, "3")]
        [DataRow(null, null, "")]
        [DataRow(null, 1.25d, "")]
        public void WhenNearestSectionDiagnosticsArePartialThenDetailsDoNotInventASection(int? section, double? distance, string expected)
        {
            TrainPathToolWindow bridge = CreateBridge(_ => { });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.UserSelectNode(new TrainPathNodeItemViewModel(new TrainPathNodeRow(0, PathNodeType.Via,
                        true, 1, -1, -1, null, null, 1, section, distance)));

                    Assert.AreEqual(expected, viewModel.SelectedNodeDetailRows.Single(row => row.Name == "Nearest Track Section").Value);
                }
            }
        }

        [TestMethod]
        public void WhenNodeIsDeselectedThenDetailPropertiesRemainWithBlankValues()
        {
            TrainPathToolWindow bridge = CreateBridge(_ => { });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    string[] names = ["Index", "Type", "Track Node", "Wait", "Next Main", "Next Siding", "Validation",
                        "Nearest Track Node", "Nearest Track Section"];
                    Assert.AreSequenceEqual(names, viewModel.SelectedNodeDetailRows.Select(row => row.Name).ToArray());
                    viewModel.UserSelectNode(new TrainPathNodeItemViewModel(new TrainPathNodeRow(2, PathNodeType.Via,
                        false, 7, 3, -1, 45, "Off track", 42, 3, 1.25)));
                    object[] originalRows = viewModel.SelectedNodeDetailRows.Cast<object>().ToArray();

                    viewModel.UserSelectNode(null);

                    Assert.AreSequenceEqual(names, viewModel.SelectedNodeDetailRows.Select(row => row.Name).ToArray());
                    Assert.AreSequenceEqual(originalRows, viewModel.SelectedNodeDetailRows.Cast<object>().ToArray());
                    Assert.IsTrue(viewModel.SelectedNodeDetailRows.All(row => string.IsNullOrEmpty(row.Value) && !row.Bold));
                    Assert.IsFalse(viewModel.CanEditSelectedNodeWaitTime);
                }
            }
        }

        [TestMethod]
        public void WhenNodeTypeContainsWaitFlagThenNodeReportsWaitPoint()
        {
            TrainPathNodeItemViewModel node = new TrainPathNodeItemViewModel(new TrainPathNodeRow(1, PathNodeType.Via | PathNodeType.Wait, true, 7, 2, 3, 45, null));

            Assert.IsTrue(node.HasWaitPoint);
            Assert.IsFalse(node.HasReversalPoint);
        }

        [TestMethod]
        public void WhenNodeTypeContainsReversalFlagThenNodeReportsReversalPoint()
        {
            TrainPathNodeItemViewModel node = new TrainPathNodeItemViewModel(new TrainPathNodeRow(1, PathNodeType.Via | PathNodeType.Reversal, true, 7, 2, 3, null, null));

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
                    trainPathToolWindowViewModel.UserSelectRouteCandidate(new TrainPathRouteCandidateItemViewModel(new TrainPathRouteCandidateRow(1, 4, 0, "candidate")));

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
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                RouteCandidates = [new TrainPathRouteCandidateRow(1, 4, 0, "candidate")],
                HasPendingAmbiguousSpanCommit = true,
            });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel trainPathToolWindowViewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    trainPathToolWindowViewModel.Start();
                    trainPathToolWindowViewModel.UserSelectRouteCandidate(trainPathToolWindowViewModel.RouteCandidates[0]);

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
        public void WhenAmbiguousDiagnosticSelectedThenMatchingRouteCandidateIsSelected()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    TrainPathRouteCandidateItemViewModel matchingCandidate = new TrainPathRouteCandidateItemViewModel(new TrainPathRouteCandidateRow(1, 4, 0, "matching"));
                    viewModel.RouteCandidates.Add(new TrainPathRouteCandidateItemViewModel(new TrainPathRouteCandidateRow(0, 1, 0, "other")));
                    viewModel.RouteCandidates.Add(matchingCandidate);

                    viewModel.UserSelectDiagnostic(new TrainPathDiagnosticItemViewModel(new TrainPathDiagnosticRow(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.AmbiguousRoute, "Several routes are available.",
                        -1, 1, 4, "Choose a route candidate.", false)));

                    Assert.AreSame(matchingCandidate, viewModel.SelectedRouteCandidate);
                    Assert.AreEqual(TrainPathToolWindowViewModel.EditorTabIndex, viewModel.SelectedTabIndex);
                }
            }
        }

        [TestMethod]
        public void WhenAmbiguousDiagnosticSelectedThenHighlightAndCandidatePreviewAreMarshaled()
        {
            int marshaledInvocations = 0;
            TrainPathToolWindow bridge = CreateBridge(action =>
            {
                marshaledInvocations++;
                action();
            });
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.RouteCandidates.Add(new TrainPathRouteCandidateItemViewModel(new TrainPathRouteCandidateRow(1, 4, 0, "matching")));

                    viewModel.UserSelectDiagnostic(new TrainPathDiagnosticItemViewModel(new TrainPathDiagnosticRow(PathRouteDiagnosticSeverity.Warning,
                        PathRouteDiagnosticCode.AmbiguousRoute, "Several routes are available.", -1, 1, 4, "Choose a route candidate.", false)));

                    Assert.AreEqual(2, marshaledInvocations);
                }
            }
        }

        private static void SetCommandAvailability(TrainPathToolWindowViewModel viewModel, string fieldName, object value)
        {
            typeof(TrainPathToolWindowViewModel).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(viewModel, value);
        }

        [TestMethod]
        [DataRow(true, true, true, PathEditorPlacementMode.StartAnchor, PassingBranchAuthoringPhase.SelectingRejoin, "candidate")]
        [DataRow(false, true, true, PathEditorPlacementMode.StartAnchor, PassingBranchAuthoringPhase.SelectingCandidate, "candidate")]
        [DataRow(false, false, true, PathEditorPlacementMode.StartAnchor, PassingBranchAuthoringPhase.SelectingRejoin, "rejoin")]
        [DataRow(false, false, true, PathEditorPlacementMode.StartAnchor, PassingBranchAuthoringPhase.Idle, "building")]
        [DataRow(false, false, false, PathEditorPlacementMode.StartAnchor, PassingBranchAuthoringPhase.Idle, "placement")]
        [DataRow(false, false, false, PathEditorPlacementMode.None, PassingBranchAuthoringPhase.Idle, "idle")]
        public void WhenWorkflowSnapshotIsAppliedThenOnlyHighestPriorityPanelIsVisible(bool mainCandidate, bool passingCandidate, bool building, object placement, object passing, string expected)
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                HasPendingAmbiguousSpanCommit = mainCandidate,
                HasPendingPassingBranchCandidate = passingCandidate,
                IsBuildingRoute = building,
                PlacementMode = (PathEditorPlacementMode)placement,
                PassingBranchPhase = (PassingBranchAuthoringPhase)passing,
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    Assert.AreSequenceEqual(new[] { expected }, new[]
                    {
                    ("candidate", viewModel.IsCandidatePanelVisible), ("rejoin", viewModel.IsRejoinPanelVisible),
                    ("building", viewModel.IsBuildingPanelVisible), ("placement", viewModel.IsPlacementPanelVisible),
                    ("idle", viewModel.IsIdlePanelVisible),
                }.Where(panel => panel.Item2).Select(panel => panel.Item1).ToArray());
                }
            }
        }

        private static readonly int[] expected01 = new[] { 0, 1 };

        [TestMethod]
        public void WhenAmbiguousIssueIsInspectedThenOnlyItsSpanCandidatesAreVisible()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Diagnostics = [new TrainPathDiagnosticRow(PathRouteDiagnosticSeverity.Error, PathRouteDiagnosticCode.AmbiguousRoute,
                    "Choose route.", -1, 2, 3, "Choose candidate.", false)],
                RouteCandidates = [new TrainPathRouteCandidateRow(0, 1, 0, "other"),
                    new TrainPathRouteCandidateRow(2, 3, 0, "first"), new TrainPathRouteCandidateRow(2, 3, 1, "second")],
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    viewModel.UserSelectDiagnostic(viewModel.Diagnostics[0]);

                    Assert.AreSequenceEqual(expected01, viewModel.VisibleRouteCandidates.Select(candidate => candidate.CandidateIndex).ToArray());
                    Assert.IsTrue(viewModel.VisibleRouteCandidates.All(candidate => candidate.FromNodeIndex == 2 && candidate.ToNodeIndex == 3));
                }
            }
        }

        [TestMethod]
        public void WhenSnapshotPreviewChangesThenSelectionAndScopeSynchronizeWithoutPreviewCommands()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            TrainPathSnapshot snapshot = TrainPathSnapshot.Empty with
            {
                RouteCandidates = [new TrainPathRouteCandidateRow(0, 1, 0, "first"), new TrainPathRouteCandidateRow(2, 3, 1, "second")],
                PreviewedRouteCandidateFromNodeIndex = 0,
                PreviewedRouteCandidateIndex = 0,
            };
            SetBridgeSnapshot(bridge, snapshot);
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();
                    SetBridgeSnapshot(bridge, snapshot with { PreviewedRouteCandidateFromNodeIndex = 2, PreviewedRouteCandidateIndex = 1 });

                    viewModel.Start();
                    viewModel.Start();
                    viewModel.UserSelectRouteCandidate(viewModel.SelectedRouteCandidate);

                    Assert.AreSame(viewModel.RouteCandidates[1], viewModel.SelectedRouteCandidate);
                    Assert.AreSequenceEqual(new[] { viewModel.RouteCandidates[1] }, viewModel.VisibleRouteCandidates.ToArray());
                    Assert.AreEqual(0, invocations);
                }
            }
        }

        [TestMethod]
        public void WhenSameCandidateIsSelectedTwiceThenOnlyOnePreviewIsRequested()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with { RouteCandidates = [new TrainPathRouteCandidateRow(0, 1, 0, "candidate")] });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    viewModel.UserSelectRouteCandidate(viewModel.RouteCandidates[0]);
                    viewModel.UserSelectRouteCandidate(viewModel.RouteCandidates[0]);

                    Assert.AreEqual(1, invocations);
                }
            }
        }

        [TestMethod]
        [DataRow(PathNodeType.Via, false, false, false, false, false, PassingBranchAuthoringPhase.Idle, false)]
        [DataRow(PathNodeType.Via, true, false, false, false, false, PassingBranchAuthoringPhase.Idle, true)]
        [DataRow(PathNodeType.Wait | PathNodeType.Reversal, false, false, false, false, false, PassingBranchAuthoringPhase.Idle, false)]
        [DataRow(PathNodeType.Start, false, false, false, false, false, PassingBranchAuthoringPhase.Idle, false)]
        [DataRow(PathNodeType.End | PathNodeType.Wait, false, false, false, false, false, PassingBranchAuthoringPhase.Idle, false)]
        [DataRow(PathNodeType.Junction, false, false, false, false, false, PassingBranchAuthoringPhase.Idle, false)]
        [DataRow(PathNodeType.Wait, true, true, false, false, false, PassingBranchAuthoringPhase.Idle, true)]
        [DataRow(PathNodeType.Wait, true, false, true, false, false, PassingBranchAuthoringPhase.Idle, false)]
        [DataRow(PathNodeType.Wait, true, false, false, true, false, PassingBranchAuthoringPhase.Idle, false)]
        [DataRow(PathNodeType.Wait, true, false, false, false, true, PassingBranchAuthoringPhase.SelectingCandidate, false)]
        [DataRow(PathNodeType.Wait, true, false, false, false, false, PassingBranchAuthoringPhase.SelectingRejoin, false)]
        public void WhenWaitCapabilityIsPublishedThenWaitEligibilityUsesItWithoutInferringNodeFlags(PathNodeType type, bool canSet, bool repair,
            bool interaction, bool mainCandidate, bool passingCandidate, object passing, bool expected)
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Nodes = [new TrainPathNodeRow(0, type, true, 1, -1, -1, null, null)],
                SelectedNodeIndex = 0,
                CanSetSelectedWaitPoint = canSet,
                IsRepairMode = repair,
                CanCancelPathInteraction = interaction,
                HasPendingAmbiguousSpanCommit = mainCandidate,
                HasPendingPassingBranchCandidate = passingCandidate,
                PassingBranchPhase = (PassingBranchAuthoringPhase)passing,
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    Assert.AreEqual(expected, viewModel.CanEditSelectedNodeWaitTime);
                }
            }
        }

        [TestMethod]
        [DataRow(PathNodeType.Via, false)]
        [DataRow(PathNodeType.Wait, false)]
        [DataRow(PathNodeType.Start | PathNodeType.Wait, true)]
        public void WhenWaitSetIsDisabledThenExplicitClearRemainsAvailableForPublishedWaitData(PathNodeType type, bool repair)
        {
            TrainPathToolWindow bridge = CreateBridge(_ => { });
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Nodes = [new TrainPathNodeRow(0, type, true, 1, -1, -1, 30, null)],
                SelectedNodeIndex = 0,
                CanClearSelectedWaitPoint = true,
                IsRepairMode = repair,
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    Assert.AreEqual((false, true), (viewModel.CanEditSelectedNodeWaitTime, viewModel.ClearWaitPointCommand.CanExecute(null)));
                }
            }
        }

        [TestMethod]
        public void WhenPositiveWaitEditIsNotAuthorizedThenValueIsResetWithWarningAndNoCommand()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Nodes = [new TrainPathNodeRow(0, PathNodeType.Via, true, 1, -1, -1, 30, null)],
                SelectedNodeIndex = 0,
                CanClearSelectedWaitPoint = true,
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    viewModel.SelectedNodeWaitTime = 60;

                    Assert.AreEqual(((int?)30, 0, true, true), (viewModel.SelectedNodeWaitTime, invocations, viewModel.StatusMessageIsWarning,
                        !string.IsNullOrWhiteSpace(viewModel.StatusMessage)));
                }
            }
        }

        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public async Task WhenQueuedWaitEditUsesStaleCapabilityThenRejectedResultRestoresAuthoritativeWaitWithoutAddingHistory()
        {
            TrackWorld world = TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld();
            PathModel source = new PathModel
            {
                Id = "stale-wait-edit",
                Name = "Stale Wait Edit",
                PathNodes = ImmutableArray.Create(
                    new PathNode(new WorldLocation(new Tile(0, 0), Vector3.Zero))
                    {
                        NodeType = PathNodeType.Start,
                        NodeIndex = 1,
                        NextMainNode = 1,
                        NextSidingNode = -1,
                    },
                    new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(50, 0, 0)))
                    {
                        NodeType = PathNodeType.Via,
                        NodeIndex = 1,
                        NextMainNode = 2,
                        NextSidingNode = -1,
                    },
                    new PathNode(new WorldLocation(new Tile(0, 0), new Vector3(100, 0, 0)))
                    {
                        NodeType = PathNodeType.End,
                        NodeIndex = 1,
                        NextMainNode = -1,
                        NextSidingNode = -1,
                    }),
            };
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, CancellationToken.None).ConfigureAwait(false));
                editor.SelectAuthoredNode(1);
                Queue<Action> gameCommands = new Queue<Action>();
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => editor, () => null, gameCommands.Enqueue,
                    () => { }, () => { }, _ => { }, () => { }, () => { })
                { Active = true };
                bridge.RefreshSnapshot();
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                    {
                        viewModel.Start();
                        TrainPathSnapshot initial = bridge.CaptureTrainPathSnapshot();
                        Assert.AreEqual((1, true, true, (int?)null, 0), (viewModel.SelectedNode.Index, initial.CanSetSelectedWaitPoint, viewModel.CanSetSelectedWaitPoint, viewModel.SelectedNodeWaitTime, gameCommands.Count));

                        Assert.IsTrue(editor.SetReversalPoint(1).Success);
                        PathModel authoritative = editor.TryCaptureCurrentPathModel();
                        (bool CanUndo, bool CanRedo, bool HasUnsavedChanges) history = (editor.CanUndo, editor.CanRedo, editor.HasUnsavedChanges);

                        viewModel.SelectedNodeWaitTime = 60;

                        Assert.AreEqual((1, (int?)60, initial.CommandResultVersion),
                            (gameCommands.Count, viewModel.SelectedNodeWaitTime, bridge.CaptureTrainPathSnapshot().CommandResultVersion));

                        gameCommands.Dequeue()();
                        bridge.RefreshSnapshot();
                        viewModel.Start();

                        TrainPathSnapshot completed = bridge.CaptureTrainPathSnapshot();
                        Assert.AreSame(authoritative, editor.TryCaptureCurrentPathModel());
                        Assert.AreEqual(history, (editor.CanUndo, editor.CanRedo, editor.HasUnsavedChanges));
                        Assert.AreEqual((initial.CommandResultVersion + 1, true, true, completed.CommandResultMessage), (completed.CommandResultVersion, completed.CommandResultIsWarning, viewModel.StatusMessageIsWarning, viewModel.StatusMessage));
                        Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
                        Assert.AreEqual(((int?)null, (int?)null, false, 0), (viewModel.SelectedNodeWaitTime, viewModel.SelectedNode.WaitTime, viewModel.CanSetSelectedWaitPoint, gameCommands.Count));

                        Assert.IsTrue(editor.Undo());
                        Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                        Assert.AreEqual((false, true), (editor.CanUndo, editor.CanRedo));
                    }
                }
            }
        }

        [TestMethod]
        public void WhenPayloadOnlyWaitIsClearedThenTheProductionClearCommandIsMarshaled()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Nodes = [new TrainPathNodeRow(0, PathNodeType.Via, false, 1, -1, -1, 30, null)],
                SelectedNodeIndex = 0,
                IsRepairMode = true,
                CanClearSelectedWaitPoint = true,
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    viewModel.ClearWaitPointCommand.Execute(null);

                    Assert.AreEqual(1, invocations);
                }
            }
        }

        [TestMethod]
        [DataRow(60)]
        [DataRow(0)]
        [DataRow(null)]
        public void WhenWaitEditIsAuthorizedThenTheProductionSetOrClearCommandIsMarshaled(int? value)
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Nodes = [new TrainPathNodeRow(0, PathNodeType.Via, true, 1, -1, -1, 30, null)],
                SelectedNodeIndex = 0,
                CanSetSelectedWaitPoint = true,
                CanClearSelectedWaitPoint = true,
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    viewModel.SelectedNodeWaitTime = value;

                    Assert.AreEqual(1, invocations);
                }
            }
        }

        [TestMethod]
        [DataRow(PathNodeType.Via, true, false, true)]
        [DataRow(PathNodeType.Via, false, true, false)]
        [DataRow(PathNodeType.Reversal, true, false, false)]
        [DataRow(PathNodeType.Reversal, false, true, true)]
        public void WhenReversalIsToggledThenEligibilityUsesTheApplicableAuthoritativeCapability(PathNodeType type, bool canSet, bool canClear, bool expected)
        {
            TrainPathToolWindow bridge = CreateBridge(_ => { });
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Nodes = [new TrainPathNodeRow(0, type, false, 1, -1, -1, null, null)],
                SelectedNodeIndex = 0,
                IsRepairMode = true,
                CanSetSelectedReversalPoint = canSet,
                CanClearSelectedReversalPoint = canClear,
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    Assert.AreEqual(expected, viewModel.ToggleReversalPointCommand.CanExecute(null));
                }
            }
        }

        [TestMethod]
        public void WhenSelectionIsAheadOfSnapshotThenEventCapabilitiesAndCommandsDoNotTargetTheNewNode()
        {
            int invocations = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => invocations++);
            TrainPathSnapshot snapshot = TrainPathSnapshot.Empty with
            {
                Nodes = [new TrainPathNodeRow(0, PathNodeType.Wait, true, 1, 1, -1, 30, null),
                    new TrainPathNodeRow(1, PathNodeType.Via, true, 1, -1, -1, null, null)],
                SelectedNodeIndex = 0,
                CanSetSelectedWaitPoint = true,
                CanClearSelectedWaitPoint = true,
                CanSetSelectedReversalPoint = true,
                CanClearSelectedReversalPoint = true,
            };
            SetBridgeSnapshot(bridge, snapshot);
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();
                    viewModel.UserSelectNode(viewModel.Nodes[1]);
                    viewModel.Start();

                    viewModel.SelectedNodeWaitTime = 60;
                    viewModel.ClearWaitPointCommand.Execute(null);
                    viewModel.ToggleReversalPointCommand.Execute(null);

                    Assert.AreEqual((false, false, false, false, 1), (viewModel.CanSetSelectedWaitPoint, viewModel.CanClearSelectedWaitPoint,
                        viewModel.CanSetSelectedReversalPoint, viewModel.CanClearSelectedReversalPoint, invocations));
                }
            }
        }

        [TestMethod]
        public void WhenBlockedSaveHasANonNodeEventDiagnosticThenIssuesFocusDoesNotInventANodeTarget()
        {
            TrainPathDiagnosticRow diagnostic = new TrainPathDiagnosticRow(PathRouteDiagnosticSeverity.Error,
                PathRouteDiagnosticCode.MultipleReversalsInContinuousVector, "Unsupported reversal traversal.", -1, -1, -1,
                "Inspect reversal points.", false);
            TrainPathToolWindow bridge = CreateBridge(_ => { });
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Nodes = [new TrainPathNodeRow(0, PathNodeType.Via, true, 1, -1, -1, null, null)],
                SelectedNodeIndex = 0,
                Diagnostics = [diagnostic],
                BlockedSaveMessage = diagnostic.Message,
                BlockedSaveDiagnostic = diagnostic,
                BlockedSaveFeedbackVersion = 1,
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    Assert.AreEqual((true, true, -1), (viewModel.IsIssuesExpanded, viewModel.SelectedNode == null, viewModel.SelectedDiagnostic?.NodeIndex));
                }
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void WhenCandidatePanelIsCanceledWithoutAnEditorThenNoOptimisticCommandResultIsPublished(bool transaction)
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                RouteCandidates = [new TrainPathRouteCandidateRow(0, 1, 0, "candidate")],
                PreviewedRouteCandidateFromNodeIndex = 0,
                PreviewedRouteCandidateIndex = 0,
                HasPendingAmbiguousSpanCommit = transaction,
                CanCancelPathInteraction = transaction,
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    viewModel.CancelPathInteractionCommand.Execute(null);
                    bridge.RefreshSnapshot();
                    viewModel.Start();

                    Assert.AreEqual(0, bridge.CaptureTrainPathSnapshot().CommandResultVersion);
                    Assert.IsFalse(viewModel.IsCandidatePanelVisible);
                }
            }
        }

        [TestMethod]
        [DataRow(PathEditorPlacementMode.StartAnchor, false, false, false, PassingBranchAuthoringPhase.Idle)]
        [DataRow(PathEditorPlacementMode.None, true, false, false, PassingBranchAuthoringPhase.Idle)]
        [DataRow(PathEditorPlacementMode.None, false, true, false, PassingBranchAuthoringPhase.Idle)]
        [DataRow(PathEditorPlacementMode.None, false, false, true, PassingBranchAuthoringPhase.SelectingCandidate)]
        [DataRow(PathEditorPlacementMode.None, false, false, false, PassingBranchAuthoringPhase.SelectingRejoin)]
        public void WhenWorkflowStartsThenEditorRevealsOnceWithoutRefreshStealingPaths(object placement,
            bool building, bool mainCandidate, bool passingCandidate, object passing)
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();
                    TrainPathSnapshot snapshot = TrainPathSnapshot.Empty with
                    {
                        PlacementMode = (PathEditorPlacementMode)placement,
                        IsBuildingRoute = building,
                        HasPendingAmbiguousSpanCommit = mainCandidate,
                        HasPendingPassingBranchCandidate = passingCandidate,
                        PassingBranchPhase = (PassingBranchAuthoringPhase)passing,
                    };
                    SetBridgeSnapshot(bridge, snapshot);
                    viewModel.Start();
                    Assert.AreEqual(TrainPathToolWindowViewModel.EditorTabIndex, viewModel.SelectedTabIndex);
                    viewModel.SelectedTabIndex = TrainPathToolWindowViewModel.PathsTabIndex;

                    viewModel.Start();
                    viewModel.Start();

                    Assert.AreEqual(TrainPathToolWindowViewModel.PathsTabIndex, viewModel.SelectedTabIndex);
                }
            }
        }

        [TestMethod]
        public void WhenOrdinaryPathIsSelectedThenPathsNavigationIsRetained()
        {
            TrainPathToolWindow bridge = CreateBridge(action => action());
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();

                    viewModel.UserSelectPath(new TrainPathListItemViewModel("path", "Path", PathValidationState.NotValidated));
                    SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with { SelectedPathId = "path" });
                    viewModel.Start();

                    Assert.AreEqual(TrainPathToolWindowViewModel.PathsTabIndex, viewModel.SelectedTabIndex);
                }
            }
        }

        [TestMethod]
        public void WhenPendingDecisionHasMultipleSpansThenEverySpanCanBePreviewedWithoutStaleRefreshUndoingSelection()
        {
            int previews = 0;
            TrainPathToolWindow bridge = CreateBridge(_ => previews++);
            TrainPathSnapshot snapshot = TrainPathSnapshot.Empty with
            {
                HasPendingAmbiguousSpanCommit = true,
                RouteCandidates = [new TrainPathRouteCandidateRow(0, 1, 0, "first span"),
                    new TrainPathRouteCandidateRow(0, 1, 1, "first alternative"),
                    new TrainPathRouteCandidateRow(1, 2, 0, "second span"),
                    new TrainPathRouteCandidateRow(1, 2, 1, "second alternative")],
                PreviewedRouteCandidateFromNodeIndex = 0,
                PreviewedRouteCandidateIndex = 0,
            };
            SetBridgeSnapshot(bridge, snapshot);
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();
                    Assert.HasCount(2, viewModel.CandidateSpans);

                    viewModel.UserSelectCandidateSpan(viewModel.CandidateSpans[1]);
                    viewModel.UserSelectRouteCandidate(viewModel.VisibleRouteCandidates[1]);
                    viewModel.Start(); // The game thread has not yet published the requested preview.

                    Assert.AreEqual(1, viewModel.SelectedCandidateSpan.FromNodeIndex);
                    Assert.AreEqual(1, viewModel.SelectedRouteCandidate.CandidateIndex);
                    Assert.IsTrue(viewModel.VisibleRouteCandidates.All(item => item.FromNodeIndex == 1));
                    Assert.IsTrue(viewModel.CanAcceptRouteCandidate);
                    Assert.AreEqual(2, previews);

                    SetBridgeSnapshot(bridge, snapshot with { PreviewedRouteCandidateFromNodeIndex = 1, PreviewedRouteCandidateIndex = 1 });
                    viewModel.Start();
                    viewModel.UserSelectCandidateSpan(viewModel.CandidateSpans[0]);

                    Assert.AreEqual(0, viewModel.SelectedCandidateSpan.FromNodeIndex);
                    Assert.AreEqual(3, previews);
                }
            }
        }

        [TestMethod]
        public void WhenIssueInspectionIsCanceledThenTheSameIssueCanBeSelectedAgain()
        {
            TrainPathToolWindow bridge = CreateBridge(_ => { });
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Diagnostics = [new TrainPathDiagnosticRow(PathRouteDiagnosticSeverity.Warning, PathRouteDiagnosticCode.AmbiguousRoute,
                    "Choose route.", -1, 0, 1, "Choose a candidate.", false)],
                RouteCandidates = [new TrainPathRouteCandidateRow(0, 1, 0, "candidate")],
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();
                    TrainPathDiagnosticItemViewModel issue = viewModel.Diagnostics[0];
                    viewModel.UserSelectDiagnostic(issue);
                    viewModel.CancelPathInteractionCommand.Execute(null);

                    Assert.IsNull(viewModel.SelectedDiagnostic);
                    Assert.IsFalse(viewModel.IsCandidatePanelVisible);

                    viewModel.UserSelectDiagnostic(issue);

                    Assert.AreSame(issue, viewModel.SelectedDiagnostic);
                    Assert.IsTrue(viewModel.IsCandidatePanelVisible);
                    Assert.IsTrue(viewModel.CanAcceptRouteCandidate);
                }
            }
        }

        [TestMethod]
        public void WhenPathSelectionIsPendingThenNeitherOldNorRequestedRowIsAnActionTarget()
        {
            TrainPathToolWindow bridge = CreateBridge(_ => { });
            SetBridgeSnapshot(bridge, TrainPathSnapshot.Empty with
            {
                Paths = [new TrainPathListRow("active", "Active", PathValidationState.NotValidated),
                    new TrainPathListRow("other", "Other", PathValidationState.NotValidated)],
                SelectedPathId = "active",
            });
            using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                {
                    viewModel.Start();
                    TrainPathListItemViewModel active = viewModel.Paths[0];
                    TrainPathListItemViewModel other = viewModel.Paths[1];
                    Assert.IsTrue(viewModel.IsActivePathActionTarget(active));
                    Assert.IsFalse(viewModel.IsActivePathActionTarget(other));

                    viewModel.UserSelectPath(other);

                    Assert.IsFalse(viewModel.IsActivePathActionTarget(active));
                    Assert.IsFalse(viewModel.IsActivePathActionTarget(other));
                }
            }
        }

        private static void SetBridgeSnapshot(TrainPathToolWindow bridge, TrainPathSnapshot snapshot)
        {
            typeof(TrainPathToolWindow).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bridge, snapshot);
        }

        private static void AssertValidationFailure(Exception exception)
        {
            TestToolingContext context = new TestToolingContext(() => Task.FromException<ImmutableArray<PathModelHeader>>(exception));
            TrainPathToolWindow bridge = CreateBridge(action => action(), context);
            using (ToolWindowRefreshScheduler refreshScheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
            {
                using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, refreshScheduler))
                {
                    viewModel.Start();

                    viewModel.ValidateAllPathsCommand.Execute(null);

                    Assert.AreEqual($"Path validation failed: {exception.Message}", viewModel.StatusMessage);
                    Assert.IsTrue(viewModel.StatusMessageIsWarning);
                }
            }
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

        private sealed class TestToolingContext : ITrainPathToolingContext
        {
            private readonly Func<Task<ImmutableArray<PathModelHeader>>> validateAllPaths;

            public TestToolingContext(Func<Task<ImmutableArray<PathModelHeader>>> validateAllPaths)
            {
                this.validateAllPaths = validateAllPaths;
            }

            public bool UseMetricUnits => true;

            public TrackWorld TrackWorld => null;

            public Task<ImmutableArray<PathModelHeader>> GetPaths() => Task.FromResult(ImmutableArray<PathModelHeader>.Empty);

            public Task<ImmutableArray<PathModelHeader>> ValidateAllPaths() => validateAllPaths();
        }
    }
}
