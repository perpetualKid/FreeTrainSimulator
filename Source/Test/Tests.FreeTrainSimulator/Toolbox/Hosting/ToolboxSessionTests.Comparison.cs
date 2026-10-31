using System;
using System.Collections.Concurrent;
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
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;
using FreeTrainSimulator.Toolbox.Views;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Graphics.MapView.Widgets;
using Tests.FreeTrainSimulator.Simulator;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenDirtyCachedEditorPathIsComparedThenSavedGeometryIsReadWithoutChangingEditorOrHistory()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, PathModel alternate) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved, alternate]).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));
                PathEditor editor = null;
                PathModel source = null;
                TrainPathBase activeRenderer = null;
                PathModel[] undo = null;
                PathModel[] redo = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    editor = scope.Session.ExistingPathEditor;
                    Assert.IsTrue(editor.SetMetadataCommand("Unsaved name", "Start", "End", false).Success);
                    Assert.IsTrue(editor.SetMetadataCommand("Later name", "Start", "End", false).Success);
                    Assert.IsTrue(editor.Undo());
                    source = editor.TryCaptureCurrentPathModel();
                    activeRenderer = editor.TrainPath;
                    undo = SavePreflightHistory(editor, "undoHistory");
                    redo = SavePreflightHistory(editor, "redoHistory");
                }).ConfigureAwait(false);
                // Deliberately poison the shared extended-model cache; comparison must read the saved file privately.
                PathModel cached = new PathModel(saved) { PathNodes = ImmutableArray<PathNode>.Empty };
                PathCache.Set(saved.Hierarchy(), Task.FromResult<PathModelHeader>(cached));
                try
                {
                    await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(saved.Id, true)).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        Assert.AreSame(editor, scope.Session.ExistingPathEditor);
                        Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                        Assert.AreSame(activeRenderer, editor.TrainPath);
                        Assert.AreSequenceEqual(undo, SavePreflightHistory(editor, "undoHistory"));
                        Assert.AreSequenceEqual(redo, SavePreflightHistory(editor, "redoHistory"));
                        Assert.IsTrue(editor.HasUnsavedChanges);
                        Assert.AreEqual(200f, ComparisonLength(content.SavedPathComparisons.Single()), 0.01f);
                        bridge.RefreshSnapshot();
                        TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                        Assert.AreEqual(saved.Id, snapshot.SelectedPathId);
                        Assert.AreEqual((true, false, true), (snapshot.Comparisons.Single(row => row.Id == saved.Id).IsSelected,
                            snapshot.Comparisons.Single(row => row.Id == saved.Id).IsLoading, snapshot.Paths.Single(row => row.Id == saved.Id).HasUnsavedChanges));
                        Assert.AreEqual("Saved path comparison displayed.", snapshot.CommandResultMessage);
                    }).ConfigureAwait(false);
                }
                finally
                {
                    PathCache.Remove(saved.Hierarchy());
                }
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenSavedPayloadIsNullThenComparisonReportsFailureAndUncheckAndStopRemainSafe()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            string target = ModelFileResolver<PathModelHeader>.FilePath(saved) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            File.WriteAllBytes(target, MemoryPackSerializer.Serialize<PathModel>(null));
            Assert.IsNull(await fixture.Route.PathModelUncached(saved.Id, TestContext.CancellationToken).ConfigureAwait(false));
            // Exercise the sibling header reader without borrowing the fixture's populated path cache.
            PathCache.Remove(saved.Hierarchy());
            Assert.IsNull(await PathModelHandler.GetCore(saved.Id, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false));
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved]).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(saved.Id, true)).ConfigureAwait(false);

                await AssertFailedComparisonCanUncheckAndStopAsync(scope, content, bridge, saved.Id,
                    "The saved path could not be loaded for comparison. Verify the native saved model and access permissions, then try again.").ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenSavedNodeIsNullThenComparisonReportsRepairActionAndUncheckAndStopRemainSafe()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            string target = ModelFileResolver<PathModelHeader>.FilePath(saved) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
            File.WriteAllBytes(target, MemoryPackSerializer.Serialize(saved with { PathNodes = saved.PathNodes.SetItem(1, null) }));
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved]).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(saved.Id, true)).ConfigureAwait(false);

                await AssertFailedComparisonCanUncheckAndStopAsync(scope, content, bridge, saved.Id, SavedPathComparison.UnresolvedMessage).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenComparisonFailsUnexpectedlyThenFailureIsRethrownAfterTerminalOwnerStateAndUncheckAndStopRemainSafe()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            SetField(runtime.TrackWorld, "<SectionGeometry>k__BackingField", null);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved]).ConfigureAwait(false);

                _ = await Assert.ThrowsExactlyAsync<NullReferenceException>(() => scope.Game.InvokeOnGameThreadAsync(
                    () => scope.Session.SetSavedPathComparisonAsync(saved.Id, true))).ConfigureAwait(false);

                await AssertFailedComparisonCanUncheckAndStopAsync(scope, content, bridge, saved.Id,
                    "The saved path could not be loaded for comparison. Verify the native saved model and access permissions, then try again.").ConfigureAwait(false);
            }
        }

        private async Task AssertFailedComparisonCanUncheckAndStopAsync(SessionScope scope, ToolboxContent content, TrainPathToolWindow bridge, string id, string message)
        {
            await scope.Host.OnOwnerAsync(() =>
            {
                bridge.RefreshSnapshot();
                TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                TrainPathComparisonRow row = snapshot.Comparisons.Single();
                Assert.AreEqual((false, false, message), (row.IsSelected, row.IsLoading, row.Error));
                Assert.IsTrue(snapshot.CommandResultIsWarning);
                Assert.IsEmpty(content.SavedPathComparisons);
                Assert.IsNull(scope.Session.ExistingPathEditor);
            }).ConfigureAwait(false);
            await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(id, false)).ConfigureAwait(false);
            await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenSavedFileChangesThenRecheckingComparisonReadsFreshGeometryAndKeepsItsColor()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, PathModel alternate) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved]).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(saved.Id, true)).ConfigureAwait(false);
                SavedPathComparison first = content.SavedPathComparisons.Single();
                PathModel replacement = alternate with { Id = saved.Id };
                _ = await fixture.RoundTripPathAsync(replacement, TestContext.CancellationToken).ConfigureAwait(false);
                PathCache.Set(saved.Hierarchy(), Task.FromResult<PathModelHeader>(saved));
                try
                {
                    await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(saved.Id, true)).ConfigureAwait(false);
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        SavedPathComparison refreshed = content.SavedPathComparisons.Single();
                        Assert.AreNotSame(first, refreshed);
                        Assert.AreEqual(first.Color, refreshed.Color);
                        Assert.AreEqual(400f, ComparisonLength(refreshed), 0.01f);
                        Assert.IsNull(scope.Session.ExistingPathEditor);
                        bridge.RefreshSnapshot();
                        Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                    }).ConfigureAwait(false);
                    // The completed request's CTS has been disposed; deselection must not Cancel it.
                    await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(saved.Id.ToUpperInvariant(), false)).ConfigureAwait(false);
                    Assert.IsEmpty(content.SavedPathComparisons);
                }
                finally
                {
                    PathCache.Remove(saved.Hierarchy());
                }
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenComparisonIsUncheckedBeforeOwnerPublicationThenLateCompletionCannotRestoreIt()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved]).ConfigureAwait(false);
                Task check = null;
                Task uncheck = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    check = scope.Session.SetSavedPathComparisonAsync(saved.Id, true);
                    // No worker can publish on this owner until this callback returns.
                    uncheck = scope.Session.SetSavedPathComparisonAsync(saved.Id, false);
                }).ConfigureAwait(false);
                await Task.WhenAll(check, uncheck).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    TrainPathComparisonRow row = bridge.CaptureTrainPathSnapshot().Comparisons.Single();
                    Assert.AreEqual((false, false, null), (row.IsSelected, row.IsLoading, row.Error));
                    Assert.IsEmpty(content.SavedPathComparisons);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenRouteIsReplacedBeforeComparisonPublicationThenOldGeometryCannotReachReplacement()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved]).ConfigureAwait(false);
                ToolboxContent replacement = new ToolboxContent(new MapContentContext(new MapRuntimeServices(runtime), new JourneyMapSession(), null, null));
                RouteModel route = new RouteModel(SyntheticPathFixture.Location(-50, 0)) { Id = "replacement-" + Guid.NewGuid().ToString("N") };
                route.Initialize(fixture.Route.Parent);
                Task check = null;
                Task unload = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    check = scope.Session.SetSavedPathComparisonAsync(saved.Id, true);
                    unload = scope.Session.ToggleRouteAsync(scope.Game.HostedMenu.Current.Command(
                        new ToolboxMenuKey(((FolderModel)fixture.Route.Parent).Id, fixture.Route.Id, null)));
                    Assert.IsTrue(unload.IsCompletedSuccessfully);
                    SetField(scope.Session, "selectedRoute", route);
                    SetField(scope.Session, "toolboxContent", replacement);
                }).ConfigureAwait(false);
                await Task.WhenAll(check, unload).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    Assert.IsEmpty(content.SavedPathComparisons);
                    Assert.IsEmpty(replacement.SavedPathComparisons);
                    Assert.IsEmpty(bridge.CaptureTrainPathSnapshot().Comparisons);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenSevenSavedPathsAreComparedThenSixDistinctColorsAreReservedAndFreedSlotIsReusable()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            ImmutableArray<PathModelHeader>.Builder paths = ImmutableArray.CreateBuilder<PathModelHeader>();
            for (int i = 0; i < 7; i++)
                paths.Add(await fixture.RoundTripPathAsync(saved with { Id = "palette-" + i, Name = "Saved " + i }, TestContext.CancellationToken).ConfigureAwait(false));
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> callbacks = new ConcurrentQueue<Task>();
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, paths.ToImmutable(), callbacks).ConfigureAwait(false);
                // Use the real bridge callback path, not just session admission.
                await scope.Host.OnOwnerAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    foreach (TrainPathComparisonRow row in bridge.CaptureTrainPathSnapshot().Comparisons)
                        bridge.ComparePath(row, true);
                }).ConfigureAwait(false);
                await Task.WhenAll(callbacks).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    Assert.HasCount(6, content.SavedPathComparisons);
                    Assert.HasCount(6, content.SavedPathComparisons.Select(overlay => overlay.Color).Distinct().ToArray());
                    Assert.IsFalse(bridge.CaptureTrainPathSnapshot().Comparisons.Single(row => row.Id == paths[6].Id).IsSelected);
                }).ConfigureAwait(false);
                // A synchronous refusal after all successes gives authoritative capacity feedback.
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(paths[6].Id, true)).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    Assert.IsTrue(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                    Assert.Contains("six saved paths", bridge.CaptureTrainPathSnapshot().CommandResultMessage);
                }).ConfigureAwait(false);
                Microsoft.Xna.Framework.Color released = content.SavedPathComparisons.Single(overlay => overlay.Id == paths[0].Id).Color;
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(paths[0].Id, false)).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(paths[6].Id, true)).ConfigureAwait(false);
                Assert.AreEqual(released, content.SavedPathComparisons.Single(overlay => overlay.Id == paths[6].Id).Color);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenSavedComparisonCannotBeLoadedThenActionableRefusalHasNoOverlay(bool missingFile)
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            PathModelHeader header = missingFile ? new PathModelHeader { Id = "missing" } :
                await fixture.RoundTripPathAsync(new PathModel(saved) { PathNodes = ImmutableArray<PathNode>.Empty }, TestContext.CancellationToken).ConfigureAwait(false);
            header.Initialize(fixture.Route);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [header]).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(header.Id, true)).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    TrainPathComparisonRow row = snapshot.Comparisons.Single();
                    Assert.AreEqual((false, false, true), (row.IsSelected, row.IsLoading, snapshot.CommandResultIsWarning));
                    Assert.AreEqual(missingFile ? "The saved path could not be loaded for comparison. Verify the native saved model and access permissions, then try again." : SavedPathComparison.UnresolvedMessage, row.Error);
                    Assert.IsEmpty(content.SavedPathComparisons);
                    Assert.IsNull(scope.Session.ExistingPathEditor);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenSelectedComparisonGeometryIsUnavailableThenItReportsRepairActionAndPublishesNoOverlay()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            // Keep the resolved network/anchors intact, but remove the selected vector's prepared drawing geometry.
            SetField(runtime.TrackWorld, "<SegmentSections>k__BackingField", runtime.TrackWorld.SegmentSections.SetItem(4, null));
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved]).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(saved.Id, true)).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    TrainPathComparisonRow row = snapshot.Comparisons.Single();
                    Assert.AreEqual((false, false, SavedPathComparison.MissingGeometryMessage), (row.IsSelected, row.IsLoading, row.Error));
                    Assert.IsTrue(snapshot.CommandResultIsWarning);
                    Assert.IsEmpty(content.SavedPathComparisons);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenSessionStopsWithComparisonOutstandingThenCancellationDrainsWithoutPublishingOrCancelingDisposedSources()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, PathModel alternate) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved, alternate]).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(saved.Id, true)).ConfigureAwait(false);
                Task outstanding = null;
                Task stop = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    outstanding = scope.Session.SetSavedPathComparisonAsync(alternate.Id, true);
                    stop = scope.Session.StopAndDrainAsync();
                }).ConfigureAwait(false);
                await Task.WhenAll(outstanding, stop).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    Assert.IsTrue(scope.Session.IsStopping);
                    Assert.IsEmpty(content.SavedPathComparisons);
                    Assert.IsEmpty(bridge.CaptureTrainPathSnapshot().Comparisons);
                }).ConfigureAwait(false);
                _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scope.Game.InvokeOnGameThreadAsync(
                    () => scope.Session.SetSavedPathComparisonAsync(saved.Id, true))).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenComparedActivePathIsSavedThenBaselineRefreshesFromPersistedModelAndKeepsColor()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved]).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SetSavedPathComparisonAsync(saved.Id, true)).ConfigureAwait(false);
                SavedPathComparison first = content.SavedPathComparisons.Single();
                TrainPathSaveRequest request = null;
                PathModel reversed = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    Assert.IsTrue(scope.Session.ExistingPathEditor.ReversePathCommand().Success);
                    reversed = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel();
                    TrainPathSaveState state = scope.Session.CaptureTrainPathSaveState();
                    request = new TrainPathSaveRequest(state.PathDetails, state.SourcePathId, false);
                }).ConfigureAwait(false);
                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SubmitTrainPathSaveAsync(request)).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                PathModel disk = await JourneyPathStore.ReadPathAsync(saved.Id, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
                Assert.AreEqual((reversed.PathNodes[0].Location, reversed.PathNodes[^1].Location), (disk.PathNodes[0].Location, disk.PathNodes[^1].Location));
                await scope.Host.OnOwnerAsync(() =>
                {
                    Assert.AreSequenceEqual(disk.PathNodes, scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel().PathNodes);
                    SavedPathComparison refreshed = content.SavedPathComparisons.Single();
                    SavedPathComparisonTests.RecordingRenderer expected = new SavedPathComparisonTests.RecordingRenderer();
                    SavedPathComparisonTests.RecordingRenderer actual = new SavedPathComparisonTests.RecordingRenderer();
                    SavedPathComparison.Create(disk, runtime.TrackWorld, first.Color, CancellationToken.None).Draw(expected);
                    refreshed.Draw(actual);
                    Assert.AreNotSame(first, refreshed);
                    Assert.AreEqual(first.Color, refreshed.Color);
                    Assert.AreSequenceEqual(expected.Lines, actual.Lines);
                    Assert.IsFalse(scope.Session.ExistingPathEditor.HasUnsavedChanges);
                    bridge.RefreshSnapshot();
                    Assert.IsTrue(bridge.CaptureTrainPathSnapshot().Comparisons.Single(row => row.Id == saved.Id).IsSelected);
                }).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenComparisonCheckboxIsCheckedAndUncheckedBeforeSnapshotRefreshThenOwnerPublishesNoOverlayAndPreservesDirtyEditor()
        {
            (SyntheticPathFixture fixture, RuntimeDataResolver runtime, PathModel saved, _) = await CreateComparisonModelsAsync().ConfigureAwait(false);
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                (ToolboxContent content, TrainPathToolWindow bridge) = await InitializeComparisonSessionAsync(scope, fixture, runtime, [saved], commands).ConfigureAwait(false);
                Assert.IsTrue(await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(saved)).ConfigureAwait(false));
                PathEditor editor = null;
                PathModel source = null;
                TrainPathBase renderer = null;
                PathModel[] undo = null;
                PathModel[] redo = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    editor = scope.Session.ExistingPathEditor;
                    Assert.IsTrue(editor.SetMetadataCommand("Unsaved name", "Start", "End", false).Success);
                    Assert.IsTrue(editor.SetMetadataCommand("Later name", "Start", "End", false).Success);
                    Assert.IsTrue(editor.Undo());
                    source = editor.TryCaptureCurrentPathModel();
                    renderer = editor.TrainPath;
                    undo = SavePreflightHistory(editor, "undoHistory");
                    redo = SavePreflightHistory(editor, "redoHistory");
                    bridge.RefreshSnapshot();
                    using ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher);
                    using TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler);
                    viewModel.Start();
                    TrainPathToolView view = new TrainPathToolView { DataContext = viewModel };
                    CheckBox toggle = (CheckBox)((DataTemplate)view.Resources["TrainPathComparisonCellTemplate"]).LoadContent();
                    toggle.DataContext = viewModel.Paths.Single();
                    MethodInfo click = typeof(CheckBox).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);

                    click.Invoke(toggle, null);
                    Assert.IsTrue(toggle.IsChecked);
                    click.Invoke(toggle, null);
                    Assert.IsFalse(toggle.IsChecked);
                    Assert.IsFalse(viewModel.Paths.Single().IsCompared);
                    Assert.HasCount(4, commands);
                }).ConfigureAwait(false);
                await Task.WhenAll(commands).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                await scope.Host.OnOwnerAsync(() =>
                {
                    bridge.RefreshSnapshot();
                    Assert.IsEmpty(content.SavedPathComparisons);
                    Assert.IsFalse(bridge.CaptureTrainPathSnapshot().Comparisons.Single().IsSelected);
                    Assert.AreSame(editor, scope.Session.ExistingPathEditor);
                    Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                    Assert.AreSame(renderer, editor.TrainPath);
                    Assert.IsTrue(editor.HasUnsavedChanges);
                    Assert.AreSequenceEqual(undo, SavePreflightHistory(editor, "undoHistory"));
                    Assert.AreSequenceEqual(redo, SavePreflightHistory(editor, "redoHistory"));
                }).ConfigureAwait(false);
            }
        }

        private async Task<(SyntheticPathFixture Fixture, RuntimeDataResolver Runtime, PathModel Saved, PathModel Alternate)> CreateComparisonModelsAsync()
        {
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            TrackWorld previous = TrackWorld.Instance;
            PathModel saved;
            PathModel alternate;
            try
            {
                _ = TrackWorld.Initialize(null, fixture.Track, fixture.Sections);
                saved = await fixture.GenerateSelectedPath(4, TestContext.CancellationToken).ConfigureAwait(false);
                alternate = await fixture.GenerateSelectedPath(5, TestContext.CancellationToken).ConfigureAwait(false);
            }
            finally
            {
                GameService<TrackWorld>.Set(null, previous);
            }
            _ = await RouteModelHandler.ToFile(fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackModelHandler.Create(fixture.Track, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await TrackSectionsModelHandler.Create(fixture.Sections, fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            await SignalConfigurationModelHandler.Create(new SignalConfigurationModel { Id = fixture.Route.Id, Tags = ImmutableDictionary<string, string>.Empty },
                fixture.Route, TestContext.CancellationToken).ConfigureAwait(false);
            RuntimeDataResolver runtime = await RuntimeDataResolver.PrepareAsync(fixture.Route, true, null, TestContext.CancellationToken).ConfigureAwait(false);
            return (fixture, runtime, saved, alternate);
        }

        private static async Task<(ToolboxContent Content, TrainPathToolWindow Bridge)> InitializeComparisonSessionAsync(
            SessionScope scope, SyntheticPathFixture fixture, RuntimeDataResolver runtime, ImmutableArray<PathModelHeader> paths)
            => await InitializeComparisonSessionAsync(scope, fixture, runtime, paths, new ConcurrentQueue<Task>()).ConfigureAwait(false);

        private static async Task<(ToolboxContent Content, TrainPathToolWindow Bridge)> InitializeComparisonSessionAsync(
            SessionScope scope, SyntheticPathFixture fixture, RuntimeDataResolver runtime, ImmutableArray<PathModelHeader> paths, ConcurrentQueue<Task> commands)
        {
            ToolboxContent content = null;
            TrainPathToolWindow bridge = null;
            await scope.Host.OnOwnerAsync(() =>
            {
                SetField(scope.Game, "userCommandController", new UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand>());
                SetField(scope.Game, "pointerInputCapture", Activator.CreateInstance(typeof(GameWindow).GetNestedType("PointerInputCapture", BindingFlags.NonPublic), true));
                SetField(scope.Game, "<Catalog>k__BackingField", new GetText.Catalog());
                content = new ToolboxContent(new MapContentContext(new MapRuntimeServices(runtime), new JourneyMapSession(), null, null));
                SetField(scope.Session, "toolboxContent", content);
                FolderModel folder = (FolderModel)fixture.Route.Parent;
                SetMenuState(scope, folder, [folder], [fixture.Route], fixture.Route);
                SetField(scope.Session, "menuPaths", paths);
                bridge = new TrainPathToolWindow(() => scope.Game.HostedPathEditor, () => scope.Game.HostedTrainPathToolingContext,
                    action => commands.Enqueue(scope.Game.InvokeSessionCommandAsync(action)), scope.Game.EditPath, scope.Game.SavePath,
                    path => commands.Enqueue(scope.Game.LoadPathAsync(path)), () => commands.Enqueue(scope.Game.UnloadPathAsync()),
                    scope.Game.ActivatePathEditingInput, null, (id, selected) =>
                    {
                        Task operation = scope.Session.SetSavedPathComparisonAsync(id, selected);
                        commands.Enqueue(operation);
                        return operation;
                    }) { Active = true };
                SetField(scope.Game, "hostedServices", new HostedToolboxServices { Menu = scope.Game.HostedMenu, TrainPathToolWindow = bridge });
                bridge.UpdatePaths(paths);
                bridge.UpdateComparisons(paths.Select(path => new TrainPathComparisonRow(path.Id, path.Name, 0, false, false, null, System.Drawing.Color.Empty)).ToImmutableArray());
                bridge.RefreshSnapshot();
            }).ConfigureAwait(false);
            return (content, bridge);
        }

        private static float ComparisonLength(SavedPathComparison comparison)
        {
            SavedPathComparisonTests.RecordingRenderer renderer = new SavedPathComparisonTests.RecordingRenderer();
            comparison.Draw(renderer);
            return renderer.Length;
        }
    }
}
