using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Simulator;
using Tests.FreeTrainSimulator.Toolbox.PathEditing;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        /// <summary>Both authoring orders reach native Save As through the real hosted menu/bridge, without modifying the saved source.</summary>
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(false, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, false, false)]
        [DataRow(true, true, false)]
        [DataRow(false, false, true)]
        [DataRow(false, true, true)]
        [DataRow(true, false, true)]
        [DataRow(true, true, true)]
        public async Task WhenHostedSecondBranchIsAuthoredThenCapabilitiesSaveAsAndUncachedReopenPreserveBothOwners(
            bool firstGuided, bool secondGuided, bool reverseOrder)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved) = await CreateSeparatedSessionModelsAsync(false).ConfigureAwait(false);
            byte[] sourceBytes = await File.ReadAllBytesAsync(CompositionFile(saved), TestContext.CancellationToken).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync(true).ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved], commands).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() => SetField(scope.Game, "hostedTrainPathToolWindow", bridge)).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));
                ToolWindowRefreshScheduler scheduler = null;
                TrainPathToolWindowViewModel viewModel = null;
                try
                {
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
                        viewModel = new TrainPathToolWindowViewModel(bridge, scheduler);
                        viewModel.Start();
                        bridge.RefreshSnapshot();
                    }).ConfigureAwait(false);
                    int firstSplit = reverseOrder ? 8 : 3;
                    int firstRejoin = reverseOrder ? 11 : 6;
                    int secondSplit = reverseOrder ? 3 : 8;
                    int secondRejoin = reverseOrder ? 6 : 11;
                    PathModel first = null;
                    PathModel authored = null;
                    await CapabilityAsync(firstSplit, true, true).ConfigureAwait(false);
                    await AuthorAsync(firstSplit, firstRejoin, firstGuided).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() => first = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel()).ConfigureAwait(false);
                    await CapabilityAsync(secondSplit, true, true).ConfigureAwait(false);
                    await AuthorAsync(secondSplit, secondRejoin, secondGuided).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() => authored = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel()).ConfigureAwait(false);
                    await CapabilityAsync(firstSplit, false, false).ConfigureAwait(false);
                    await CapabilityAsync(firstRejoin, false, false).ConfigureAwait(false);
                    await CapabilityAsync(secondSplit, false, false).ConfigureAwait(false);
                    await CapabilityAsync(secondRejoin, false, false).ConfigureAwait(false);
                    await CapabilityAsync(5, false, true).ConfigureAwait(false);
                    await CapabilityAsync(10, false, true).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        PathEditor editor = scope.Session.ExistingPathEditor;
                        Assert.IsTrue(editor.Undo());
                        Assert.AreSequenceEqual(first.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                        Assert.IsTrue(editor.Redo());
                        Assert.AreSequenceEqual(authored.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                    }).ConfigureAwait(false);
                    PathModel refusedSource = null;
                    await scope.Host.OnOwnerAsync(() => refusedSource = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel()).ConfigureAwait(false);
                    // A queued bridge command must publish the actual refusal, not optimistic authoring feedback.
                    await CommandAsync(() => bridge.BeginPassingBranch(SeparatedPassingFixture.Index(authored, secondSplit))).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        Assert.AreSame(refusedSource, scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel());
                        Assert.IsTrue(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                        Assert.AreEqual(PassingBranchAuthoringPhase.Idle, bridge.CaptureTrainPathSnapshot().PassingBranchPhase);
                    }).ConfigureAwait(false);

                    string savedId = "HostedTwoOwners" + Guid.NewGuid().ToString("N");
                    int saveCount = 0;
                    void save(object _1, EventArgs _2)
                    {
                        saveCount++;
                        TrainPathSaveState state = scope.Session.CaptureTrainPathSaveState();
                        commands.Enqueue(scope.Session.SubmitTrainPathSaveAsync(new TrainPathSaveRequest(
                            state.PathDetails with { Id = savedId }, state.SourcePathId, false)));
                    }
                    await scope.Host.OnOwnerAsync(() => scope.Game.SaveTrainPathRequested += save).ConfigureAwait(false);
                    try
                    {
                        await CommandAsync(bridge.SavePath).ConfigureAwait(false);
                    }
                    finally
                    {
                        await scope.Host.OnOwnerAsync(() => scope.Game.SaveTrainPathRequested -= save).ConfigureAwait(false);
                    }
                    Assert.AreEqual(1, saveCount);
                    PathModel persisted = null;
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        persisted = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel();
                        Assert.AreEqual(savedId, persisted.Id);
                        Assert.IsFalse(scope.Session.ExistingPathEditor.HasUnsavedChanges);
                        Assert.AreEqual("Path saved.", bridge.CaptureTrainPathSnapshot().CommandResultMessage);
                    }).ConfigureAwait(false);
                    PathModel disk = await fixture.Route.PathModelUncached(savedId, TestContext.CancellationToken).ConfigureAwait(false);
                    Assert.AreNotSame(persisted, disk);
                    TestContext.WriteLine("Save count: {0}; native artifact: {1}", saveCount, CompositionFile(disk));
                    PathRouteResolution expected = PathRouteResolver.Resolve(authored, runtime.TrackWorld, TestContext.CancellationToken);
                    AssertSeparatedTraversal(authored, expected, disk, runtime.TrackWorld);
                    await CommandAsync(() => bridge.SelectPath(null)).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        Assert.IsNull(scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel());
                        Assert.IsNull(scope.Session.ExistingPathEditor.TrainPath);
                        Assert.IsNull(bridge.CaptureTrainPathSnapshot().SelectedPathId);
                    }).ConfigureAwait(false);
                    PathCache.Remove(fixture.Route.Hierarchy(savedId));
                    PathModelHeader header = await JourneyPathStore.ReadHeaderAsync(savedId, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
                    Assert.IsNotInstanceOfType<PathModel>(header);
                    await scope.Host.OnOwnerAsync(() => bridge.UpdatePaths([header])).ConfigureAwait(false);
                    await CommandAsync(() => bridge.SelectPath(header.Id)).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        PathEditor editor = scope.Session.ExistingPathEditor;
                        PathModel reopened = editor.TryCaptureCurrentPathModel();
                        Assert.AreNotSame(persisted, reopened);
                        Assert.AreNotSame(disk, reopened);
                        AssertSeparatedTraversal(authored, expected, reopened, runtime.TrackWorld);
                        Assert.AreSequenceEqual(disk.PathNodes, reopened.PathNodes);
                        Assert.IsFalse(editor.HasUnsavedChanges);
                    }).ConfigureAwait(false);
                    Assert.AreSequenceEqual(sourceBytes, await File.ReadAllBytesAsync(CompositionFile(saved), TestContext.CancellationToken).ConfigureAwait(false));

                    async Task CommandAsync(Action command)
                    {
                        await scope.Host.OnOwnerAsync(command).ConfigureAwait(false);
                        Assert.IsFalse(commands.IsEmpty);
                        while (commands.TryDequeue(out Task operation))
                            await operation.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        await scope.Host.OnOwnerAsync(bridge.RefreshSnapshot).ConfigureAwait(false);
                    }

                    async Task MapAsync(Func<MapContextMenuItem> createItem)
                    {
                        await CommandAsync(() =>
                        {
                            MapContextMenuItem item = createItem();
                            MapContextMenuRequestedEventArgs request = new MapContextMenuRequestedEventArgs(0, 0, item.NodeIndex, [item], scope.Session.ExistingPathEditor);
                            scope.Game.ExecuteMapContextMenuAction(request, item);
                        }).ConfigureAwait(false);
                    }

                    async Task AuthorAsync(int splitTrack, int rejoinTrack, bool guided)
                    {
                        await MapAsync(() => bridge.GetNodeActions(SeparatedPassingFixture.Index(scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel(), splitTrack))
                            .Single(item => item.Action == MapContextMenuAction.StartPassingBranch)).ConfigureAwait(false);
                        await MapAsync(() => bridge.GetNodeActions(SeparatedPassingFixture.Index(scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel(), rejoinTrack))
                            .Single(item => item.Action == (guided ? MapContextMenuAction.RejoinPassingBranchWithVia : MapContextMenuAction.RejoinPassingBranch))).ConfigureAwait(false);
                        if (guided)
                        {
                            await MapAsync(() => MapContextMenuActionBuilder.BuildForMap(new MapContextMenuActionBuilder.MapContextMenuState
                            {
                                IsPlacementActive = true, IsPlacingPassingBranchVia = true, CanCancelPassingBranch = true,
                            }, SeparatedPassingFixture.Via(splitTrack)).Single(item => item.Action == MapContextMenuAction.PreviewPassingBranchViaHere)).ConfigureAwait(false);
                            await MapAsync(() => bridge.GetNodeActions(SeparatedPassingFixture.Index(scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel(), rejoinTrack))
                                .Single(item => item.Action == MapContextMenuAction.ConfirmPassingBranchVia)).ConfigureAwait(false);
                        }
                        await scope.Host.OnOwnerAsync(() => Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning)).ConfigureAwait(false);
                    }

                    async Task CapabilityAsync(int track, bool begin, bool move)
                    {
                        int index = -1;
                        await scope.Host.OnOwnerAsync(() => index = SeparatedPassingFixture.Index(scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel(), track)).ConfigureAwait(false);
                        await CommandAsync(() => bridge.HighlightNode(index)).ConfigureAwait(false);
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            viewModel.Start();
                            TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                            Assert.AreEqual((begin, move, begin, move, begin, move),
                                (snapshot.CanBeginPassingBranch, snapshot.CanMoveSelectedNode, viewModel.CanBeginPassingBranch, viewModel.CanMoveSelectedNode,
                                    bridge.GetNodeActions(index).Any(item => item.Action == MapContextMenuAction.StartPassingBranch),
                                    bridge.GetNodeActions(index).Any(item => item.Action == MapContextMenuAction.MoveNode)));
                            Assert.AreEqual(begin, viewModel.BeginPassingBranchCommand.CanExecute(null));
                            Assert.AreEqual(move, viewModel.MoveSelectedNodeCommand.CanExecute(null));
                        }).ConfigureAwait(false);
                    }
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

        /// <summary>A expectedArray18-owner guided menu cannot act after history ABA or editor replacement, even when the first owner still matches.</summary>
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow((int)MapContextMenuAction.RejoinPassingBranchWithVia, false)]
        [DataRow((int)MapContextMenuAction.CancelPassingBranch, false)]
        [DataRow((int)MapContextMenuAction.PreviewPassingBranchViaHere, false)]
        [DataRow((int)MapContextMenuAction.ConfirmPassingBranchVia, false)]
        [DataRow((int)MapContextMenuAction.RejoinPassingBranchWithVia, true)]
        [DataRow((int)MapContextMenuAction.CancelPassingBranch, true)]
        [DataRow((int)MapContextMenuAction.PreviewPassingBranchViaHere, true)]
        [DataRow((int)MapContextMenuAction.ConfirmPassingBranchVia, true)]
        public async Task WhenOldSecondGuidedMenuIsClickedThenFirstOwnerNewDraftAndHistoryAreUntouched(int actionValue, bool reload)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved) = await CreateSeparatedSessionModelsAsync(true).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved], commands).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));
                MapContextMenuRequestedEventArgs request = null;
                MapContextMenuItem item = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetField(scope.Game, "hostedTrainPathToolWindow", bridge);
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    PathModel source = editor.TryCaptureCurrentPathModel();
                    int split = SeparatedPassingFixture.Index(source, 8);
                    int rejoin = SeparatedPassingFixture.Index(source, 11);
                    Assert.IsTrue(editor.BeginPassingBranchCommand(split).Success);
                    MapContextMenuAction action = (MapContextMenuAction)actionValue;
                    if (action is MapContextMenuAction.PreviewPassingBranchViaHere or MapContextMenuAction.ConfirmPassingBranchVia)
                        Assert.IsTrue(editor.RejoinPassingBranchWithViaCommand(rejoin).Success);
                    if (action == MapContextMenuAction.ConfirmPassingBranchVia)
                        Assert.IsTrue(editor.PreviewPassingBranchViaCommand(SeparatedPassingFixture.Via(8), false).Success);
                    item = new MapContextMenuItem(action, rejoin) { PlacementAnchor = SeparatedPassingFixture.Via(8) };
                    request = new MapContextMenuRequestedEventArgs(0, 0, rejoin, [item], editor);
                    Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                    SeparatedPassingFixture.AuthorBranch(editor, 8, 11, true);
                    Assert.IsTrue(editor.Undo());
                    PathModel restored = editor.TryCaptureCurrentPathModel();
                    Assert.AreSequenceEqual(source.PathNodes, restored.PathNodes);
                    PathRouteResolution before = editor.ResolveCurrent(source);
                    PathRouteResolution after = editor.ResolveCurrent(restored);
                    Assert.IsTrue(before.MainRoute.HasSamePhysicalTraversal(after.MainRoute));
                    Assert.IsTrue(before.PassingRoutes.Single().HasSamePhysicalTraversal(after.PassingRoutes.Single()));
                }).ConfigureAwait(false);
                if (reload)
                {
                    Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(null)).ConfigureAwait(false));
                    Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));
                }
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    PathModel source = editor.TryCaptureCurrentPathModel();
                    int split = SeparatedPassingFixture.Index(source, 8);
                    int rejoin = SeparatedPassingFixture.Index(source, 11);
                    Assert.IsTrue(editor.BeginPassingBranchCommand(split).Success);
                    Assert.IsTrue(editor.RejoinPassingBranchWithViaCommand(rejoin).Success);
                    Assert.IsTrue(editor.PreviewPassingBranchViaCommand(SeparatedPassingFixture.Via(8), false).Success);
                    long revision = editor.PassingBranchInteractionRevision;
                    PathModel[] undo = SavePreflightHistory(editor, "undoHistory");
                    PathModel[] redo = SavePreflightHistory(editor, "redoHistory");
                    Assert.IsFalse(request.IsCurrent(editor));

                    scope.Game.ExecuteMapContextMenuAction(request, item);

                    Assert.IsEmpty(commands);
                    Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                    Assert.HasCount(1, editor.ResolveCurrent(source).PassingRoutes);
                    Assert.AreEqual((PassingBranchAuthoringPhase.ConfirmingVia, revision), (editor.PassingBranchPhase, editor.PassingBranchInteractionRevision));
                    Assert.AreSequenceEqual(undo, SavePreflightHistory(editor, "undoHistory"));
                    Assert.AreSequenceEqual(redo, SavePreflightHistory(editor, "redoHistory"));
                    Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                    Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                    Assert.AreSequenceEqual(redo, SavePreflightHistory(editor, "redoHistory"));
                }).ConfigureAwait(false);
            }
        }

        private async Task<(SyntheticPathFixture Fixture, RuntimeDataResolver Runtime, PathModel Saved)> CreateSeparatedSessionModelsAsync(bool firstOwner)
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            PathModel saved;
            using (SeparatedPassingFixture composition = await SeparatedPassingFixture.CreateAsync().ConfigureAwait(false))
            {
                using (PathEditor editor = new PathEditor(composition.Content))
                {
                    Assert.IsTrue(await editor.InitializePathAsync(SeparatedPassingFixture.CreateMain(), TestContext.CancellationToken).ConfigureAwait(false));
                    if (firstOwner)
                        SeparatedPassingFixture.AuthorBranch(editor, 3, 6, true);
                    saved = await fixture.RoundTripPathAsync(editor.TryCaptureCurrentPathModel(), TestContext.CancellationToken).ConfigureAwait(false);
                }
            }
            _ = await RouteModelHandler.ToFile(fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackModelHandler.Create(fixture.Track with { TrackDatabase = SyntheticPathFixture.CreateTwoSeparatedBranchesDatabase() }, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackSectionsModelHandler.Create(fixture.Sections, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await SignalConfigurationModelHandler.Create(new SignalConfigurationModel { Id = fixture.Route.Id, Tags = ImmutableDictionary<string, string>.Empty },
                fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            RuntimeDataResolver runtime = await RuntimeDataResolver.PrepareAsync(fixture.Route, true, null, TestContext.CancellationToken).ConfigureAwait(false);
            return (fixture, runtime, saved);
        }

        private void AssertSeparatedTraversal(PathModel source, PathRouteResolution expected, PathModel actual, TrackWorld world)
        {
            PathRouteResolution resolution = PathRouteResolver.Resolve(actual, world, TestContext.CancellationToken);
            Assert.IsTrue(resolution.IsValid);
            Assert.HasCount(2, resolution.PassingRoutes);
            Assert.IsTrue(expected.MainRoute.HasSamePhysicalTraversal(resolution.MainRoute));
            Assert.IsTrue(expected.PassingRoutes.All(owner => owner.HasSamePhysicalTraversal(resolution.PassingRoutes.Single(route =>
                actual.PathNodes[route.StartNodeIndex].NodeIndex == source.PathNodes[owner.StartNodeIndex].NodeIndex))));
        }
    }
}
