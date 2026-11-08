using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Simulator;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        /// <summary>Menu ownership is captured before clicking, including cancel and placement actions.</summary>
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
        public async Task WhenOldGuidedMenuIsClickedThenNewerSourceAndDraftAreUntouched(int actionValue, bool reload)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
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
                    (int split, int rejoin) = GuidedEndpoints(editor);
                    Assert.IsTrue(editor.BeginPassingBranchCommand(split).Success);
                    MapContextMenuAction action = (MapContextMenuAction)actionValue;
                    if (action is MapContextMenuAction.PreviewPassingBranchViaHere or MapContextMenuAction.ConfirmPassingBranchVia)
                        Assert.IsTrue(editor.RejoinPassingBranchWithViaCommand(rejoin).Success);
                    if (action == MapContextMenuAction.ConfirmPassingBranchVia)
                        Assert.IsTrue(editor.PreviewPassingBranchViaCommand(GuidedVia(), false).Success);
                    item = new MapContextMenuItem(action, rejoin) { PlacementAnchor = GuidedVia() };
                    request = new MapContextMenuRequestedEventArgs(0, 0, rejoin, [item], editor);
                    Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                }).ConfigureAwait(false);
                if (reload)
                {
                    Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(null)).ConfigureAwait(false));
                    Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));
                }
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    (int split, int rejoin) = GuidedEndpoints(editor);
                    Assert.IsTrue(editor.BeginPassingBranchCommand(split).Success);
                    Assert.IsTrue(editor.RejoinPassingBranchWithViaCommand(rejoin).Success);
                    Assert.IsTrue(editor.PreviewPassingBranchViaCommand(GuidedVia(), false).Success);
                    PathModel newer = editor.TryCaptureCurrentPathModel();
                    long revision = editor.PassingBranchInteractionRevision;
                    Assert.IsFalse(request.IsCurrent(editor));

                    scope.Game.ExecuteMapContextMenuAction(request, item);

                    Assert.IsEmpty(commands);
                    Assert.AreSame(newer, editor.TryCaptureCurrentPathModel());
                    Assert.AreEqual((PassingBranchAuthoringPhase.ConfirmingVia, revision),
                        (editor.PassingBranchPhase, editor.PassingBranchInteractionRevision));
                    Assert.IsFalse(editor.HasUnsavedChanges);
                    Assert.IsFalse(editor.CanUndo);
                }).ConfigureAwait(false);
            }
        }

        /// <summary>Current map commands reach the bridge and native session save without overwriting the source.</summary>
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenGuidedMapBranchIsSavedAsThenUncachedReopenPreservesTraversalAndSourceBytes()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            byte[] sourceBytes = await File.ReadAllBytesAsync(CompositionFile(saved), TestContext.CancellationToken).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync(true).ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved], commands).ConfigureAwait(false);
                SetField(scope.Game, "hostedTrainPathToolWindow", bridge);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));
                PathModel baseline = null;
                int split = -1;
                int rejoin = -1;
                await scope.Host.OnOwnerAsync(() =>
                {
                    baseline = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel();
                    (split, rejoin) = GuidedEndpoints(scope.Session.ExistingPathEditor);
                }).ConfigureAwait(false);
                await CommandAsync(() => bridge.BeginPassingBranch(split)).ConfigureAwait(false);
                await MapActionAsync(() => bridge.GetNodeActions(rejoin).Single(entry => entry.Action == MapContextMenuAction.RejoinPassingBranchWithVia)).ConfigureAwait(false);
                await MapActionAsync(() => MapContextMenuActionBuilder.BuildForMap(new MapContextMenuActionBuilder.MapContextMenuState
                {
                    IsPlacementActive = true,
                    IsPlacingPassingBranchVia = true,
                    CanCancelPassingBranch = true,
                }, GuidedVia()).Single(entry => entry.Action == MapContextMenuAction.PreviewPassingBranchViaHere)).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() => Assert.AreSame(baseline, scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel())).ConfigureAwait(false);
                await MapActionAsync(() => bridge.GetNodeActions(rejoin).Single(entry => entry.Action == MapContextMenuAction.ConfirmPassingBranchVia)).ConfigureAwait(false);
                PathModel authored = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    authored = editor.TryCaptureCurrentPathModel();
                    Assert.AreEqual(PassingBranchAuthoringPhase.Idle, editor.PassingBranchPhase);
                    Assert.IsTrue(editor.HasUnsavedChanges);
                    Assert.IsTrue(editor.Undo());
                    Assert.AreSequenceEqual(baseline.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                    Assert.IsFalse(editor.CanUndo);
                    Assert.IsTrue(editor.Redo());
                    Assert.AreSequenceEqual(authored.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                }).ConfigureAwait(false);
                string savedId = "GuidedMapCopy" + Guid.NewGuid().ToString("N");
                void save(object _1, EventArgs _2)
                {
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
                PathModel disk = await fixture.Route.PathModelUncached(savedId, TestContext.CancellationToken).ConfigureAwait(false);
                PathRouteResolution expected = PathRouteResolver.Resolve(authored, runtime.TrackWorld, TestContext.CancellationToken);
                PathRouteResolution actual = PathRouteResolver.Resolve(disk, runtime.TrackWorld, TestContext.CancellationToken);
                Assert.IsTrue(actual.IsValid);
                Assert.IsTrue(expected.MainRoute.HasSamePhysicalTraversal(actual.MainRoute));
                Assert.IsTrue(expected.PassingRoutes.Single().HasSamePhysicalTraversal(actual.PassingRoutes.Single()));
                Assert.IsTrue(disk.PathNodes.Any(node => node.Location == GuidedVia().Location && node.NextMainNode < 0));
                await CommandAsync(() => bridge.SelectPath(null)).ConfigureAwait(false);
                PathCache.Remove(fixture.Route.Hierarchy(savedId));
                PathModelHeader header = await JourneyPathStore.ReadHeaderAsync(savedId, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
                await CommandAsync(() => bridge.SelectPath(header.Id)).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    PathRouteResolution reopened = PathRouteResolver.Resolve(editor.TryCaptureCurrentPathModel(), runtime.TrackWorld, TestContext.CancellationToken);
                    Assert.IsTrue(expected.MainRoute.HasSamePhysicalTraversal(reopened.MainRoute));
                    Assert.IsTrue(expected.PassingRoutes.Single().HasSamePhysicalTraversal(reopened.PassingRoutes.Single()));
                    Assert.AreSequenceEqual(disk.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
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

                async Task MapActionAsync(Func<MapContextMenuItem> createItem)
                {
                    await CommandAsync(() =>
                    {
                        MapContextMenuItem entry = createItem();
                        MapContextMenuRequestedEventArgs menu = new MapContextMenuRequestedEventArgs(0, 0, entry.NodeIndex, [entry], scope.Session.ExistingPathEditor);
                        scope.Game.ExecuteMapContextMenuAction(menu, entry);
                    }).ConfigureAwait(false);
                }
            }
        }

        private static (int Split, int Rejoin) GuidedEndpoints(PathEditor editor)
        {
            PathModel model = editor.TryCaptureCurrentPathModel();
            return (model.PathNodes.IndexOf(model.PathNodes.Single(node => node.Location == SyntheticPathFixture.Location(0, 0))),
                model.PathNodes.IndexOf(model.PathNodes.Single(node => node.Location == SyntheticPathFixture.Location(100, 0))));
        }

        private static PathNode GuidedVia() => SyntheticPathFixture.Node(PathNodeType.Via, 5, 50, 100, -1, -1);
    }
}
