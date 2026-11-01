using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Simulator;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(4)]
        [DataRow(5)]
        public async Task WhenSavedPathIsAppendedThenOneUndoAndNativeSaveAsPreserveBothSources(int selectedVector)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel current, PathModel extension) = await CreateCompositionModelsAsync(selectedVector).ConfigureAwait(false);
            byte[] currentBytes = await File.ReadAllBytesAsync(CompositionFile(current), TestContext.CancellationToken).ConfigureAwait(false);
            byte[] extensionBytes = await File.ReadAllBytesAsync(CompositionFile(extension), TestContext.CancellationToken).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [current, extension], commands).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(current)).ConfigureAwait(false));
                PathModel loaded = null;
                PathCache.Set(extension.Hierarchy(), Task.FromResult<PathModelHeader>(extension with { PathNodes = ImmutableArray<PathNode>.Empty }));
                try
                {
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        PathEditor editor = scope.Session.ExistingPathEditor;
                        loaded = editor.TryCaptureCurrentPathModel();
                        bridge.RefreshSnapshot();
                        TrainPathCompositionSourceRow row = bridge.CaptureTrainPathSnapshot().CompositionSources.Single(path => path.Id != current.Id);
                        Assert.AreEqual(extension.Id, row.Id);
                        Assert.AreEqual(current.Id, bridge.CaptureTrainPathSnapshot().SelectedPathId);
                        Assert.IsTrue(bridge.CaptureTrainPathSnapshot().CanAppendSavedPath);
                        Assert.AreSame(loaded, editor.TryCaptureCurrentPathModel());
                        bridge.SetSavedPathComposition(row, true);
                        bridge.RefreshSnapshot();
                        Assert.IsTrue(bridge.CaptureTrainPathSnapshot().IsAppendingSavedPath);
                        Assert.AreSame(loaded, editor.TryCaptureCurrentPathModel(), "The worker cannot commit inside request admission.");
                    }).ConfigureAwait(false);
                    await Task.WhenAll(commands).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    PathCache.Remove(extension.Hierarchy());
                }
                PathModel composed = null;
                TrainPathSaveRequest saveRequest = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    composed = editor.TryCaptureCurrentPathModel();
                    Assert.AreEqual((current.Id, current.Name, current.Start, extension.End, current.PlayerPath),
                        (composed.Id, composed.Name, composed.Start, composed.End, composed.PlayerPath));
                    Assert.IsTrue(editor.HasUnsavedChanges);
                    Assert.IsFalse(editor.EditMode);
                    Assert.IsTrue(editor.Undo());
                    Assert.AreSequenceEqual(loaded.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                    Assert.IsFalse(editor.CanUndo);
                    Assert.IsTrue(editor.Redo());
                    Assert.AreSequenceEqual(composed.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                    bridge.Active = true;
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    Assert.AreEqual(current.Id, snapshot.SelectedPathId);
                    Assert.AreEqual(extension.End, snapshot.PathEnd);
                    Assert.IsFalse(snapshot.IsAppendingSavedPath);
                    Assert.IsFalse(snapshot.CommandResultIsWarning);
                    Assert.Contains("matching endpoint", snapshot.CommandResultMessage);
                    bridge.RefreshSnapshot();
                    Assert.AreSame(snapshot, bridge.CaptureTrainPathSnapshot());
                    Assert.IsTrue(bridge.CanSavePath);
                    TrainPathSaveState state = scope.Session.CaptureTrainPathSaveState();
                    saveRequest = new TrainPathSaveRequest(state.PathDetails with { Id = "CompositionCopy" + Guid.NewGuid().ToString("N") }, state.SourcePathId, false);
                }).ConfigureAwait(false);
                Assert.AreSequenceEqual(currentBytes, await File.ReadAllBytesAsync(CompositionFile(current), TestContext.CancellationToken).ConfigureAwait(false));
                Assert.AreSequenceEqual(extensionBytes, await File.ReadAllBytesAsync(CompositionFile(extension), TestContext.CancellationToken).ConfigureAwait(false));
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SubmitTrainPathSaveAsync(saveRequest)).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                PathModel disk = await fixture.Route.PathModelUncached(saveRequest.PathDetails.Id, TestContext.CancellationToken).ConfigureAwait(false);
                Assert.AreSequenceEqual(PathRouteResolver.Resolve(composed, runtime.TrackWorld, TestContext.CancellationToken).MainRoute.Spans.SelectMany(span => span.Candidates.Single().PhysicalTraversals).ToArray(),
                    PathRouteResolver.Resolve(disk, runtime.TrackWorld, TestContext.CancellationToken).MainRoute.Spans.SelectMany(span => span.Candidates.Single().PhysicalTraversals).ToArray());
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(null)).ConfigureAwait(false));
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(disk)).ConfigureAwait(false));
                await scope.Host.OnOwnerAsync(() =>
                {
                    Assert.AreSequenceEqual(disk.PathNodes, scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel().PathNodes);
                    Assert.IsFalse(scope.Session.ExistingPathEditor.HasUnsavedChanges);
                    Assert.IsFalse(scope.Session.ExistingPathEditor.CanUndo);
                }).ConfigureAwait(false);
                Assert.AreSequenceEqual(currentBytes, await File.ReadAllBytesAsync(CompositionFile(current), TestContext.CancellationToken).ConfigureAwait(false));
                Assert.AreSequenceEqual(extensionBytes, await File.ReadAllBytesAsync(CompositionFile(extension), TestContext.CancellationToken).ConfigureAwait(false));
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow("edit")]
        [DataRow("edit-undo")]
        [DataRow("cancel")]
        [DataRow("switch")]
        [DataRow("new")]
        [DataRow("stop")]
        [DataRow("placement")]
        [DataRow("read-failure-edit")]
        [DataRow("route")]
        public async Task WhenCompositionIsOutstandingThenInterveningStateCannotBeOverwritten(string action)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel current, PathModel extension) = await CreateCompositionModelsAsync(4).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [current, extension]).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(current)).ConfigureAwait(false));
                Task append = null;
                Task followup = Task.CompletedTask;
                PathModel expected = null;
                PathModel[] undo = null;
                PathModel[] redo = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    PathModel original = editor.TryCaptureCurrentPathModel();
                    long originalRevision = editor.CompositionRevision;
                    if (action == "read-failure-edit")
                        File.Delete(CompositionFile(extension));
                    bridge.RefreshSnapshot();
                    append = scope.Session.SetSavedPathCompositionAsync(bridge.CaptureTrainPathSnapshot().CompositionSources.Single(row => row.Id == extension.Id), true);
                    Assert.IsFalse(append.IsCompleted);
                    switch (action)
                    {
                        case "edit":
                        case "edit-undo":
                        case "read-failure-edit":
                            Assert.IsTrue(editor.SetMetadataCommand("Intervening edit", current.Start, current.End, current.PlayerPath).Success);
                            if (action == "edit-undo")
                            {
                                Assert.IsTrue(editor.Undo());
                                Assert.AreSame(original, editor.TryCaptureCurrentPathModel());
                                Assert.IsGreaterThan(originalRevision, editor.CompositionRevision);
                            }
                            break;
                        case "cancel":
                            followup = scope.Session.SetSavedPathCompositionAsync(null, false);
                            break;
                        case "switch":
                            followup = scope.Session.LoadPathAsync(extension);
                            break;
                        case "new":
                            followup = scope.Session.EditPathAsync();
                            break;
                        case "stop":
                            followup = scope.Session.StopAndDrainAsync();
                            break;
                        case "route":
                            followup = scope.Session.ToggleRouteAsync(scope.Game.HostedMenu.Current.Command(
                            new ToolboxMenuKey(((FolderModel)fixture.Route.Parent).Id, fixture.Route.Id, null)));
                            break;
                        case "placement":
                            Assert.IsTrue(editor.ContinuePathCommand().Success);
                            break;
                    }
                    expected = editor.TryCaptureCurrentPathModel();
                    undo = SavePreflightHistory(editor, "undoHistory");
                    redo = SavePreflightHistory(editor, "redoHistory");
                }).ConfigureAwait(false);
                await Task.WhenAll(append, followup).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    if (action == "route")
                        Assert.IsNull(editor);
                    else if (action == "switch")
                        Assert.AreEqual(extension.Id, editor.TryCaptureCurrentPathModel().Id);
                    else
                    {
                        Assert.AreSame(expected, editor.TryCaptureCurrentPathModel());
                        Assert.AreSequenceEqual(undo, SavePreflightHistory(editor, "undoHistory"));
                        Assert.AreSequenceEqual(redo, SavePreflightHistory(editor, "redoHistory"));
                    }
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    Assert.IsFalse(snapshot.IsAppendingSavedPath);
                    if (action is "edit" or "edit-undo" or "placement" or "read-failure-edit")
                    {
                        Assert.IsTrue(snapshot.CommandResultIsWarning);
                        Assert.Contains("current path changed", snapshot.CommandResultMessage);
                    }
                    if (action is "stop" or "route")
                        Assert.IsEmpty(snapshot.CompositionSources);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow("missing")]
        [DataRow("null")]
        [DataRow("malformed")]
        [DataRow("mismatch")]
        [DataRow("wait")]
        [DataRow("old-row")]
        public async Task WhenCompositionSourceIsUnavailableOrUnsupportedThenModelAndHistoryRemainUnchanged(string condition)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel current, PathModel extension) = await CreateCompositionModelsAsync(4).ConfigureAwait(false);
            switch (condition)
            {
                case "missing":
                    File.Delete(CompositionFile(extension));
                    break;
                case "null":
                    await File.WriteAllBytesAsync(CompositionFile(extension), MemoryPackSerializer.Serialize<PathModel>(null), TestContext.CancellationToken).ConfigureAwait(false);
                    break;
                case "malformed":
                    await File.WriteAllBytesAsync(CompositionFile(extension), [1, 2, 3], TestContext.CancellationToken).ConfigureAwait(false);
                    break;
                case "mismatch":
                    extension = await fixture.RoundTripPathAsync(extension with
                    {
                        PathNodes = extension.PathNodes.SetItem(0, SyntheticPathFixture.Node(PathNodeType.Start, 4, 60, 0, 1, -1)),
                    }, TestContext.CancellationToken).ConfigureAwait(false);
                    break;
                case "wait":
                    extension = await fixture.RoundTripPathAsync(extension with
                    {
                        PathNodes = extension.PathNodes.SetItem(0, extension.PathNodes[0] with { NodeType = PathNodeType.Start | PathNodeType.Wait }),
                    }, TestContext.CancellationToken).ConfigureAwait(false);
                    break;
            }
            byte[] bytes = await File.ReadAllBytesAsync(CompositionFile(current), TestContext.CancellationToken).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [current, extension]).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(current)).ConfigureAwait(false));
                PathModel source = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel();
                await scope.Game.InvokeOnGameThreadAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    TrainPathCompositionSourceRow row = bridge.CaptureTrainPathSnapshot().CompositionSources.Single(item => item.Id == extension.Id);
                    return scope.Session.SetSavedPathCompositionAsync(condition == "old-row" ? row with { RouteContentVersion = row.RouteContentVersion + 1 } : row, true);
                }).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                    Assert.IsFalse(editor.CanUndo);
                    Assert.IsFalse(editor.HasUnsavedChanges);
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    Assert.IsTrue(snapshot.CommandResultIsWarning);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.CommandResultMessage));
                    Assert.IsFalse(snapshot.IsAppendingSavedPath);
                }).ConfigureAwait(false);
                Assert.AreSequenceEqual(bytes, await File.ReadAllBytesAsync(CompositionFile(current), TestContext.CancellationToken).ConfigureAwait(false));
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenNewCompositionSupersedesOutstandingRequestThenOnlyLatestSourceCommits()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel current, PathModel extension) = await CreateCompositionModelsAsync(4).ConfigureAwait(false);
            PathModel latest = await fixture.RoundTripPathAsync(extension with { Id = "LatestExtension" + Guid.NewGuid().ToString("N"), End = "Latest destination" },
                TestContext.CancellationToken).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [current, extension, latest]).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(current)).ConfigureAwait(false));
                Task first = null;
                Task second = null;
                PathModel original = null;
                int updates = 0;
                await scope.Host.OnOwnerAsync(() =>
                {
                    original = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel();
                    scope.Session.ExistingPathEditor.OnPathUpdated += (_, _) => updates++;
                    bridge.RefreshSnapshot();
                    ImmutableArray<TrainPathCompositionSourceRow> rows = bridge.CaptureTrainPathSnapshot().CompositionSources;
                    first = scope.Session.SetSavedPathCompositionAsync(rows.Single(row => row.Id == extension.Id), true);
                    second = scope.Session.SetSavedPathCompositionAsync(rows.Single(row => row.Id == latest.Id), true);
                    Assert.AreSame(original, scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel());
                }).ConfigureAwait(false);
                await Task.WhenAll(first, second).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    Assert.AreEqual(latest.End, editor.TryCaptureCurrentPathModel().End);
                    Assert.AreEqual(1, updates);
                    bridge.RefreshSnapshot();
                    Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                    Assert.IsFalse(bridge.CaptureTrainPathSnapshot().IsAppendingSavedPath);
                    Assert.IsTrue(editor.Undo());
                    Assert.AreSame(original, editor.TryCaptureCurrentPathModel());
                    Assert.IsFalse(editor.CanUndo);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenSavedAppendIsCanceledBeforeSnapshotRefreshThenNoCompositionCommits()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel current, PathModel extension) = await CreateCompositionModelsAsync(4).ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                (_, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [current, extension], commands).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(current)).ConfigureAwait(false));
                PathModel original = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    original = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel();
                    bridge.RefreshSnapshot();
                    bridge.SetSavedPathComposition(bridge.CaptureTrainPathSnapshot().CompositionSources.Single(path => path.Id != current.Id), true);
                    bridge.SetSavedPathComposition(null, false);
                    Assert.HasCount(4, commands);
                }).ConfigureAwait(false);
                await Task.WhenAll(commands).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = scope.Session.ExistingPathEditor;
                    Assert.AreSame(original, editor.TryCaptureCurrentPathModel());
                    Assert.IsFalse(editor.CanUndo);
                    Assert.IsFalse(editor.HasUnsavedChanges);
                    bridge.Active = true;
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    Assert.IsFalse(snapshot.IsAppendingSavedPath);
                    Assert.IsFalse(snapshot.CommandResultIsWarning);
                    Assert.Contains("canceled", snapshot.CommandResultMessage);
                }).ConfigureAwait(false);
            }
        }

        private async Task<(SyntheticPathFixture Fixture, RuntimeDataResolver Runtime, PathModel Current, PathModel Extension)> CreateCompositionModelsAsync(int selectedVector)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, _, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            float seamZ = selectedVector == 5 ? 100 : 0;
            PathModel current = await fixture.RoundTripPathAsync(PathEditor.RefreshValidationState(new PathModel
            {
                Id = "CompositionCurrent" + Guid.NewGuid().ToString("N"),
                Name = "First leg",
                Start = "Origin",
                End = "Join",
                PlayerPath = true,
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, selectedVector, 50, seamZ, -1, -1)),
            }, runtime.TrackWorld), TestContext.CancellationToken).ConfigureAwait(false);
            PathModel extension = await fixture.RoundTripPathAsync(new PathModel
            {
                Id = "CompositionExtension" + Guid.NewGuid().ToString("N"),
                Name = "Second leg",
                Start = "Join",
                End = "Destination",
                PathNodes = ImmutableArray.Create(SyntheticPathFixture.Node(PathNodeType.Start, selectedVector, 50, seamZ, 1, -1),
                    SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1)),
            }, TestContext.CancellationToken).ConfigureAwait(false);
            return (fixture, runtime, current, extension);
        }

        private static string CompositionFile(PathModel model)
            => ModelFileResolver<PathModelHeader>.FilePath(model) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
    }
}
