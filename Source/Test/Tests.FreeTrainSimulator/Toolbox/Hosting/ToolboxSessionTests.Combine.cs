using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Runtime.Track;
using Tests.FreeTrainSimulator.Simulator;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        private static readonly float[] expectedArray20f80f80f20f = new[] { 20f, 80f, 80f, 20f };
        private static readonly float[] expectedArray80f20f20f80f = new[] { 80f, 20f, 20f, 80f };

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(0, false)]
        [DataRow(1, false)]
        [DataRow(0, true)]
        [DataRow(1, true)]
        public async Task WhenAnOrientationIsAcceptedThenPreviewIsNonMutatingAndTheChosenRouteSurvivesNativeSave(int candidateIndex, bool dirty)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel first, PathModel second, PathModel third) = await CreateAmbiguousCombineModelsAsync().ConfigureAwait(false);
            byte[] firstBytes = await File.ReadAllBytesAsync(CompositionFile(first), TestContext.CancellationToken).ConfigureAwait(false);
            byte[] secondBytes = await File.ReadAllBytesAsync(CompositionFile(second), TestContext.CancellationToken).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [first, second, third], commands).ConfigureAwait(false);
                PathModel source = null;
                PathModel[] undo = [];
                if (dirty)
                {
                    Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(third)).ConfigureAwait(false));
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        PathEditor editor = scope.Session.ExistingPathEditor;
                        Assert.IsTrue(editor.SetMetadataCommand("Retained outgoing edits", third.Start, third.End, third.PlayerPath).Success);
                        source = editor.TryCaptureCurrentPathModel();
                        undo = SavePreflightHistory(editor, "undoHistory");
                    }).ConfigureAwait(false);
                }
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(first.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(second.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.CombineSelectedPathsAsync(true)).ConfigureAwait(false);
                TrainPathCombinationRow row = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                    Assert.AreEqual(dirty, editor.HasUnsavedChanges);
                    Assert.IsTrue(editor.IsChoosingPathCombination);
                    Assert.IsFalse(editor.CanCombinePaths);
                    Assert.IsFalse(editor.CanUndo);
                    Assert.IsFalse(editor.CanPlaceStartAnchor);
                    Assert.IsFalse(editor.ValidateCurrentPathForPersistence().PersistenceAllowed);
                    Assert.IsFalse(editor.SetMetadataCommand("Must not apply", "", "", false).Success);
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    Assert.HasCount(2, snapshot.CombinationCandidates);
                    Assert.AreEqual(-1, snapshot.PreviewedCombinationIndex);
                    Assert.IsFalse(snapshot.IsCombiningPaths);
                    Assert.IsTrue(snapshot.CanCancelPathInteraction);
                    Assert.IsNull(typeof(PathEditorBase).GetProperty("PreviewPathModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
                    bridge.RefreshSnapshot();
                    Assert.AreSame(snapshot, bridge.CaptureTrainPathSnapshot());
                    row = snapshot.CombinationCandidates[candidateIndex];
                }).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SelectPathCombinationAsync(row.RequestVersion, candidateIndex, PathCombinationAction.Accept)).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    if (dirty)
                        bridge.SelectPathCombination(row, PathCombinationAction.Preview);
                    else
                        bridge.CycleRouteCandidate(candidateIndex == 0 ? 1 : -1);
                }).ConfigureAwait(false);
                await Task.WhenAll(commands).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                PathModel preview = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    preview = (PathModel)typeof(PathEditorBase).GetProperty("PreviewPathModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                    Assert.IsNotNull(preview);
                    Assert.AreSequenceEqual(candidateIndex == 0 ? expectedArray20f80f80f20f : expectedArray80f20f20f80f, preview.PathNodes.Select(node => node.Location.Location.X).ToArray());
                    Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                    Assert.AreEqual(dirty, editor.HasUnsavedChanges);
                    Assert.AreSequenceEqual(undo, SavePreflightHistory(editor, "undoHistory"));
                    Assert.IsEmpty(SavePreflightHistory(editor, "redoHistory"));
                    Assert.IsEmpty((Dictionary<string, PathModel>)typeof(TrainPathToolWindow).GetField("transientPaths", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bridge));
                    editor.ClearRouteCandidatePreview();
                    Assert.AreSame(preview, typeof(PathEditorBase).GetProperty("PreviewPathModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
                    if (dirty)
                        bridge.SelectPathCombination(row, PathCombinationAction.Accept);
                    else
                        bridge.AcceptPreviewedRouteCandidate();
                }).ConfigureAwait(false);
                await Task.WhenAll(commands).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                TrainPathSaveRequest saveRequest = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    Assert.IsTrue(editor.IsNewPath);
                    Assert.IsTrue(editor.HasUnsavedChanges);
                    Assert.IsFalse(editor.EditMode);
                    Assert.AreEqual(ToolboxContentMode.ViewPath, content.ContentMode);
                    Assert.IsFalse(editor.CanCancelPathInteraction);
                    Assert.IsFalse(editor.CanUndo);
                    Assert.AreSequenceEqual(preview.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                    Assert.IsNull(typeof(PathEditorBase).GetProperty("PreviewPathModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
                    bridge.RefreshSnapshot();
                    Assert.IsEmpty(bridge.CaptureTrainPathSnapshot().CombinationCandidates);
                    Assert.AreEqual(dirty, bridge.CaptureTrainPathSnapshot().Paths.Single(path => path.Id == third.Id).HasUnsavedChanges);
                    Assert.AreEqual(2, bridge.CaptureTrainPathSnapshot().Comparisons.Count(comparison => comparison.IsSelected));
                    TrainPathSaveState state = scope.Session.CaptureTrainPathSaveState();
                    saveRequest = new TrainPathSaveRequest(state.PathDetails with { Id = "ChosenOrientation" + Guid.NewGuid().ToString("N"), Name = "Chosen orientation" }, state.SourcePathId, false);
                }).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SubmitTrainPathSaveAsync(saveRequest)).ConfigureAwait(false);
                PathModel saved = await fixture.Route.PathModelUncached(saveRequest.PathDetails.Id, TestContext.CancellationToken).ConfigureAwait(false);
                Assert.AreSequenceEqual(PathRouteResolver.Resolve(preview, runtime.TrackWorld, TestContext.CancellationToken).MainRoute.Spans.SelectMany(span => span.Candidates.Single().PhysicalTraversals).ToArray(),
                    PathRouteResolver.Resolve(saved, runtime.TrackWorld, TestContext.CancellationToken).MainRoute.Spans.SelectMany(span => span.Candidates.Single().PhysicalTraversals).ToArray());
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(null)).ConfigureAwait(false));
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));
                await scope.Host.OnOwnerAsync(() =>
                {
                    Assert.AreSequenceEqual(saved.PathNodes, scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel().PathNodes);
                    Assert.IsFalse(scope.Session.ExistingPathEditor.HasUnsavedChanges);
                    Assert.IsFalse(scope.Session.ExistingPathEditor.EditMode);
                }).ConfigureAwait(false);
                Assert.AreSequenceEqual(firstBytes, await File.ReadAllBytesAsync(CompositionFile(first), TestContext.CancellationToken).ConfigureAwait(false));
                Assert.AreSequenceEqual(secondBytes, await File.ReadAllBytesAsync(CompositionFile(second), TestContext.CancellationToken).ConfigureAwait(false));
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow("cancel")]
        [DataRow("keyboard-cancel")]
        [DataRow("direct-cancel")]
        [DataRow("uncheck")]
        [DataRow("selection-aba")]
        [DataRow("reload")]
        [DataRow("direct-reload")]
        [DataRow("new-path")]
        [DataRow("supersede")]
        [DataRow("stop")]
        public async Task WhenAnOrientationDecisionLosesItsOwnerThenQueuedChoicesCannotOverwriteTheCurrentState(string transition)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel first, PathModel second, PathModel third) = await CreateAmbiguousCombineModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [first, second, third], commands).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(third)).ConfigureAwait(false));
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(first.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(second.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.CombineSelectedPathsAsync(true)).ConfigureAwait(false);
                TrainPathCombinationRow stale = null;
                TrainPathSnapshot displayed = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    displayed = bridge.CaptureTrainPathSnapshot();
                    stale = displayed.CombinationCandidates[0];
                }).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SelectPathCombinationAsync(stale.RequestVersion, stale.CandidateIndex, PathCombinationAction.Preview)).ConfigureAwait(false);
                switch (transition)
                {
                    case "cancel":
                    case "supersede":
                        await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.CombineSelectedPathsAsync(false)).ConfigureAwait(false);
                        if (transition == "supersede")
                            await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.CombineSelectedPathsAsync(true)).ConfigureAwait(false);
                        break;
                    case "keyboard-cancel":
                        await scope.Host.OnOwnerAsync(bridge.CancelPathInteraction).ConfigureAwait(false);
                        await Task.WhenAll(commands).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        break;
                    case "direct-cancel":
                        await scope.Host.OnOwnerAsync(() => Assert.IsTrue(scope.Session.ExistingPathEditor.CancelPathInteractionCommand().Success)).ConfigureAwait(false);
                        break;
                    case "uncheck":
                    case "selection-aba":
                        await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(second.Id, false)).ConfigureAwait(false);
                        if (transition == "selection-aba")
                            await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(second.Id, true)).ConfigureAwait(false);
                        break;
                    case "reload":
                        Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(third)).ConfigureAwait(false));
                        break;
                    case "direct-reload":
                        Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.ExistingPathEditor.InitializePathAsync(third, TestContext.CancellationToken)).ConfigureAwait(false));
                        break;
                    case "new-path":
                        await scope.Game.InvokeOnGameThreadAsync(scope.Session.EditPathAsync).ConfigureAwait(false);
                        break;
                    case "stop":
                        await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).ConfigureAwait(false);
                        break;
                }
                PathModel expected = null;
                long version = 0;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    expected = editor.TryCaptureCurrentPathModel();
                    bridge.RefreshSnapshot();
                    version = bridge.CaptureTrainPathSnapshot().CombineRequestVersion;
                    if (transition != "stop")
                    {
                        bridge.SelectPathCombination(stale, PathCombinationAction.Preview);
                        bridge.SelectPathCombination(stale, PathCombinationAction.Accept);
                        bridge.SelectPathCombination(stale, PathCombinationAction.Cancel);
                        bridge.ExecuteDisplayedPathCommand(displayed, current => current.CancelPathInteraction());
                    }
                }).ConfigureAwait(false);
                await Task.WhenAll(commands).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    Assert.AreSame(expected, editor.TryCaptureCurrentPathModel());
                    Assert.IsNull(typeof(PathEditorBase).GetProperty("PreviewPathModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    Assert.AreEqual(version, snapshot.CombineRequestVersion);
                    Assert.HasCount(transition == "supersede" ? 2 : 0, snapshot.CombinationCandidates);
                    Assert.AreEqual(-1, snapshot.PreviewedCombinationIndex);
                    Assert.IsEmpty((Dictionary<string, PathModel>)typeof(TrainPathToolWindow).GetField("transientPaths", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bridge));
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenTheOrientationPickerIsUsedThenSelectionPreviewsBeforeTheVisibleAcceptOrCancelAction(bool accept)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel first, PathModel second, PathModel third) = await CreateAmbiguousCombineModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [first, second, third], commands).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(null)).ConfigureAwait(false));
                TrainPathSnapshot beforeChoices = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    beforeChoices = bridge.CaptureTrainPathSnapshot();
                }).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(first.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(second.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.CombineSelectedPathsAsync(true)).ConfigureAwait(false);
                ToolWindowRefreshScheduler scheduler = null;
                TrainPathToolWindowViewModel viewModel = null;
                TrainPathToolView view = null;
                try
                {
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        bridge.RefreshSnapshot();
                        scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
                        viewModel = new TrainPathToolWindowViewModel(bridge, scheduler);
                        viewModel.Start();
                        view = new TrainPathToolView { DataContext = viewModel };
                        view.Measure(new Size(520, 700));
                        view.Arrange(new Rect(0, 0, 520, 700));
                        view.UpdateLayout();
                        Assert.AreEqual(TrainPathToolWindowViewModel.EditorTabIndex, viewModel.SelectedTabIndex);
                        Assert.IsTrue(viewModel.IsCombinationPanelVisible);
                        Assert.IsFalse(viewModel.IsIdlePanelVisible);
                        Assert.IsFalse(viewModel.IsCandidatePanelVisible);
                        ListView choices = (ListView)view.FindName("CombinationCandidatesList");
                        Assert.HasCount(2, choices.Items);
                        Button acceptButton = (Button)view.FindName("AcceptPathCombinationButton");
                        Assert.AreSame(viewModel.AcceptPathCombinationCommand, acceptButton.Command);
                        Assert.IsFalse(acceptButton.Command.CanExecute(null));
                        Assert.Contains("original direction", viewModel.CombinationCandidates[0].Description);
                        Assert.Contains("reversed", viewModel.CombinationCandidates[1].Description);
                        bridge.ExecuteDisplayedPathCommand(beforeChoices, current => current.CancelPathInteraction());
                        choices.SelectedIndex = 1;
                        Assert.AreEqual(1, viewModel.SelectedCombinationCandidate.CandidateIndex);
                        Assert.IsFalse(acceptButton.Command.CanExecute(null), "The selected row must be acknowledged by its preview owner first.");
                    }).ConfigureAwait(false);
                    await Task.WhenAll(commands).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        bridge.RefreshSnapshot();
                        viewModel.Start();
                        Assert.IsNull(scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel());
                        Assert.IsTrue(viewModel.AcceptPathCombinationCommand.CanExecute(null));
                        int before = commands.Count;
                        viewModel.RefreshLocalization();
                        Assert.HasCount(before, commands);
                        Assert.AreEqual(1, viewModel.SelectedCombinationCandidate.CandidateIndex);
                        Button action = (Button)view.FindName(accept ? "AcceptPathCombinationButton" : "CancelPathCombinationButton");
                        Assert.IsTrue(action.Command.CanExecute(null));
                        action.Command.Execute(null);
                    }).ConfigureAwait(false);
                    await Task.WhenAll(commands).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        bridge.RefreshSnapshot();
                        viewModel.Start();
                        Assert.IsFalse(viewModel.IsCombinationPanelVisible);
                        Assert.IsFalse(scope.Session.ExistingPathEditor.CanCancelPathInteraction);
                        Assert.AreEqual(accept, scope.Session.ExistingPathEditor.IsNewPath);
                        if (accept)
                            Assert.AreSequenceEqual(expectedArray80f20f20f80f, scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel().PathNodes.Select(node => node.Location.Location.X).ToArray());
                        else
                            Assert.IsNull(scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel());
                    }).ConfigureAwait(false);
                }
                finally
                {
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        viewModel?.Dispose();
                        scheduler?.Dispose();
                    }).ConfigureAwait(false);
                }
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(0)]
        [DataRow(1)]
        public async Task WhenOnlyTheConnectingSpanIsAmbiguousThenBothCandidatesCanBeChosenAndSavedInViewMode(int candidateIndex)
        {
            TrackWorld world = PathModelEditorCombineTests.CreateTiedConnectingWorld();
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            _ = await RouteModelHandler.ToFile(fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackModelHandler.Create(new TrackModel { Id = fixture.Route.Id, TrackDatabase = world.TrackDatabase }, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackSectionsModelHandler.Create(fixture.Sections, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await SignalConfigurationModelHandler.Create(new SignalConfigurationModel { Id = fixture.Route.Id, Tags = ImmutableDictionary<string, string>.Empty },
                fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            RuntimeDataResolver runtime = await RuntimeDataResolver.PrepareAsync(fixture.Route, true, null, TestContext.CancellationToken).ConfigureAwait(false);
            world = runtime.TrackWorld;

            static PathNode Anchor(float x, int nodeIndex)
            {
                return new PathNode(new WorldLocation(new Tile(0, 0), new Microsoft.Xna.Framework.Vector3(x, 0, 0))) { NodeIndex = nodeIndex };
            }

            PathModel first = new PathModel
            {
                Id = "first",
                PathNodes = [Anchor(-90, 1) with { NodeType = PathNodeType.Start, NextMainNode = 1, NextSidingNode = -1 }, Anchor(-70, 1) with { NodeType = PathNodeType.End, NextMainNode = -1, NextSidingNode = -1 }],
            };
            PathModel second = new PathModel
            {
                Id = "second",
                PathNodes = [Anchor(120, 2) with { NodeType = PathNodeType.Start, NextMainNode = 1, NextSidingNode = -1 }, Anchor(170, 2) with { NodeType = PathNodeType.End, NextMainNode = -1, NextSidingNode = -1 }],
            };
            PathEditResult combination = PathModelEditor.CombinePaths(first, second, world, TestContext.CancellationToken);
            Assert.IsTrue(combination.Success, combination.Message);
            using (JourneyMapSession journeyMapSession = new JourneyMapSession())
            {
                ToolboxContent content = new ToolboxContent(new MapContentContext(new MapRuntimeServices(runtime), journeyMapSession, null, null));
                using (PathEditor editor = new PathEditor(content))
                {
                    Assert.IsTrue(editor.CombinePathsCommand(combination).Success);
                    Assert.IsFalse(editor.EditMode);
                    Assert.AreEqual(ToolboxContentMode.ViewPath, content.ContentMode);
                    ResolvedPathSpan join = editor.GetAmbiguousSpans().Single();
                    Assert.AreEqual(1, join.FromNodeIndex);
                    Assert.HasCount(2, join.Candidates);
                    PathModel beforePreview = editor.TryCaptureCurrentPathModel();
                    Assert.IsTrue(editor.PreviewRouteCandidate(join.FromNodeIndex, candidateIndex).Success);
                    Assert.AreSame(beforePreview, editor.TryCaptureCurrentPathModel());
                    Assert.IsFalse(editor.CanUndo);
                    PathEditResult acceptance = editor.AcceptRouteCandidate(join.FromNodeIndex, candidateIndex);
                    Assert.IsTrue(acceptance.Success, acceptance.Message);
                    Assert.IsEmpty(editor.GetAmbiguousSpans());
                    Assert.IsFalse(editor.EditMode);
                    Assert.IsTrue(editor.CanUndo);
                    PathModel chosen = editor.TryCaptureCurrentPathModel();
                    int selectedVector = candidateIndex == 0 ? 4 : 5;
                    Assert.Contains(node => node.NodeIndex == selectedVector, chosen.PathNodes);
                    Assert.DoesNotContain(node => node.NodeIndex == (selectedVector == 4 ? 5 : 4), chosen.PathNodes);
                    PathPersistenceValidationResult save = await PathEditor.SaveValidatedPath(chosen with { Id = "Combined" + Guid.NewGuid().ToString("N"), Name = "Chosen combined path" },
                        fixture.Route, world, TestContext.CancellationToken).ConfigureAwait(false);
                    Assert.IsTrue(save.PersistenceAllowed, save.FailureMessage);
                    PathModel reopened = await fixture.Route.PathModelUncached(save.PathModel.Id, TestContext.CancellationToken).ConfigureAwait(false);
                    Assert.AreSequenceEqual(save.PathModel.PathNodes, reopened.PathNodes);
                    Assert.IsTrue(editor.Undo());
                    Assert.AreSequenceEqual(beforePreview.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                    Assert.HasCount(1, editor.GetAmbiguousSpans());
                }
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenCombinedPathContainsBasicWaitThenSessionCommitAndNativeReopenPreserveIt()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel first, PathModel second, PathModel third) = await CreateCombineModelsAsync().ConfigureAwait(false);
            first = await fixture.RoundTripPathAsync(first with
            {
                PathNodes = [first.PathNodes[0],
                    SyntheticPathFixture.Node(PathNodeType.Wait, 1, -80, 0, 2, -1) with { WaitInfo = new PathNodeWaitInfo { WaitTime = 45 } },
                    first.PathNodes[1] with { NextMainNode = -1 }],
            }, TestContext.CancellationToken).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                _ = await InitializeComparisonSessionAsync(scope, fixture, runtime, [first, second, third], commands).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(first.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(second.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.CombineSelectedPathsAsync(true)).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                PathModel combined = null;
                TrackWorld world = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    Assert.IsTrue(editor.IsNewPath);
                    combined = editor.TryCaptureCurrentPathModel();
                    world = editor.RuntimeTrackWorld;
                    PathNode wait = combined.PathNodes.Single(node => node.WaitInfo != null);
                    Assert.AreEqual((-80, 45), (wait.Location.Location.X, wait.WaitInfo.WaitTime));
                }).ConfigureAwait(false);

                PathPersistenceValidationResult saved = await PathEditor.SaveValidatedPath(combined with
                {
                    Id = "WaitCombined" + Guid.NewGuid().ToString("N"),
                    Name = "Wait combined path",
                }, fixture.Route, world, TestContext.CancellationToken).ConfigureAwait(false);
                Assert.IsTrue(saved.PersistenceAllowed, saved.FailureMessage);
                PathModel reopened = await fixture.Route.PathModelUncached(saved.PathModel.Id, TestContext.CancellationToken).ConfigureAwait(false);
                PathNode reopenedWait = reopened.PathNodes.Single(node => node.WaitInfo != null);
                Assert.AreEqual((-80, 45), (reopenedWait.Location.Location.X, reopenedWait.WaitInfo.WaitTime));
                await Task.WhenAll(commands).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenCombineReplacesAnExistingPathThenOnlyItsUnsavedEditsAreRetained(bool dirty)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel first, PathModel second, PathModel third) = await CreateCombineModelsAsync().ConfigureAwait(false);
            byte[] thirdBytes = await File.ReadAllBytesAsync(CompositionFile(third), TestContext.CancellationToken).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [first, second, third], commands).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(third)).ConfigureAwait(false));
                PathModel outgoing = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    if (dirty)
                    {
                        Assert.IsTrue(editor.SetMetadataCommand("Unsaved third path", "Changed origin", "Changed destination", !third.PlayerPath).Success);
                    }
                    outgoing = editor.TryCaptureCurrentPathModel();
                }).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(first.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(second.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.CombineSelectedPathsAsync(true)).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    Assert.IsTrue(scope.Session.ExistingPathEditor.IsNewPath);
                    bridge.RefreshSnapshot();
                    TrainPathListRow previous = bridge.CaptureTrainPathSnapshot().Paths.Single(row => row.Id == third.Id);
                    Assert.AreEqual(outgoing.Name, previous.Name);
                    Assert.AreEqual(dirty, previous.HasUnsavedChanges);
                    bridge.SelectPath(third.Id);
                }).ConfigureAwait(false);
                await Task.WhenAll(commands).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathModel reopened = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel();
                    Assert.AreEqual((outgoing.Id, outgoing.Name, outgoing.Start, outgoing.End, outgoing.PlayerPath),
                        (reopened.Id, reopened.Name, reopened.Start, reopened.End, reopened.PlayerPath));
                    Assert.AreSequenceEqual(outgoing.PathNodes, reopened.PathNodes);
                    bridge.RefreshSnapshot();
                    Assert.AreEqual(dirty, bridge.CaptureTrainPathSnapshot().Paths.Single(row => row.Id == third.Id).HasUnsavedChanges);
                }).ConfigureAwait(false);
                Assert.AreSequenceEqual(thirdBytes, await File.ReadAllBytesAsync(CompositionFile(third), TestContext.CancellationToken).ConfigureAwait(false));
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenShutdownDrainsCombineOrCompositionThenTheOperationDisposesItsOwnRequest(bool append)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel first, PathModel second, PathModel third) = await CreateCombineModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [first, second, third]).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(third)).ConfigureAwait(false));
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(first.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(second.Id, true)).ConfigureAwait(false);
                Task operation = null;
                Task drain = null;
                CancellationTokenSource request = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    operation = append
                        ? scope.Session.SetSavedPathCompositionAsync(bridge.CaptureTrainPathSnapshot().CompositionSources.Single(row => row.Id == second.Id), true)
                        : scope.Session.CombineSelectedPathsAsync(true);
                    Assert.IsFalse(operation.IsCompleted);
                    request = (CancellationTokenSource)typeof(ToolboxSession).GetField(append ? "compositionRequest" : "combineRequest", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope.Session);
                    Assert.IsNotNull(request);
                    CancellationToken token = request.Token;
                    drain = scope.Session.StopAndDrainAsync();
                    Assert.IsTrue(token.IsCancellationRequested);
                }).ConfigureAwait(false);
                await drain.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                Assert.IsTrue(operation.IsCompletedSuccessfully);
                _ = Assert.ThrowsExactly<ObjectDisposedException>(() => _ = request.Token);
                await scope.Host.OnOwnerAsync(() =>
                {
                    scope.Session.Dispose();
                    Assert.IsTrue(scope.Session.IsDisposed);
                    Assert.IsNull(scope.Session.ExistingPathEditor);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow("no-active-path")]
        [DataRow("view")]
        [DataRow("edit")]
        public async Task WhenTwoSavedCheckboxesAreCombinedThenViewModeSaveAndReopenPreserveBothSourcesAndOverlays(string initialState)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel first, PathModel second, PathModel third) = await CreateCombineModelsAsync().ConfigureAwait(false);
            byte[] firstBytes = await File.ReadAllBytesAsync(CompositionFile(first), TestContext.CancellationToken).ConfigureAwait(false);
            byte[] secondBytes = await File.ReadAllBytesAsync(CompositionFile(second), TestContext.CancellationToken).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [first, second, third], commands).ConfigureAwait(false);
                Assert.IsNull(scope.Session.ExistingPathEditor);
                if (initialState != "no-active-path")
                {
                    Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(third)).ConfigureAwait(false));
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        if (initialState == "edit")
                            scope.Session.ExistingPathEditor.InitializeNewPath();
                        Assert.AreEqual(initialState == "edit", scope.Session.ExistingPathEditor.EditMode);
                    }).ConfigureAwait(false);
                }
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(first.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(second.Id, true)).ConfigureAwait(false);
                PathCache.Set(first.Hierarchy(), Task.FromResult<PathModelHeader>(first with { PathNodes = ImmutableArray<PathNode>.Empty }));
                PathCache.Set(second.Hierarchy(), Task.FromResult<PathModelHeader>(second with { PathNodes = ImmutableArray<PathNode>.Empty }));
                try
                {
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        PathModel original = scope.Session.ExistingPathEditor?.TryCaptureCurrentPathModel();
                        bridge.RefreshSnapshot();
                        TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                        if (initialState == "no-active-path")
                            Assert.IsNull(snapshot.SelectedPathId);
                        Assert.IsTrue(snapshot.CanCombineSelectedPaths);
                        bridge.RefreshSnapshot();
                        Assert.AreSame(snapshot, bridge.CaptureTrainPathSnapshot());
                        using ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
                        using TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler);
                        viewModel.Start();
                        TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
                        view.Measure(new Size(520, 700));
                        view.Arrange(new Rect(0, 0, 520, 700));
                        view.UpdateLayout();
                        Assert.AreEqual(2, viewModel.Paths.Count(path => path.IsCompared));
                        MenuItem combine = (MenuItem)view.FindName("CombinePathsMenuItem");
                        Assert.IsTrue(combine.Command.CanExecute(null));
                        combine.Command.Execute(null);
                        Assert.IsTrue(viewModel.IsCombiningPaths);
                        Assert.AreSame(original, scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel(), "The worker must not commit inside admission.");
                    }).ConfigureAwait(false);
                    await Task.WhenAll(commands).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    PathCache.Remove(first.Hierarchy());
                    PathCache.Remove(second.Hierarchy());
                }
                PathModel combined = null;
                TrainPathSaveRequest saveRequest = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    combined = editor.TryCaptureCurrentPathModel();
                    Assert.IsTrue(editor.IsNewPath);
                    Assert.IsTrue(editor.HasUnsavedChanges);
                    Assert.IsFalse(editor.CanUndo);
                    Assert.IsFalse(editor.EditMode);
                    Assert.AreEqual(ToolboxContentMode.ViewPath, content.ContentMode);
                    Assert.IsFalse(editor.CanCancelPathInteraction);
                    Assert.IsTrue(editor.CanContinuePath);
                    Assert.AreSequenceEqual(new[] { -90f, -70f, -40f, -10f }, combined.PathNodes.Select(node => node.Location.Location.X).ToArray());
                    bridge.Active = true;
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    Assert.AreEqual(2, snapshot.Comparisons.Count(row => row.IsSelected));
                    Assert.IsFalse(snapshot.IsCombiningPaths);
                    Assert.IsFalse(snapshot.CommandResultIsWarning, snapshot.CommandResultMessage);
                    Assert.Contains("combined", snapshot.CommandResultMessage);
                    TrainPathSaveState state = scope.Session.CaptureTrainPathSaveState();
                    saveRequest = new TrainPathSaveRequest(state.PathDetails with { Id = "CombinedCopy" + Guid.NewGuid().ToString("N") }, state.SourcePathId, false);
                }).ConfigureAwait(false);
                Assert.AreSequenceEqual(firstBytes, await File.ReadAllBytesAsync(CompositionFile(first), TestContext.CancellationToken).ConfigureAwait(false));
                Assert.AreSequenceEqual(secondBytes, await File.ReadAllBytesAsync(CompositionFile(second), TestContext.CancellationToken).ConfigureAwait(false));
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SubmitTrainPathSaveAsync(saveRequest)).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                PathModel saved = await fixture.Route.PathModelUncached(saveRequest.PathDetails.Id, TestContext.CancellationToken).ConfigureAwait(false);
                Assert.AreSequenceEqual(PathRouteResolver.Resolve(combined, runtime.TrackWorld, TestContext.CancellationToken).MainRoute.Spans.SelectMany(span => span.Candidates.Single().PhysicalTraversals).ToArray(),
                    PathRouteResolver.Resolve(saved, runtime.TrackWorld, TestContext.CancellationToken).MainRoute.Spans.SelectMany(span => span.Candidates.Single().PhysicalTraversals).ToArray());
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(null)).ConfigureAwait(false));
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));
                await scope.Host.OnOwnerAsync(() =>
                {
                    Assert.AreSequenceEqual(saved.PathNodes, scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel().PathNodes);
                    Assert.IsFalse(scope.Session.ExistingPathEditor.HasUnsavedChanges);
                    Assert.IsFalse(scope.Session.ExistingPathEditor.EditMode);
                    bridge.RefreshSnapshot();
                    Assert.AreEqual(2, bridge.CaptureTrainPathSnapshot().Comparisons.Count(row => row.IsSelected));
                }).ConfigureAwait(false);
                Assert.AreSequenceEqual(firstBytes, await File.ReadAllBytesAsync(CompositionFile(first), TestContext.CancellationToken).ConfigureAwait(false));
                Assert.AreSequenceEqual(secondBytes, await File.ReadAllBytesAsync(CompositionFile(second), TestContext.CancellationToken).ConfigureAwait(false));
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow("uncheck")]
        [DataRow("selection-aba")]
        [DataRow("third-aba")]
        [DataRow("edit")]
        [DataRow("edit-undo")]
        [DataRow("placement")]
        [DataRow("cancel")]
        [DataRow("read-failure")]
        public async Task WhenSharedSelectionOrEditorChangesDuringCombineThenNoInterveningStateIsOverwritten(string action)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel first, PathModel second, PathModel third) = await CreateCombineModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [first, second, third]).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(third)).ConfigureAwait(false));
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(first.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(second.Id, true)).ConfigureAwait(false);
                Task combine = null;
                Task followup = Task.CompletedTask;
                PathModel expected = null;
                PathModel[] undo = null;
                PathModel[] redo = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    if (action == "read-failure")
                        File.Delete(CompositionFile(first));
                    combine = scope.Session.CombineSelectedPathsAsync(true);
                    Assert.IsFalse(combine.IsCompleted);
                    switch (action)
                    {
                        case "uncheck":
                            followup = scope.Session.SetSavedPathComparisonAsync(second.Id, false);
                            break;
                        case "selection-aba":
                            followup = Task.WhenAll(scope.Session.SetSavedPathComparisonAsync(second.Id, false), scope.Session.SetSavedPathComparisonAsync(second.Id, true));
                            break;
                        case "third-aba":
                            followup = Task.WhenAll(scope.Session.SetSavedPathComparisonAsync(third.Id, true), scope.Session.SetSavedPathComparisonAsync(third.Id, false));
                            break;
                        case "edit":
                        case "edit-undo":
                            Assert.IsTrue(editor.SetMetadataCommand("Intervening edit", third.Start, third.End, third.PlayerPath).Success);
                            if (action == "edit-undo")
                                Assert.IsTrue(editor.Undo());
                            break;
                        case "placement":
                            Assert.IsTrue(editor.ContinuePathCommand().Success);
                            break;
                        case "cancel":
                            followup = scope.Session.CombineSelectedPathsAsync(false);
                            break;
                    }
                    expected = editor.TryCaptureCurrentPathModel();
                    undo = SavePreflightHistory(editor, "undoHistory");
                    redo = SavePreflightHistory(editor, "redoHistory");
                }).ConfigureAwait(false);
                await Task.WhenAll(combine, followup).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    Assert.AreSame(expected, editor.TryCaptureCurrentPathModel());
                    Assert.AreSequenceEqual(undo, SavePreflightHistory(editor, "undoHistory"));
                    Assert.AreSequenceEqual(redo, SavePreflightHistory(editor, "redoHistory"));
                    Assert.IsEmpty((Dictionary<string, PathModel>)typeof(TrainPathToolWindow).GetField("transientPaths", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bridge),
                        "Canceled, failed and stale combines must not capture a transient path.");
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    Assert.IsFalse(snapshot.IsCombiningPaths);
                    if (action == "cancel")
                    {
                        Assert.IsFalse(snapshot.CommandResultIsWarning);
                        Assert.Contains("canceled", snapshot.CommandResultMessage);
                    }
                    else if (action is not "selection-aba" and not "third-aba")
                        Assert.IsTrue(snapshot.CommandResultIsWarning, snapshot.CommandResultMessage);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(3)]
        public async Task WhenCheckedSavedPathCountIsNotTwoThenCombineRefusesWithoutCreatingAnEditor(int count)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel first, PathModel second, PathModel third) = await CreateCombineModelsAsync().ConfigureAwait(false);
            PathModel[] paths = [first, second, third];
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, paths.Cast<PathModelHeader>().ToImmutableArray()).ConfigureAwait(false);
                foreach (PathModel path in paths.Take(count))
                    await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(path.Id, true)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.CombineSelectedPathsAsync(true)).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    Assert.IsNull(scope.Session.ExistingPathEditor);
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    Assert.IsFalse(snapshot.CanCombineSelectedPaths);
                    Assert.IsTrue(snapshot.CommandResultIsWarning);
                    Assert.AreEqual(count, snapshot.Comparisons.Count(row => row.IsSelected));
                }).ConfigureAwait(false);
            }
        }

        private async Task<(SyntheticPathFixture Fixture, RuntimeDataResolver Runtime, PathModel First, PathModel Second, PathModel Third)> CreateAmbiguousCombineModelsAsync()
        {
            TrackWorld world = PathModelEditorCombineTests.CreateLoopWorld();
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackDatabase database = world.TrackDatabase with
            {
                TrackNodes = [null, ((VectorNode)fixture.Track.TrackDatabase.TrackNodes[4]) with { NodeIndex = 1 },
                    world.TrackDatabase.TrackNodes[2], world.TrackDatabase.TrackNodes[3],
                    ((VectorNode)fixture.Track.TrackDatabase.TrackNodes[5]) with { NodeIndex = 4 }],
            };
            _ = await RouteModelHandler.ToFile(fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackModelHandler.Create(new TrackModel { Id = fixture.Route.Id, TrackDatabase = database }, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackSectionsModelHandler.Create(fixture.Sections, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await SignalConfigurationModelHandler.Create(new SignalConfigurationModel { Id = fixture.Route.Id, Tags = ImmutableDictionary<string, string>.Empty }, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            RuntimeDataResolver runtime = await RuntimeDataResolver.PrepareAsync(fixture.Route, true, null, TestContext.CancellationToken).ConfigureAwait(false);
            async Task<PathModel> SavePathAsync(string name, int vector, float start, float end) => await fixture.RoundTripPathAsync(new PathModel
            {
                Id = name + Guid.NewGuid().ToString("N"),
                Name = name,
                Start = name + " origin",
                End = name + " destination",
                PathNodes = [SyntheticPathFixture.Node(PathNodeType.Start, vector, start, vector == 4 ? 100 : 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, vector, end, vector == 4 ? 100 : 0, -1, -1)],
            }, TestContext.CancellationToken).ConfigureAwait(false);
            return (fixture, runtime, await SavePathAsync("First", 1, 20, 80).ConfigureAwait(false),
                await SavePathAsync("Second", 4, 80, 20).ConfigureAwait(false), await SavePathAsync("Third", 1, 30, 70).ConfigureAwait(false));
        }

        private async Task<(SyntheticPathFixture Fixture, RuntimeDataResolver Runtime, PathModel First, PathModel Second, PathModel Third)> CreateCombineModelsAsync()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, _, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            async Task<PathModel> SavePathAsync(string name, int vector, float start, float end)
                => await fixture.RoundTripPathAsync(new PathModel
                {
                    Id = name + Guid.NewGuid().ToString("N"),
                    Name = name,
                    Start = "Origin",
                    End = "Destination",
                    PathNodes = [SyntheticPathFixture.Node(PathNodeType.Start, vector, start, 0, 1, -1),
                        SyntheticPathFixture.Node(PathNodeType.End, vector, end, 0, -1, -1)],
                }, TestContext.CancellationToken).ConfigureAwait(false);
            return (fixture, runtime, await SavePathAsync("First", 1, -90, -70).ConfigureAwait(false),
                await SavePathAsync("Second", 1, -40, -10).ConfigureAwait(false), await SavePathAsync("Third", 2, 120, 170).ConfigureAwait(false));
        }
    }
}