using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Tests.FreeTrainSimulator.Common;

using EditorTrainPath = FreeTrainSimulator.Graphics.MapView.Widgets.EditorTrainPath;
using JunctionWidget = FreeTrainSimulator.Graphics.MapView.Widgets.JunctionNode;
using TrackSegment = FreeTrainSimulator.Graphics.MapView.Widgets.TrackSegment;

namespace Tests.FreeTrainSimulator.Toolbox.PathEditing
{
    /// <summary>Exercises correlated main-route candidates through the production editor interaction flow.</summary>
    [TestClass]
    [DoNotParallelize]
    [OSCondition(OperatingSystems.Windows)]
    public sealed class PathEditorCorrelatedCandidateTests : IDisposable
    {
        private readonly TrackWorld previousTrackWorld = TrackWorld.Instance;
        private readonly RuntimeDataResolver previousResolver = RuntimeDataResolver.Instance;

        /// <summary>Provides cancellation for asynchronous path initialization.</summary>
        public TestContext TestContext { get; set; }

        private static readonly int[] expectedArray124 = new int[] { 1, 2, 4 };
        private static readonly int[] expectedArray134 = new int[] { 1, 3, 4 };
        private static readonly int[] expectedArray468 = new int[] { 4, 6, 8 };
        private static readonly int[] expectedArray458 = new int[] { 4, 5, 8 };
        private static readonly int[] expectedArray13458 = new int[] { 1, 3, 4, 5, 8 };
        private static readonly int[] expectedArray18 = new int[] { 1, 8 };
        private static readonly int[] expected = new int[] { 1, 4, 2 };

        /// <summary>A real pointer update queues evaluation and cannot publish before owner completion.</summary>
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenGuidedHoverIsQueuedThenPointerReturnsBeforeEvaluationAndOwnerCompletion()
        {
            using (HeldHoverScheduler scheduler = new HeldHoverScheduler())
            {
                GuidedHoverMap map = await CreateGuidedHoverMapAsync(scheduler).ConfigureAwait(false);
                await using (map.ConfigureAwait(false))
                {
                    int updates = 0;
                    map.Editor.OnPathUpdated += (_, e) => updates += e.PreviewResult != null ? 1 : 0;

                    map.MovePointer(LongPassingVia().Location);

                    Assert.AreEqual((1, false, false, PassingBranchAuthoringPhase.PlacingVia, 0),
                        (scheduler.LaunchCount, CaptureHoverTask(map.Editor).IsCompleted, map.Editor.CanCommitPlacement,
                        map.Editor.PassingBranchPhase, updates));
                    Assert.IsNull(CaptureCandidatePreview(map.Editor));
                    Task operation = scheduler.RunNextAsync();
                    HeldHoverCompletion completion = await scheduler.TakeCompletionAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    Assert.IsFalse(operation.IsCompleted, "The admitted task must include its held owner completion.");
                    Assert.IsNull(CaptureCandidatePreview(map.Editor));

                    completion.Apply();
                    await operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken).ConfigureAwait(false);
                    await CaptureHoverTask(map.Editor).WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken).ConfigureAwait(false);

                    Assert.AreEqual((true, PassingBranchAuthoringPhase.ConfirmingVia, 1),
                        (map.Editor.CanCommitPlacement, map.Editor.PassingBranchPhase, updates));
                    Assert.IsTrue(CaptureCandidatePreview(map.Editor).PathNodes.Any(node => node.Location == LongPassingVia().Location && node.NextMainNode < 0));
                }
            }
        }

        private static Task CaptureHoverTask(PathEditor editor)
            => (Task)typeof(PathEditor).GetField("interactivePreviewTask", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);

        /// <summary>A superseded queued launch or held completion cannot overwrite the latest physical via.</summary>
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenOlderGuidedHoverFinishesAfterNewerHoverThenOnlyLatestPreviewAndFeedbackSurvive(bool beforeLaunch)
        {
            using (HeldHoverScheduler scheduler = new HeldHoverScheduler())
            {
                GuidedHoverMap map = await CreateGuidedHoverMapAsync(scheduler).ConfigureAwait(false);
                await using (map.ConfigureAwait(false))
                {
                    TrainPathToolWindow bridge = CreateCandidateBridge(map.Editor);
                    int updates = 0;
                    map.Editor.OnPathUpdated += (_, e) =>
                    {
                        updates++;
                        if (e.PreviewResult != null)
                            bridge.ReportPreviewResult(map.Editor, e.PreviewResult);
                    };
                    PathModel source = map.Editor.TryCaptureCurrentPathModel();
                    PathModel[] undo = CaptureHistory(map.Editor, "undoHistory");
                    map.MovePointer(LongPassingVia().Location);
                    Task olderAdmitted = CaptureHoverTask(map.Editor);
                    Task olderOperation = null;
                    HeldHoverCompletion olderCompletion = null;
                    if (!beforeLaunch)
                    {
                        olderOperation = scheduler.RunNextAsync();
                        olderCompletion = await scheduler.TakeCompletionAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    }
                    WorldLocation latest = new WorldLocation(new Tile(1, 0), new Vector3(75, 0, 100));

                    map.MovePointer(latest);
                    Task newerOperation = scheduler.RunLastAsync();
                    HeldHoverCompletion newerCompletion = await scheduler.TakeCompletionAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    newerCompletion.Apply();
                    await newerOperation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken).ConfigureAwait(false);
                    PathModel preview = CaptureCandidatePreview(map.Editor);
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    Assert.IsTrue(preview.PathNodes.Any(node => node.Location == latest && node.NextMainNode < 0));
                    if (beforeLaunch)
                    {
                        olderOperation = scheduler.RunNextAsync();
                        olderCompletion = await scheduler.TakeCompletionAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    }

                    olderCompletion.Apply();
                    await olderOperation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken).ConfigureAwait(false);
                    await olderAdmitted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken).ConfigureAwait(false);

                    bridge.RefreshSnapshot();
                    Assert.AreSame(preview, CaptureCandidatePreview(map.Editor));
                    Assert.AreSame(source, map.Editor.TryCaptureCurrentPathModel());
                    Assert.IsTrue(PathRouteResolver.Resolve(source, map.World, TestContext.CancellationToken).MainRoute.HasSamePhysicalTraversal(
                        PathRouteResolver.Resolve(preview, map.World, TestContext.CancellationToken).MainRoute));
                    Assert.AreSequenceEqual(undo, CaptureHistory(map.Editor, "undoHistory"));
                    Assert.AreEqual((1, snapshot.CommandResultVersion, snapshot.CommandResultMessage),
                        (updates, bridge.CaptureTrainPathSnapshot().CommandResultVersion, bridge.CaptureTrainPathSnapshot().CommandResultMessage));
                    Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                }
            }
        }

        /// <summary>Invalid current hovers report their actual warning without replacing the last valid branch or via.</summary>
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenCurrentGuidedHoverIsInvalidThenWarningRetainsLastValidPhysicalAnchor(bool offTrack)
        {
            using (HeldHoverScheduler scheduler = new HeldHoverScheduler())
            {
                GuidedHoverMap map = await CreateGuidedHoverMapAsync(scheduler).ConfigureAwait(false);
                await using (map.ConfigureAwait(false))
                {
                    TrainPathToolWindow bridge = CreateCandidateBridge(map.Editor);
                    map.Editor.OnPathUpdated += (_, e) =>
                    {
                        if (e.PreviewResult != null)
                            bridge.ReportPreviewResult(map.Editor, e.PreviewResult);
                    };
                    Assert.IsTrue(map.Editor.PreviewPassingBranchViaCommand(LongPassingVia(), false).Success);
                    PathModel preview = CaptureCandidatePreview(map.Editor);
                    WorldLocation invalid = offTrack
                        ? new WorldLocation(new Tile(1, 0), new Vector3(1000, 0, 1000))
                        : map.Editor.TryCaptureCurrentPathModel().PathNodes[0].Location;
                    map.Content.UpdateItemVisiblity(MapContentType.Tracks, offTrack);

                    map.MovePointer(invalid);
                    if (!offTrack)
                    {
                        Task operation = scheduler.RunNextAsync();
                        HeldHoverCompletion completion = await scheduler.TakeCompletionAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        completion.Apply();
                        await operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken).ConfigureAwait(false);
                    }

                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    Assert.IsTrue(snapshot.CommandResultIsWarning);
                    Assert.AreEqual(offTrack ? "Choose a track location for the passing-branch via."
                        : "The forced passing via cannot form a continuous, physically distinct branch with the unchanged main route.", snapshot.CommandResultMessage);
                    Assert.AreSame(preview, CaptureCandidatePreview(map.Editor));
                    Assert.IsTrue(map.Editor.CanCommitPlacement);
                    Assert.IsTrue(map.Editor.CommitPlacementCommand().Success);
                    Assert.IsTrue(map.Editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.Location == LongPassingVia().Location && node.NextMainNode < 0));
                }
            }
        }

        /// <summary>A retired successful hover cannot overwrite the warning for the current invalid pointer.</summary>
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task WhenOlderGuidedHoverCompletesAfterInvalidPointerThenCurrentWarningAndLastValidPreviewSurvive(bool offTrack, bool beforeLaunch)
        {
            using (HeldHoverScheduler scheduler = new HeldHoverScheduler())
            {
                GuidedHoverMap map = await CreateGuidedHoverMapAsync(scheduler).ConfigureAwait(false);
                await using (map.ConfigureAwait(false))
                {
                    TrainPathToolWindow bridge = CreateCandidateBridge(map.Editor);
                    int updates = 0;
                    map.Editor.OnPathUpdated += (_, e) =>
                    {
                        updates++;
                        if (e.PreviewResult != null)
                            bridge.ReportPreviewResult(map.Editor, e.PreviewResult);
                    };
                    Assert.IsTrue(map.Editor.PreviewPassingBranchViaCommand(LongPassingVia(), false).Success);
                    PathModel preview = CaptureCandidatePreview(map.Editor);
                    map.MovePointer(new WorldLocation(new Tile(1, 0), new Vector3(75, 0, 100)));
                    Task olderAdmitted = CaptureHoverTask(map.Editor);
                    Task olderOperation = null;
                    HeldHoverCompletion olderCompletion = null;
                    if (!beforeLaunch)
                    {
                        olderOperation = scheduler.RunNextAsync();
                        olderCompletion = await scheduler.TakeCompletionAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    }
                    WorldLocation invalid = offTrack ? new WorldLocation(new Tile(1, 0), new Vector3(1000, 0, 1000))
                        : map.Editor.TryCaptureCurrentPathModel().PathNodes[0].Location;
                    map.Content.UpdateItemVisiblity(MapContentType.Tracks, offTrack);

                    map.MovePointer(invalid);
                    if (!offTrack)
                    {
                        Task invalidOperation = scheduler.RunLastAsync();
                        HeldHoverCompletion invalidCompletion = await scheduler.TakeCompletionAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        invalidCompletion.Apply();
                        await invalidOperation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken).ConfigureAwait(false);
                    }
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot warning = bridge.CaptureTrainPathSnapshot();
                    int warningUpdates = updates;
                    Assert.IsTrue(warning.CommandResultIsWarning);
                    if (beforeLaunch)
                    {
                        olderOperation = scheduler.RunNextAsync();
                        olderCompletion = await scheduler.TakeCompletionAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    }

                    olderCompletion.Apply();
                    await olderOperation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken).ConfigureAwait(false);
                    await olderAdmitted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken).ConfigureAwait(false);

                    bridge.RefreshSnapshot();
                    Assert.AreSame(preview, CaptureCandidatePreview(map.Editor));
                    Assert.AreEqual((warningUpdates, warning.CommandResultVersion, warning.CommandResultMessage, true),
                        (updates, bridge.CaptureTrainPathSnapshot().CommandResultVersion, bridge.CaptureTrainPathSnapshot().CommandResultMessage,
                        bridge.CaptureTrainPathSnapshot().CommandResultIsWarning));
                    map.Content.UpdateItemVisiblity(MapContentType.Tracks, true);
                    map.MovePointer(LongPassingVia().Location);
                    bridge.RefreshSnapshot();
                    Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning, "Returning to the known valid anchor reports its cached actual result without relaunch.");
                    Assert.AreEqual(0, scheduler.LaunchCount);
                    Assert.AreSame(preview, CaptureCandidatePreview(map.Editor));
                }
            }
        }

        /// <summary>Retired launches and completions drain but cannot resurrect or report on a newer interaction.</summary>
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow("Confirm", false)]
        [DataRow("Confirm", true)]
        [DataRow("Explicit", false)]
        [DataRow("Explicit", true)]
        [DataRow("ExplicitInvalid", false)]
        [DataRow("ExplicitInvalid", true)]
        [DataRow("Cancel", false)]
        [DataRow("Cancel", true)]
        [DataRow("Draft", false)]
        [DataRow("Draft", true)]
        [DataRow("LoadSame", false)]
        [DataRow("LoadSame", true)]
        [DataRow("LoadOther", false)]
        [DataRow("LoadOther", true)]
        [DataRow("Unload", false)]
        [DataRow("Unload", true)]
        [DataRow("EditUndo", false)]
        [DataRow("EditUndo", true)]
        [DataRow("Stop", false)]
        [DataRow("Stop", true)]
        [DataRow("Owner", false)]
        [DataRow("Owner", true)]
        [DataRow("Mode", false)]
        [DataRow("Mode", true)]
        public async Task WhenGuidedHoverOutlivesItsOwnerThenNewerStateAndFeedbackArePreserved(string replacement, bool beforeLaunch)
        {
            using (HeldHoverScheduler scheduler = new HeldHoverScheduler())
            {
                GuidedHoverMap map = await CreateGuidedHoverMapAsync(scheduler).ConfigureAwait(false);
                await using (map.ConfigureAwait(false))
                {
                    TrainPathToolWindow bridge = CreateQueuedCandidateBridge(() => map.Content.PathEditor as PathEditor, new Queue<Action>());
                    int updates = 0;
                    map.Editor.OnPathUpdated += (_, e) =>
                    {
                        updates++;
                        if (e.PreviewResult != null)
                            bridge.ReportPreviewResult(map.Editor, e.PreviewResult);
                    };
                    Assert.IsTrue(map.Editor.PreviewPassingBranchViaCommand(LongPassingVia(), false).Success);
                    map.MovePointer(new WorldLocation(new Tile(1, 0), new Vector3(75, 0, 100)));
                    Task admitted = CaptureHoverTask(map.Editor);
                    Task operation = null;
                    HeldHoverCompletion completion = null;
                    if (!beforeLaunch)
                    {
                        operation = scheduler.RunNextAsync();
                        completion = await scheduler.TakeCompletionAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    }

                    using PathEditor replacementOwner = await ReplaceHoverOwnerAsync(map, replacement).ConfigureAwait(false);
                    Assert.IsFalse(admitted.IsCompleted, "Cancellation must not prematurely finish the admitted launch/completion.");
                    PathModel source = map.Editor.TryCaptureCurrentPathModel();
                    PathModel preview = CaptureCandidatePreview(map.Editor);
                    object draft = typeof(PathEditor).GetField("pendingGuidedPassingBranch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(map.Editor);
                    long revision = map.Editor.PassingBranchInteractionRevision;
                    ToolboxContentMode mode = map.Content.ContentMode;
                    PathModel[] undo = CaptureHistory(map.Editor, "undoHistory");
                    PathModel[] redo = CaptureHistory(map.Editor, "redoHistory");
                    int baselineUpdates = updates;
                    bridge.RefreshSnapshot();
                    TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                    if (beforeLaunch)
                    {
                        operation = scheduler.RunNextAsync();
                        completion = await scheduler.TakeCompletionAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    }

                    completion.Apply();
                    await operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken).ConfigureAwait(false);
                    await admitted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken).ConfigureAwait(false);

                    bridge.RefreshSnapshot();
                    Assert.AreSame(source, map.Editor.TryCaptureCurrentPathModel());
                    Assert.AreSame(preview, CaptureCandidatePreview(map.Editor));
                    Assert.AreSame(draft, typeof(PathEditor).GetField("pendingGuidedPassingBranch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(map.Editor));
                    Assert.AreSequenceEqual(undo, CaptureHistory(map.Editor, "undoHistory"));
                    Assert.AreSequenceEqual(redo, CaptureHistory(map.Editor, "redoHistory"));
                    Assert.AreEqual((baselineUpdates, revision, mode, snapshot.CommandResultVersion, snapshot.CommandResultMessage),
                        (updates, map.Editor.PassingBranchInteractionRevision, map.Content.ContentMode,
                        bridge.CaptureTrainPathSnapshot().CommandResultVersion, bridge.CaptureTrainPathSnapshot().CommandResultMessage));
                }
            }
        }

        private async Task<PathEditor> ReplaceHoverOwnerAsync(GuidedHoverMap map, string replacement)
        {
            PathEditor editor = map.Editor;
            PathModel source = editor.TryCaptureCurrentPathModel();
            switch (replacement)
            {
                case "Confirm":
                    int undoCount = CaptureHistory(editor, "undoHistory").Length;
                    Assert.IsTrue(editor.CommitPlacementCommand().Success);
                    Assert.AreEqual(undoCount + 1, CaptureHistory(editor, "undoHistory").Length);
                    Assert.IsTrue(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.Location == LongPassingVia().Location && node.NextMainNode < 0));
                    return null;
                case "Explicit":
                    Assert.IsTrue(editor.PreviewPassingBranchViaCommand(LongPassingVia(), false).Success);
                    return null;
                case "ExplicitInvalid":
                    Assert.IsFalse(editor.PreviewPassingBranchViaCommand(source.PathNodes[0], true).Success);
                    return null;
                case "Cancel":
                    Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                    return null;
                case "Draft":
                    Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                    break;
                case "LoadSame":
                    Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                    break;
                case "LoadOther":
                    Assert.IsTrue(await editor.InitializePathAsync(source with { Id = "replacement-hover-source" }, TestContext.CancellationToken).ConfigureAwait(false));
                    break;
                case "Unload":
                    Assert.IsTrue(await editor.InitializePathAsync(null, TestContext.CancellationToken).ConfigureAwait(false));
                    return null;
                case "EditUndo":
                    Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                    Assert.IsTrue(editor.SetMetadataCommand("Replacement hover edit", "Origin", "Destination", false).Success);
                    Assert.IsTrue(editor.Undo());
                    break;
                case "Stop":
                    editor.Stop();
                    return null;
                case "Owner":
                    PathEditor owner = new PathEditor(map.Content);
                    Assert.IsTrue(await owner.InitializePathAsync(source with { Id = "replacement-hover-owner" }, TestContext.CancellationToken).ConfigureAwait(false));
                    return owner;
                case "Mode":
                    map.Content.ContentMode = ToolboxContentMode.ViewRoute;
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(replacement));
            }
            Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);
            Assert.IsTrue(editor.RejoinPassingBranchWithViaCommand(2).Success);
            Assert.IsTrue(editor.PreviewPassingBranchViaCommand(LongPassingVia(), false).Success);
            return null;
        }

        private async Task<GuidedHoverMap> CreateGuidedHoverMapAsync(HeldHoverScheduler scheduler)
        {
            TrackWorld world = CreateLongPassingTrackWorld();
            Hosting.StaControlHost host = await Hosting.StaControlHost.StartAsync().ConfigureAwait(false);
            Game game = null;
            await host.OnOwnerAsync(() => game = new Game()).ConfigureAwait(false);
            GuidedHoverMap map = new GuidedHoverMap(host, game, world, scheduler);
            bool initialized = false;
            try
            {
                await map.Content.Initialize().ConfigureAwait(false);
                Assert.IsTrue(await map.Editor.InitializePathAsync(CreateLongPassingSource(), TestContext.CancellationToken).ConfigureAwait(false));
                PrepareExistingHistory(map.Editor, true);
                Assert.IsTrue(map.Editor.BeginPassingBranchCommand(0).Success);
                Assert.IsTrue(map.Editor.RejoinPassingBranchWithViaCommand(2).Success);
                initialized = true;
                return map;
            }
            finally
            {
                if (!initialized)
                    await map.DisposeAsync().ConfigureAwait(false);
            }
        }

        private sealed class GuidedHoverMap : IAsyncDisposable
        {
            private readonly Hosting.StaControlHost host;
            private readonly Game game;
            private readonly System.Drawing.Font font = new System.Drawing.Font("Arial", 10);

            public ToolboxContent Content { get; }
            public PathEditor Editor { get; }
            public TrackWorld World { get; }

            public GuidedHoverMap(global::Tests.FreeTrainSimulator.Toolbox.Hosting.StaControlHost host, Game game,
                TrackWorld world, HeldHoverScheduler scheduler)
            {
                this.host = host;
                this.game = game;
                World = world;
                PointerMapDevice device = new PointerMapDevice(game, font);
                Content = new ToolboxContent(new MapContentContext(new MapRuntimeServices(RuntimeDataResolver.Instance), device, null, null));
                Editor = new PathEditor(Content, new UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand>(),
                    scheduler.DispatchAsync, commit => Task.FromResult(commit()), scheduler.QueueLaunch);
            }

            public void MovePointer(in WorldLocation location)
            {
                Tile tile = new Tile(1, 0);
                Content.UpdatePointerLocation(PointD.FromWorldLocation(location), tile, tile);
            }

            public async ValueTask DisposeAsync()
            {
                Editor.Dispose();
                Content.Session.Dispose();
                font.Dispose();
                await host.OnOwnerAsync(game.Dispose).ConfigureAwait(false);
                await host.DisposeAsync().ConfigureAwait(false);
            }
        }

        private sealed class HeldHoverScheduler : IDisposable
        {
            private readonly List<(Func<Task> Launch, TaskCompletionSource Completion)> launches = new List<(Func<Task>, TaskCompletionSource)>();
            private readonly ConcurrentQueue<HeldHoverCompletion> completions = new ConcurrentQueue<HeldHoverCompletion>();
            private readonly SemaphoreSlim completionAvailable = new SemaphoreSlim(0);

            public int LaunchCount => launches.Count;

            public Task QueueLaunch(Func<Task> launch)
            {
                TaskCompletionSource completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                launches.Add((launch, completion));
                return completion.Task;
            }

            public Task RunNextAsync() => RunAtAsync(0);

            public Task RunLastAsync() => RunAtAsync(launches.Count - 1);

            private Task RunAtAsync(int index)
            {
                (Func<Task> launch, TaskCompletionSource completion) = launches[index];
                launches.RemoveAt(index);
                Task operation = launch();
                _ = operation.ContinueWith(completed =>
                {
                    if (completed.IsFaulted)
                        completion.SetException(completed.Exception.InnerExceptions);
                    else if (completed.IsCanceled)
                        completion.SetCanceled();
                    else
                        completion.SetResult();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                return operation;
            }

            public Task DispatchAsync(Action action)
            {
                HeldHoverCompletion completion = new HeldHoverCompletion(action);
                completions.Enqueue(completion);
                completionAvailable.Release();
                return completion.Task;
            }

            public async Task<HeldHoverCompletion> TakeCompletionAsync(CancellationToken cancellationToken)
            {
                Assert.IsTrue(await completionAvailable.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false));
                Assert.IsTrue(completions.TryDequeue(out HeldHoverCompletion completion));
                return completion;
            }

            public void Dispose() => completionAvailable.Dispose();
        }

        private sealed class HeldHoverCompletion
        {
            private readonly Action action;
            private readonly TaskCompletionSource completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Task => completion.Task;

            public HeldHoverCompletion(Action action) => this.action = action;

            public void Apply()
            {
                action();
                completion.SetResult();
            }
        }

        /// <summary>Real map pointer updates preview a private branch and restore viewing on cancel or confirmation.</summary>
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task WhenLoadedViewPathReceivesGuidedMapPointerThenPlacementEndsInSourceMode(bool tied, bool confirm)
        {
            TrackWorld world = tied ? CreateCorrelatedTrackWorld(true) : CreateLongPassingTrackWorld();
            PathModel source = tied ? CreatePassingSource(world) : CreateLongPassingSource();
            Hosting.StaControlHost host = await Hosting.StaControlHost.StartAsync().ConfigureAwait(false);
            await using (host.ConfigureAwait(false))
            {
                Game game = null;
                await host.OnOwnerAsync(() => game = new Game()).ConfigureAwait(false);
                try
                {
                    using System.Drawing.Font font = new System.Drawing.Font("Arial", 10);
                    PointerMapDevice device = new PointerMapDevice(game, font);
                    ToolboxContent content = new ToolboxContent(new MapContentContext(new MapRuntimeServices(RuntimeDataResolver.Instance),
                        device, null, null));
                    using (content.Session)
                    {
                        using (PathEditor editor = new PathEditor(content))
                        {
                            await content.Initialize().ConfigureAwait(false);
                            Assert.IsInstanceOfType<XnaMapSession>(content.Session);
                            Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                            PrepareExistingHistory(editor, true);
                            source = editor.TryCaptureCurrentPathModel();
                            PathModel[] undo = CaptureHistory(editor, "undoHistory");
                            PathModel[] redo = CaptureHistory(editor, "redoHistory");
                            TrainPathBase rendered = editor.TrainPath;
                            Assert.AreEqual((false, ToolboxContentMode.ViewPath), (editor.EditMode, content.ContentMode));
                            Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);
                            Assert.IsTrue(editor.RejoinPassingBranchWithViaCommand(2).Success);
                            // The tied fixture has overlapping collinear vectors; a visible junction supplies the forced via.
                            content.UpdateItemVisiblity(MapContentType.Tracks, !tied);
                            PointD pointer = tied ? PointD.FromWorldLocation(world.TrackDatabase.TrackNodes[4].Location)
                                : PointD.FromWorldLocation(LongPassingVia().Location);
                            Tile tile = new Tile(1, 0);

                            Assert.AreEqual(ToolboxContentMode.EditPath, content.ContentMode);
                            content.UpdatePointerLocation(pointer, tile, tile);
                            await ((Task)typeof(PathEditor).GetField("interactivePreviewTask", BindingFlags.Instance | BindingFlags.NonPublic)
                                .GetValue(editor)).WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken).ConfigureAwait(false);

                            Assert.AreEqual(PassingBranchAuthoringPhase.ConfirmingVia, editor.PassingBranchPhase);
                            Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                            Assert.AreSame(rendered, editor.TrainPath);
                            Assert.AreSequenceEqual(undo, CaptureHistory(editor, "undoHistory"));
                            Assert.AreSequenceEqual(redo, CaptureHistory(editor, "redoHistory"));
                            Assert.AreEqual((false, true, ToolboxContentMode.EditPath), (editor.EditMode, editor.HasUnsavedChanges, content.ContentMode));
                            PathModel preview = (PathModel)typeof(PathEditorBase).GetProperty("PreviewPathModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                            Assert.IsNotNull(preview);
                            Assert.IsTrue(PathRouteResolver.Resolve(source, world, TestContext.CancellationToken).MainRoute.HasSamePhysicalTraversal(
                                PathRouteResolver.Resolve(preview, world, TestContext.CancellationToken).MainRoute));

                            if (!confirm)
                            {
                                Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                                Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                                Assert.AreSequenceEqual(undo, CaptureHistory(editor, "undoHistory"));
                                Assert.AreSequenceEqual(redo, CaptureHistory(editor, "redoHistory"));
                                Assert.IsNull(typeof(PathEditorBase).GetProperty("ActivePathPoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
                            }
                            else
                            {
                                Assert.IsTrue(editor.CommitPlacementCommand().Success);
                                Assert.IsNull(typeof(PathEditorBase).GetProperty("ActivePathPoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
                                if (tied)
                                {
                                    Assert.AreEqual(PassingBranchAuthoringPhase.SelectingCandidate, editor.PassingBranchPhase);
                                    Assert.AreEqual(ToolboxContentMode.ViewPath, content.ContentMode);
                                    Assert.AreSame(preview, typeof(PathEditorBase).GetProperty("PreviewPathModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor));
                                    Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                                    Assert.AreSequenceEqual(undo, CaptureHistory(editor, "undoHistory"));
                                    Assert.AreSequenceEqual(redo, CaptureHistory(editor, "redoHistory"));
                                    ResolvedPathSpan first = editor.GetAmbiguousSpans()[0];
                                    Assert.IsTrue(editor.PreviewRouteCandidateCommand(first.FromNodeIndex, 0).Success);
                                    Assert.IsTrue(editor.AcceptRouteCandidateCommand(first.FromNodeIndex, 0).Success);
                                    ResolvedPathSpan remaining = editor.GetAmbiguousSpans().Single();
                                    Assert.IsTrue(editor.PreviewRouteCandidateCommand(remaining.FromNodeIndex, 0).Success);
                                    Assert.IsTrue(editor.AcceptRouteCandidateCommand(remaining.FromNodeIndex, 0).Success);
                                }
                                Assert.AreEqual(undo.Length + 1, CaptureHistory(editor, "undoHistory").Length);
                                Assert.IsEmpty(CaptureHistory(editor, "redoHistory"));
                            }
                            Assert.AreEqual((false, ToolboxContentMode.ViewPath, PassingBranchAuthoringPhase.Idle),
                                (editor.EditMode, content.ContentMode, editor.PassingBranchPhase));
                        }
                    }
                }
                finally
                {
                    await host.OnOwnerAsync(game.Dispose).ConfigureAwait(false);
                }
            }
        }

        /// <summary>Device-only composition follows the existing ownership fixture; all map adapters stay real.</summary>
        private sealed class PointerMapDevice : IMapSessionComposer, IMapHostSessionFactory, IMapHostAdapterBundle, IMapHostEnvironment
        {
            private readonly Game game;
            public IMapViewStateAdapter ViewStateAdapter { get; }
            public IMapRenderAdapter RenderAdapter => null;
            public IMapInteractionAdapter InteractionAdapter { get; }
            public IMapOverlayShapeAdapter OverlayShapeAdapter => null;
            public IMapHostResources HostResources { get; } = new XnaMapHostResources(null, null, null, null);
            public Point ClientSize => new Point(800, 600);
            public Point PointerPosition => Point.Zero;
            public event EventHandler ClientSizeChanged { add { } remove { } }

            public PointerMapDevice(Game game, System.Drawing.Font font)
            {
                this.game = game;
                MapViewController controller = new MapViewController(new MapViewportBounds(0, 0, 100, 100));
                MapViewStateAdapter viewState = new MapViewStateAdapter(controller, font);
                ViewStateAdapter = viewState;
                InteractionAdapter = new MapInteractionAdapter(new FontManagerInstance("Arial", System.Drawing.FontStyle.Regular, 1), controller, this, viewState);
            }

            public IMapHostSession Create(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent)
                => new XnaMapHostSession(this);

            public IMapSession Compose(MapSessionRequest request)
            {
                PointerMapShell shell = new PointerMapShell(game, request.Content, this);
                try
                {
                    IMapSession session = new XnaMapSession(shell);
                    shell = null;
                    return session;
                }
                finally
                {
                    shell?.Dispose();
                }
            }

            public void RegisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler) { }
            public void UnregisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler) { }
        }

        private sealed class PointerMapShell : ContentArea
        {
            public PointerMapShell(Game game, ContentBase content, IMapHostSessionFactory factory) : base(game, content, null, factory) { }

            // No GraphicsDevice is acquired; content initialization and pointer routing remain production code.
            public override void Initialize() => ResetSize(new Point(800, 600), 0);
        }

        /// <summary>Restores process-wide services after each case, including failed fixture setup.</summary>
        public void Dispose()
        {
            GameService<RuntimeDataResolver>.Set(null, previousResolver);
            GameService<TrackWorld>.Set(null, previousTrackWorld);
            GC.SuppressFinalize(this);
        }

        /// <summary>The guided public workflow selects a 300m route absent from the cheapest 100m branch candidates.</summary>
        [TestMethod]
        public async Task WhenGuidedViaForcesLongerBranchThenConfirmationCommitsOneUndoableEdit()
        {
            TrackWorld world = CreateLongPassingTrackWorld();
            PathModel source = CreateLongPassingSource();
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                PathRouteResolution baseline = PathRouteResolver.Resolve(PathModelEditor.CreatePassingBranch(source, 0, 2).PathModel, world, TestContext.CancellationToken);
                Assert.IsTrue(baseline.IsValid);
                Assert.HasCount(1, baseline.PassingRoutes[0].Spans[0].Candidates);
                Assert.AreEqual(100.0, world.VectorNodeLength((VectorNode)world.TrackDatabase.TrackNodes[4]), 1e-6);
                Assert.AreEqual(300.0, world.VectorNodeLength((VectorNode)world.TrackDatabase.TrackNodes[5]), 1e-6);
                Assert.AreSequenceEqual(expected, baseline.PassingRoutes[0].Spans[0].Candidates.Single().RouteNodeIndexes.ToArray());
                Assert.DoesNotContain(5, baseline.PassingRoutes[0].Spans[0].Candidates.Single().RouteNodeIndexes);
                Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);
                Assert.IsTrue(editor.RejoinPassingBranchWithViaCommand(2).Success);
                Assert.AreEqual(PassingBranchAuthoringPhase.PlacingVia, editor.PassingBranchPhase);

                PathEditorCommandResult preview = editor.PreviewPassingBranchViaCommand(LongPassingVia(), false);

                Assert.IsTrue(preview.Success, preview.Message);
                Assert.AreEqual(PassingBranchAuthoringPhase.ConfirmingVia, editor.PassingBranchPhase);
                AssertSourceUnchanged(editor, source);
                Assert.IsFalse(editor.ValidateCurrentPathForPersistence().PersistenceAllowed);
                PathEditResult committed = editor.CommitPlacement();
                Assert.IsTrue(committed.Success, committed.Message);
                Assert.AreEqual(PassingBranchAuthoringPhase.Idle, editor.PassingBranchPhase);
                PathModel result = editor.TryCaptureCurrentPathModel();
                PathRouteResolution resolved = PathRouteResolver.Resolve(result, world, TestContext.CancellationToken);
                Assert.IsTrue(resolved.IsValid);
                Assert.IsTrue(resolved.PassingRoutes[0].Spans.SelectMany(span => span.Candidates[0].TrackVectorNodeIndexes).Contains(5));
                Assert.IsTrue(PathRouteResolver.Resolve(source, world, TestContext.CancellationToken).MainRoute.HasSamePhysicalTraversal(resolved.MainRoute));
                Assert.IsTrue(result.PathNodes.Any(node => node.Location == LongPassingVia().Location && node.NextMainNode < 0));
                Assert.IsTrue(editor.Undo());
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.AreEqual(source.Id, editor.TryCaptureCurrentPathModel().Id);
                Assert.IsFalse(editor.CanUndo);
                Assert.IsTrue(editor.Redo());
                Assert.AreSequenceEqual(result.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.IsTrue(resolved.PassingRoutes[0].HasSamePhysicalTraversal(PathRouteResolver.Resolve(editor.TryCaptureCurrentPathModel(), world, TestContext.CancellationToken).PassingRoutes[0]));
            }
        }

        private static PathNode LongPassingVia() => new PathNode(new WorldLocation(new Tile(1, 0), new Vector3(50, 0, 100)))
        {
            NodeIndex = 5,
        };

        /// <summary>Every guided phase can be canceled without publishing a model or changing existing history.</summary>
        [TestMethod]
        [DataRow(0, false)]
        [DataRow(1, false)]
        [DataRow(2, false)]
        [DataRow(3, false)]
        [DataRow(0, true)]
        [DataRow(1, true)]
        [DataRow(2, true)]
        [DataRow(3, true)]
        public async Task WhenGuidedPhaseIsCanceledThenSourceAndExistingHistoryArePreserved(int phase, bool withHistory)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(CreatePassingSource(world), TestContext.CancellationToken).ConfigureAwait(false));
                PrepareExistingHistory(editor, withHistory);
                PathModel source = editor.TryCaptureCurrentPathModel();
                PathModel[] undo = CaptureHistory(editor, "undoHistory");
                PathModel[] redo = CaptureHistory(editor, "redoHistory");
                BeginGuidedPhase(editor, world, phase);

                CreateCandidateBridge(editor).CancelPathInteraction();

                Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual((PassingBranchAuthoringPhase.Idle, PathEditorPlacementMode.None, withHistory), (editor.PassingBranchPhase, editor.PlacementMode, editor.HasUnsavedChanges));
                Assert.AreSequenceEqual(undo, CaptureHistory(editor, "undoHistory"));
                Assert.AreSequenceEqual(redo, CaptureHistory(editor, "redoHistory"));
            }
        }

        /// <summary>Guided drafts block persistence and unrelated public editing commands in every phase.</summary>
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        public async Task WhenGuidedPhaseIsActiveThenSaveAndUnrelatedCommandsAreExclusive(int phase)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            PathModel source = CreatePassingSource(world);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginGuidedPhase(editor, world, phase);
                PassingBranchAuthoringPhase expectedPhase = editor.PassingBranchPhase;

                Assert.IsFalse(editor.ValidateCurrentPathForPersistence().PersistenceAllowed);
                Assert.IsFalse(editor.SetMetadataCommand("Unrelated", "Start", "End", false).Success);
                Assert.IsFalse(editor.BeginMoveNodeCommand(1).Success);
                Assert.IsFalse(editor.SetWaitPointCommand(1, 30).Success);
                Assert.IsFalse(editor.SetReversalPointCommand(1).Success);
                Assert.IsFalse(editor.SetStartAnchorCommand(source.PathNodes[0], true).Success);
                Assert.IsFalse(editor.ReResolvePathCommand().Success);
                Assert.IsFalse(editor.ContinuePathCommand().Success);
                Assert.IsFalse(editor.Undo());
                Assert.IsFalse(editor.Redo());
                Assert.AreEqual(expectedPhase, editor.PassingBranchPhase);
                AssertSourceUnchanged(editor, source);
            }
        }

        /// <summary>Accepting an upstream leg must not implicitly accept its compatible suffix.</summary>
        [TestMethod]
        [DataRow(0, new int[] { 1, 2, 4, 6, 8 })]
        [DataRow(1, new int[] { 1, 3, 4, 5, 8 })]
        public async Task WhenGuidedCorrelatedLegIsAcceptedThenSuffixRemainsPreviewUntilExplicitAcceptance(int candidateIndex, int[] expected)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            PathModel source = CreatePassingSource(world);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginGuidedPhase(editor, world, 3);
                ImmutableArray<ResolvedPathSpan> initial = editor.GetAmbiguousSpans();
                Assert.HasCount(2, initial);
                Assert.HasCount(2, initial[0].Candidates);
                Assert.HasCount(2, initial[1].Candidates);

                PathEditorCommandResult upstream = editor.AcceptRouteCandidateCommand(initial[0].FromNodeIndex, candidateIndex);

                Assert.IsTrue(upstream.Success, upstream.Message);
                AssertSourceUnchanged(editor, source);
                Assert.AreEqual(PassingBranchAuthoringPhase.SelectingCandidate, editor.PassingBranchPhase);
                ResolvedPathSpan suffix = editor.GetAmbiguousSpans().Single();
                Assert.AreEqual(initial[1].FromNodeIndex, suffix.FromNodeIndex);
                Assert.HasCount(1, suffix.Candidates);
                Assert.IsTrue(editor.PreviewRouteCandidateCommand(suffix.FromNodeIndex, 0).Success);
                AssertSourceUnchanged(editor, source);
                PathEditorCommandResult accepted = editor.AcceptRouteCandidateCommand(suffix.FromNodeIndex, 0);
                Assert.IsTrue(accepted.Success, accepted.Message);
                PathModel result = editor.TryCaptureCurrentPathModel();
                PathRouteResolution resolved = PathRouteResolver.Resolve(result, world, TestContext.CancellationToken);
                Assert.IsTrue(resolved.IsValid);
                AssertJunctionRoute(expected, resolved.PassingRoutes.Single().Spans.SelectMany(span => span.Candidates.Single().RouteNodeIndexes).Distinct());
                Assert.IsTrue(PathRouteResolver.Resolve(source, world, TestContext.CancellationToken).MainRoute.HasSamePhysicalTraversal(resolved.MainRoute));
                Assert.IsTrue(editor.Undo());
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.IsFalse(editor.CanUndo);
                Assert.IsTrue(editor.Redo());
                Assert.AreSequenceEqual(result.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.IsTrue(resolved.PassingRoutes.Single().HasSamePhysicalTraversal(PathRouteResolver.Resolve(editor.TryCaptureCurrentPathModel(), world, TestContext.CancellationToken).PassingRoutes.Single()));
            }
        }

        /// <summary>Unseeded keyboard cycles preview each remaining guided leg without accepting it or changing history.</summary>
        [TestMethod]
        [DataRow(1, 0, new int[] { 1, 2, 4, 6, 8 })]
        [DataRow(-1, 1, new int[] { 1, 3, 4, 5, 8 })]
        public async Task WhenGuidedCandidatesAreCycledFromRejoinThenEachLegRequiresExplicitAcceptance(int direction, int candidateIndex, int[] expected)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(CreatePassingSource(world), TestContext.CancellationToken).ConfigureAwait(false));
                PrepareExistingHistory(editor, true);
                PathModel source = editor.TryCaptureCurrentPathModel();
                PathModel[] undo = CaptureHistory(editor, "undoHistory");
                PathModel[] redo = CaptureHistory(editor, "redoHistory");
                BeginGuidedPhase(editor, world, 3);
                editor.SelectAuthoredNode(2);
                ImmutableArray<ResolvedPathSpan> initial = editor.GetAmbiguousSpans();
                Assert.HasCount(2, initial);
                Assert.HasCount(2, initial[0].Candidates);
                Assert.AreEqual((2, -1, -1), (editor.SelectedPathNodeIndex, editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
                Assert.IsFalse(editor.AcceptPreviewedRouteCandidateCommand().Success);
                Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                Assert.AreSequenceEqual(undo, CaptureHistory(editor, "undoHistory"));
                Assert.AreSequenceEqual(redo, CaptureHistory(editor, "redoHistory"));

                PathEditorCommandResult upstreamCycle = editor.CycleRouteCandidateCommand(direction);

                Assert.IsTrue(upstreamCycle.Success, upstreamCycle.Message);
                Assert.AreEqual((initial[0].FromNodeIndex, candidateIndex), (editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
                PathRouteResolution upstreamPreview = PathRouteResolver.Resolve(CaptureCandidatePreview(editor), world, TestContext.CancellationToken);
                Assert.IsTrue(upstreamPreview.IsValid);
                AssertJunctionRoute(expected, upstreamPreview.PassingRoutes.Single().Spans.SelectMany(span => span.Candidates.Single().RouteNodeIndexes).Distinct());
                Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                Assert.AreSequenceEqual(undo, CaptureHistory(editor, "undoHistory"));
                Assert.AreSequenceEqual(redo, CaptureHistory(editor, "redoHistory"));
                Assert.HasCount(2, editor.GetAmbiguousSpans());

                PathEditorCommandResult upstreamAcceptance = editor.AcceptPreviewedRouteCandidateCommand();

                Assert.IsTrue(upstreamAcceptance.Success, upstreamAcceptance.Message);
                Assert.AreEqual(PassingBranchAuthoringPhase.SelectingCandidate, editor.PassingBranchPhase);
                Assert.AreEqual((2, -1, -1), (editor.SelectedPathNodeIndex, editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
                ResolvedPathSpan suffix = editor.GetAmbiguousSpans().Single();
                Assert.AreEqual(initial[1].FromNodeIndex, suffix.FromNodeIndex);
                Assert.HasCount(1, suffix.Candidates);
                Assert.IsFalse(editor.AcceptPreviewedRouteCandidateCommand().Success);
                Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                Assert.AreSequenceEqual(undo, CaptureHistory(editor, "undoHistory"));
                Assert.AreSequenceEqual(redo, CaptureHistory(editor, "redoHistory"));

                PathEditorCommandResult suffixCycle = editor.CycleRouteCandidateCommand(direction);

                Assert.IsTrue(suffixCycle.Success, suffixCycle.Message);
                Assert.AreEqual((suffix.FromNodeIndex, 0), (editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
                PathRouteResolution suffixPreview = PathRouteResolver.Resolve(CaptureCandidatePreview(editor), world, TestContext.CancellationToken);
                Assert.IsTrue(suffixPreview.IsValid);
                Assert.IsTrue(upstreamPreview.PassingRoutes.Single().HasSamePhysicalTraversal(suffixPreview.PassingRoutes.Single()));
                Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                Assert.AreSequenceEqual(undo, CaptureHistory(editor, "undoHistory"));
                Assert.AreSequenceEqual(redo, CaptureHistory(editor, "redoHistory"));
                Assert.AreEqual(PassingBranchAuthoringPhase.SelectingCandidate, editor.PassingBranchPhase);

                PathEditorCommandResult accepted = editor.AcceptPreviewedRouteCandidateCommand();

                Assert.IsTrue(accepted.Success, accepted.Message);
                PathRouteResolution committed = PathRouteResolver.Resolve(editor.TryCaptureCurrentPathModel(), world, TestContext.CancellationToken);
                Assert.IsTrue(committed.IsValid);
                AssertJunctionRoute(expected, committed.PassingRoutes.Single().Spans.SelectMany(span => span.Candidates.Single().RouteNodeIndexes).Distinct());
                Assert.IsTrue(suffixPreview.PassingRoutes.Single().HasSamePhysicalTraversal(committed.PassingRoutes.Single()));
                Assert.IsTrue(PathRouteResolver.Resolve(source, world, TestContext.CancellationToken).MainRoute.HasSamePhysicalTraversal(committed.MainRoute));
                Assert.AreEqual((PassingBranchAuthoringPhase.Idle, -1, -1), (editor.PassingBranchPhase, editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
                Assert.AreSequenceEqual(new PathModel[] { source }.Concat(undo).ToArray(), CaptureHistory(editor, "undoHistory"));
                Assert.IsEmpty(CaptureHistory(editor, "redoHistory"));
            }
        }

        /// <summary>Both queued previews remain admitted while the correlated candidate structure is unchanged.</summary>
        [TestMethod]
        [DataRow(0, 1, new int[] { 1, 3, 4, 5, 8 })]
        [DataRow(1, 0, new int[] { 1, 2, 4, 6, 8 })]
        public async Task WhenGuidedCandidatePreviewsAreQueuedTogetherThenBothSucceedAndLastTraversalIsPublished(int first, int last, int[] expected)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            PathModel source = CreatePassingSource(world);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginGuidedPhase(editor, world, 3);
                int fromNodeIndex = editor.GetAmbiguousSpans()[0].FromNodeIndex;
                Queue<Action> commands = new Queue<Action>();
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => editor, () => null, commands.Enqueue, () => { }, () => { }, _ => { }, () => { }, () => { }) { Active = true };
                long revision = editor.PassingBranchInteractionRevision;

                bridge.PreviewRouteCandidate(fromNodeIndex, first);
                bridge.PreviewRouteCandidate(fromNodeIndex, last);
                Assert.HasCount(2, commands);
                commands.Dequeue()();
                bridge.RefreshSnapshot();
                Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                Assert.AreEqual(first, bridge.CaptureTrainPathSnapshot().PreviewedRouteCandidateIndex);
                commands.Dequeue()();
                bridge.RefreshSnapshot();

                TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
                Assert.IsFalse(snapshot.CommandResultIsWarning, snapshot.CommandResultMessage);
                Assert.AreEqual((fromNodeIndex, last), (snapshot.PreviewedRouteCandidateFromNodeIndex, snapshot.PreviewedRouteCandidateIndex));
                Assert.AreEqual(revision + 2, editor.PassingBranchInteractionRevision);
                PathModel preview = (PathModel)typeof(PathEditorBase).GetProperty("PreviewPathModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                AssertJunctionRoute(expected, PathRouteResolver.Resolve(preview, world, TestContext.CancellationToken).PassingRoutes.Single().Spans.SelectMany(span => span.Candidates.Single().RouteNodeIndexes).Distinct());
                AssertSourceUnchanged(editor, source);
            }
        }

        /// <summary>Queued cycles advance from the latest physical preview rather than the selection at capture.</summary>
        [TestMethod]
        [DataRow(0, 1)]
        [DataRow(1, -1)]
        public async Task WhenGuidedCandidateCyclesAreQueuedTogetherThenBothAdvanceTheCurrentPreview(int initial, int direction)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            PathModel source = CreatePassingSource(world);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginGuidedPhase(editor, world, 3);
                int fromNodeIndex = editor.GetAmbiguousSpans()[0].FromNodeIndex;
                Assert.IsTrue(editor.PreviewRouteCandidateCommand(fromNodeIndex, initial).Success);
                PathModel initialPreview = CaptureCandidatePreview(editor);
                Queue<Action> commands = new Queue<Action>();
                TrainPathToolWindow bridge = CreateQueuedCandidateBridge(() => editor, commands);
                long revision = editor.PassingBranchInteractionRevision;

                bridge.CycleRouteCandidate(direction);
                bridge.CycleRouteCandidate(direction);
                commands.Dequeue()();
                bridge.RefreshSnapshot();
                Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                Assert.AreEqual(1 - initial, bridge.CaptureTrainPathSnapshot().PreviewedRouteCandidateIndex);
                commands.Dequeue()();
                bridge.RefreshSnapshot();

                Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                Assert.AreEqual((fromNodeIndex, initial), (bridge.CaptureTrainPathSnapshot().PreviewedRouteCandidateFromNodeIndex,
                    bridge.CaptureTrainPathSnapshot().PreviewedRouteCandidateIndex));
                Assert.AreEqual(revision + 2, editor.PassingBranchInteractionRevision);
                Assert.IsTrue(PathRouteResolver.Resolve(initialPreview, world, TestContext.CancellationToken).PassingRoutes.Single().HasSamePhysicalTraversal(
                    PathRouteResolver.Resolve(CaptureCandidatePreview(editor), world, TestContext.CancellationToken).PassingRoutes.Single()));
                AssertSourceUnchanged(editor, source);
            }
        }

        /// <summary>Polling between queued WPF picks must not leave the earlier selection on the map or in the view model.</summary>
        [TestMethod]
        [DataRow(0, 1, new int[] { 1, 3, 4, 5, 8 })]
        [DataRow(1, 0, new int[] { 1, 2, 4, 6, 8 })]
        public async Task WhenWpfGuidedCandidatePicksAreQueuedTogetherThenLastPhysicalPreviewAndSelectionAgree(int first, int last, int[] expected)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            PathModel source = CreatePassingSource(world);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginGuidedPhase(editor, world, 3);
                int fromNodeIndex = editor.GetAmbiguousSpans()[0].FromNodeIndex;
                Queue<Action> commands = new Queue<Action>();
                TrainPathToolWindow bridge = CreateQueuedCandidateBridge(() => editor, commands);
                bridge.RefreshSnapshot();
                using (ToolWindowRefreshScheduler scheduler = new ToolWindowRefreshScheduler(Dispatcher.CurrentDispatcher))
                {
                    using (TrainPathToolWindowViewModel viewModel = new TrainPathToolWindowViewModel(bridge, scheduler))
                    {
                        viewModel.Start();

                        viewModel.UserSelectRouteCandidate(viewModel.RouteCandidates.Single(row => row.FromNodeIndex == fromNodeIndex && row.CandidateIndex == first));
                        viewModel.UserSelectRouteCandidate(viewModel.RouteCandidates.Single(row => row.FromNodeIndex == fromNodeIndex && row.CandidateIndex == last));
                        Assert.HasCount(2, commands);
                        commands.Dequeue()();
                        bridge.RefreshSnapshot();
                        viewModel.Start();
                        Assert.AreEqual(first, viewModel.SelectedRouteCandidate.CandidateIndex);
                        commands.Dequeue()();
                        bridge.RefreshSnapshot();
                        viewModel.Start();

                        Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                        Assert.AreEqual((fromNodeIndex, last), (viewModel.SelectedRouteCandidate.FromNodeIndex, viewModel.SelectedRouteCandidate.CandidateIndex));
                        Assert.AreEqual(last, bridge.CaptureTrainPathSnapshot().PreviewedRouteCandidateIndex);
                        AssertJunctionRoute(expected, PathRouteResolver.Resolve(CaptureCandidatePreview(editor), world, TestContext.CancellationToken)
                            .PassingRoutes.Single().Spans.SelectMany(span => span.Candidates.Single().RouteNodeIndexes).Distinct());
                        AssertSourceUnchanged(editor, source);
                    }
                }
            }
        }

        /// <summary>Acceptance queued before a new preview remains strict; a fresh acceptance uses the latest selection.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenGuidedPreviewAndAcceptanceAreQueuedTogetherThenOnlyFreshAcceptanceCanAcceptTheLatestPreview(bool acceptPreviewed)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            PathModel source = CreatePassingSource(world);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginGuidedPhase(editor, world, 3);
                int fromNodeIndex = editor.GetAmbiguousSpans()[0].FromNodeIndex;
                Queue<Action> commands = new Queue<Action>();
                TrainPathToolWindow bridge = CreateQueuedCandidateBridge(() => editor, commands);

                bridge.PreviewRouteCandidate(fromNodeIndex, 1);
                QueueCandidateAcceptance(bridge, fromNodeIndex, acceptPreviewed);
                commands.Dequeue()();
                PathModel latestPreview = CaptureCandidatePreview(editor);
                long revision = editor.PassingBranchInteractionRevision;
                commands.Dequeue()();
                bridge.RefreshSnapshot();

                Assert.IsTrue(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                Assert.AreSame(latestPreview, CaptureCandidatePreview(editor));
                Assert.AreEqual((revision, 1), (editor.PassingBranchInteractionRevision, editor.PreviewedRouteCandidateIndex));
                Assert.HasCount(2, editor.GetAmbiguousSpans());
                bridge.AcceptPreviewedRouteCandidate();
                commands.Dequeue()();
                bridge.RefreshSnapshot();
                Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                ResolvedPathSpan suffix = editor.GetAmbiguousSpans().Single();
                Assert.HasCount(1, suffix.Candidates);
                AssertJunctionRoute(expectedArray458, suffix.Candidates.Single().RouteNodeIndexes);
                bridge.AcceptRouteCandidate(suffix.FromNodeIndex, 0);
                commands.Dequeue()();
                AssertJunctionRoute(expectedArray13458, PathRouteResolver.Resolve(editor.TryCaptureCurrentPathModel(), world, TestContext.CancellationToken)
                    .PassingRoutes.Single().Spans.SelectMany(span => span.Candidates.Single().RouteNodeIndexes).Distinct());
            }
        }

        /// <summary>An accepted leg replaces the correlated candidate ordering even when the same draft and ordinal survive.</summary>
        [TestMethod]
        [DataRow(0, 1, false)]
        [DataRow(1, 0, false)]
        [DataRow(0, 1, true)]
        [DataRow(1, 0, true)]
        public async Task WhenAcceptedGuidedLegChangesRemainingCandidatesThenQueuedPreviewCannotOverwriteTheNewPreview(int acceptedIndex, int oldIndex, bool cycle)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            PathModel source = CreatePassingSource(world);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginGuidedPhase(editor, world, 3);
                ImmutableArray<ResolvedPathSpan> spans = editor.GetAmbiguousSpans();
                Assert.IsTrue(editor.PreviewRouteCandidateCommand(spans[1].FromNodeIndex, oldIndex).Success);
                Queue<Action> commands = new Queue<Action>();
                TrainPathToolWindow bridge = CreateQueuedCandidateBridge(() => editor, commands);
                QueueCandidatePreview(bridge, spans[1].FromNodeIndex, oldIndex, cycle);

                Assert.IsTrue(editor.AcceptRouteCandidateCommand(spans[0].FromNodeIndex, acceptedIndex).Success);
                ResolvedPathSpan remaining = editor.GetAmbiguousSpans().Single();
                Assert.HasCount(1, remaining.Candidates);
                Assert.IsTrue(editor.PreviewRouteCandidateCommand(remaining.FromNodeIndex, 0).Success);
                PathModel latestPreview = CaptureCandidatePreview(editor);
                long revision = editor.PassingBranchInteractionRevision;
                commands.Dequeue()();
                bridge.RefreshSnapshot();

                Assert.IsTrue(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                Assert.AreSame(latestPreview, CaptureCandidatePreview(editor));
                Assert.AreSame(remaining, editor.GetAmbiguousSpans().Single());
                Assert.AreEqual((revision, remaining.FromNodeIndex, 0), (editor.PassingBranchInteractionRevision,
                    editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
                AssertSourceUnchanged(editor, source);
            }
        }

        /// <summary>Queued previews and cycles retain source, draft, cancellation and load/edit-ABA ownership checks.</summary>
        [TestMethod]
        [DataRow("Draft", false)]
        [DataRow("Draft", true)]
        [DataRow("LoadSame", false)]
        [DataRow("LoadSame", true)]
        [DataRow("LoadOther", false)]
        [DataRow("LoadOther", true)]
        [DataRow("EditUndo", false)]
        [DataRow("EditUndo", true)]
        [DataRow("Cancel", false)]
        [DataRow("Cancel", true)]
        [DataRow("Unload", false)]
        [DataRow("Unload", true)]
        [DataRow("Stop", false)]
        [DataRow("Stop", true)]
        public async Task WhenQueuedGuidedPreviewOutlivesItsSourceThenNewerStateIsNotOverwritten(string replacement, bool cycle)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(CreatePassingSource(world), TestContext.CancellationToken).ConfigureAwait(false));
                BeginGuidedPhase(editor, world, 3);
                int fromNodeIndex = editor.GetAmbiguousSpans()[0].FromNodeIndex;
                Assert.IsTrue(editor.PreviewRouteCandidateCommand(fromNodeIndex, 0).Success);
                Queue<Action> commands = new Queue<Action>();
                TrainPathToolWindow bridge = CreateQueuedCandidateBridge(() => editor, commands);
                QueueCandidatePreview(bridge, fromNodeIndex, 0, cycle);
                await ReplaceGuidedPreviewSourceAsync(editor, world, replacement).ConfigureAwait(false);
                PathModel latestModel = editor.TryCaptureCurrentPathModel();
                PathModel latestPreview = CaptureCandidatePreview(editor);
                PathModel[] undo = CaptureHistory(editor, "undoHistory");
                PathModel[] redo = CaptureHistory(editor, "redoHistory");
                var latestState = (editor.CompositionRevision, editor.PassingBranchInteractionRevision, editor.PassingBranchPhase,
                    editor.PlacementMode, editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex);

                commands.Dequeue()();
                bridge.RefreshSnapshot();

                Assert.IsTrue(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                Assert.AreSame(latestModel, editor.TryCaptureCurrentPathModel());
                Assert.AreSame(latestPreview, CaptureCandidatePreview(editor));
                Assert.AreSequenceEqual(undo, CaptureHistory(editor, "undoHistory"));
                Assert.AreSequenceEqual(redo, CaptureHistory(editor, "redoHistory"));
                Assert.AreEqual(latestState, (editor.CompositionRevision, editor.PassingBranchInteractionRevision, editor.PassingBranchPhase,
                    editor.PlacementMode, editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
            }
        }

        /// <summary>Save cannot acquire a token during the draft or invalidate otherwise current queued previews.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenSaveIsRequestedDuringQueuedGuidedPreviewThenSaveRefusesAndPreviewRemainsCurrent(bool cycle)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            PathModel source = CreatePassingSource(world);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginGuidedPhase(editor, world, 3);
                int fromNodeIndex = editor.GetAmbiguousSpans()[0].FromNodeIndex;
                Assert.IsTrue(editor.PreviewRouteCandidateCommand(fromNodeIndex, 0).Success);
                Queue<Action> commands = new Queue<Action>();
                TrainPathToolWindow bridge = CreateQueuedCandidateBridge(() => editor, commands);
                QueueCandidatePreview(bridge, fromNodeIndex, 1, cycle);

                PathSaveOperation save = editor.BeginSave(source);
                Assert.IsFalse(save.Acquired);
                Assert.IsFalse((await save.PersistenceTask.ConfigureAwait(false)).PersistenceAllowed);
                Assert.IsFalse(editor.IsSaveInProgress);
                commands.Dequeue()();
                bridge.RefreshSnapshot();

                Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                Assert.AreEqual((PassingBranchAuthoringPhase.SelectingCandidate, fromNodeIndex, 1),
                    (editor.PassingBranchPhase, editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
                AssertSourceUnchanged(editor, source);
            }
        }

        /// <summary>Identical models in another editor do not transfer queued preview ownership.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenQueuedGuidedPreviewOwnerChangesThenReplacementEditorPreviewSurvives(bool cycle)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            PathModel source = CreatePassingSource(world);
            using (PathEditor original = new PathEditor(new TestPathEditorContext(world)))
            {
                using (PathEditor replacement = new PathEditor(new TestPathEditorContext(world)))
                {
                    Assert.IsTrue(await original.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                    Assert.IsTrue(await replacement.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                    BeginGuidedPhase(original, world, 3);
                    BeginGuidedPhase(replacement, world, 3);
                    int fromNodeIndex = original.GetAmbiguousSpans()[0].FromNodeIndex;
                    Assert.IsTrue(original.PreviewRouteCandidateCommand(fromNodeIndex, 0).Success);
                    Assert.IsTrue(replacement.PreviewRouteCandidateCommand(fromNodeIndex, 1).Success);
                    PathEditor owner = original;
                    Queue<Action> commands = new Queue<Action>();
                    TrainPathToolWindow bridge = CreateQueuedCandidateBridge(() => owner, commands);
                    QueueCandidatePreview(bridge, fromNodeIndex, 0, cycle);
                    owner = replacement;
                    PathModel latestPreview = CaptureCandidatePreview(replacement);
                    long revision = replacement.PassingBranchInteractionRevision;

                    commands.Dequeue()();
                    bridge.RefreshSnapshot();

                    Assert.IsTrue(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                    Assert.AreSame(latestPreview, CaptureCandidatePreview(replacement));
                    Assert.AreEqual((revision, 1), (replacement.PassingBranchInteractionRevision, replacement.PreviewedRouteCandidateIndex));
                    AssertSourceUnchanged(replacement, source);
                }
            }
        }

        /// <summary>Replacing or confirming the via cannot reinterpret a candidate queued before candidate admission.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenViaIsReplacedBeforeQueuedCandidateRunsThenConfirmedDraftPreviewSurvives(bool cycle)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            PathModel source = CreatePassingSource(world);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginGuidedPhase(editor, world, 2);
                Queue<Action> commands = new Queue<Action>();
                TrainPathToolWindow bridge = CreateQueuedCandidateBridge(() => editor, commands);
                QueueCandidatePreview(bridge, 0, 0, cycle);
                Assert.IsTrue(editor.PreviewPassingBranchViaCommand(CreateAnchor(world, 4, PathNodeType.Via, -1), true).Success);
                Assert.IsTrue(editor.CommitPlacementCommand().Success);
                int fromNodeIndex = editor.GetAmbiguousSpans()[0].FromNodeIndex;
                Assert.IsTrue(editor.PreviewRouteCandidateCommand(fromNodeIndex, 1).Success);
                PathModel latestPreview = CaptureCandidatePreview(editor);
                long revision = editor.PassingBranchInteractionRevision;

                commands.Dequeue()();
                bridge.RefreshSnapshot();

                Assert.IsTrue(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                Assert.AreSame(latestPreview, CaptureCandidatePreview(editor));
                Assert.AreEqual((revision, fromNodeIndex, 1), (editor.PassingBranchInteractionRevision,
                    editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
                AssertSourceUnchanged(editor, source);
            }
        }

        /// <summary>Quick passing previews keep their existing stable pending object and span ownership.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenQuickPassingPreviewsAreQueuedTogetherThenBothSucceedWithoutChangingInteractionOwnership(bool cycle)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            PathModel source = CreatePassingSource(world);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);
                Assert.IsFalse(editor.CompletePassingBranchCommand(2).Success);
                ResolvedPathSpan span = editor.GetAmbiguousSpans().Single();
                Assert.HasCount(2, span.Candidates);
                Assert.IsTrue(editor.PreviewRouteCandidateCommand(span.FromNodeIndex, 1).Success);
                Queue<Action> commands = new Queue<Action>();
                TrainPathToolWindow bridge = CreateQueuedCandidateBridge(() => editor, commands);
                long revision = editor.PassingBranchInteractionRevision;

                QueueCandidatePreview(bridge, span.FromNodeIndex, 0, cycle);
                QueueCandidatePreview(bridge, span.FromNodeIndex, 1, cycle);
                commands.Dequeue()();
                bridge.RefreshSnapshot();
                Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                Assert.AreEqual(0, editor.PreviewedRouteCandidateIndex);
                commands.Dequeue()();
                bridge.RefreshSnapshot();

                Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
                Assert.AreEqual((revision, 1), (editor.PassingBranchInteractionRevision, editor.PreviewedRouteCandidateIndex));
                Assert.AreSame(span, editor.GetAmbiguousSpans().Single());
                Assert.IsTrue(new ResolvedPathRoute(PathRouteBranchKind.Passing, 0, 2, ImmutableArray.Create(span with
                {
                    Status = PathRouteSpanStatus.Resolved,
                    Candidates = ImmutableArray.Create(span.Candidates[1]),
                })).HasSamePhysicalTraversal(PathRouteResolver.Resolve(CaptureCandidatePreview(editor), world, TestContext.CancellationToken).PassingRoutes.Single()));
                AssertSourceUnchanged(editor, source);
            }
        }

        /// <summary>A stale queued cancel must not discard a replacement guided interaction.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenQueuedGuidedCancelIsStaleThenNewerSourceAndDraftSurvive(bool replaceSource)
        {
            TrackWorld world = CreateCorrelatedTrackWorld(true);
            PathModel source = CreatePassingSource(world);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginGuidedPhase(editor, world, 2);
                Queue<Action> commands = new Queue<Action>();
                TrainPathToolWindow bridge = new TrainPathToolWindow(() => editor, () => null, commands.Enqueue, () => { }, () => { }, _ => { }, () => { }, () => { }) { Active = true };
                bridge.CancelPassingBranch();
                Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                if (replaceSource)
                    Assert.IsTrue(await editor.InitializePathAsync(source with { Id = "newer-source" }, TestContext.CancellationToken).ConfigureAwait(false));
                PathModel newer = editor.TryCaptureCurrentPathModel();
                BeginGuidedPhase(editor, world, 2);
                long revision = editor.PassingBranchInteractionRevision;

                commands.Dequeue()();

                Assert.AreSame(newer, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual((PassingBranchAuthoringPhase.ConfirmingVia, revision), (editor.PassingBranchPhase, editor.PassingBranchInteractionRevision));
                bridge.RefreshSnapshot();
                Assert.IsTrue(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning);
            }
        }

        /// <summary>Invalid, collapsed and discontinuous vias cannot replace a valid private preview.</summary>
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public async Task WhenGuidedViaIsInvalidThenExistingPreviewAndSourceSurvive(int invalidKind)
        {
            TrackWorld world = CreateLongPassingTrackWorld();
            PathModel source = CreateLongPassingSource();
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(world)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);
                Assert.IsTrue(editor.RejoinPassingBranchWithViaCommand(2).Success);
                Assert.IsTrue(editor.PreviewPassingBranchViaCommand(LongPassingVia(), false).Success);
                PathNode invalid = invalidKind switch
                {
                    0 => new PathNode(new WorldLocation(new Tile(1, 0), new Vector3(1000, 0, 1000))),
                    1 => source.PathNodes[0],
                    _ => source.PathNodes[1],
                };
                long revision = editor.PassingBranchInteractionRevision;

                Assert.IsFalse(editor.PreviewPassingBranchViaCommand(invalid, invalidKind == 1).Success);

                Assert.AreEqual((PassingBranchAuthoringPhase.ConfirmingVia, revision), (editor.PassingBranchPhase, editor.PassingBranchInteractionRevision));
                AssertSourceUnchanged(editor, source);
                Assert.IsTrue(editor.CommitPlacementCommand().Success);
                Assert.IsTrue(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.Location == LongPassingVia().Location && node.NextMainNode < 0));
            }
        }

        private static PathModel CreatePassingSource(TrackWorld world)
        {
            int bypass = world.TrackDatabase.TrackNodes.OfType<VectorNode>().Single(node =>
                world.TrackDatabase.TrackNodeConnectors[node.NodeIndex].TrackNodeConnectors.Select(pin => pin.Link).SequenceEqual(expectedArray18)).NodeIndex;
            return CreateSourcePath(world) with
            {
                PathNodes = ImmutableArray.Create(CreateAnchor(world, 1, PathNodeType.Start, 1),
                    new PathNode(new WorldLocation(new Tile(1, 0), new Vector3(450, 0, 0)))
                    { NodeIndex = bypass, NodeType = PathNodeType.Via, NextMainNode = 2, NextSidingNode = -1 },
                    CreateAnchor(world, 8, PathNodeType.End, -1)),
            };
        }

        private static void BeginGuidedPhase(PathEditor editor, TrackWorld world, int phase)
        {
            Assert.IsTrue(editor.BeginPassingBranchCommand(0).Success);
            if (phase == 0)
                return;
            Assert.IsTrue(editor.RejoinPassingBranchWithViaCommand(2).Success);
            if (phase == 1)
                return;
            PathEditorCommandResult preview = editor.PreviewPassingBranchViaCommand(CreateAnchor(world, 4, PathNodeType.Via, -1), true);
            Assert.IsTrue(preview.Success, preview.Message);
            if (phase == 2)
                return;
            Assert.IsTrue(editor.CommitPlacementCommand().Success);
            Assert.AreEqual(PassingBranchAuthoringPhase.SelectingCandidate, editor.PassingBranchPhase);
        }

        private static PathModel CreateLongPassingSource() => new PathModel
        {
            Id = "guided-source",
            Name = "Guided source",
            PathNodes = ImmutableArray.Create(
                new PathNode(new WorldLocation(new Tile(1, 0), Vector3.Zero)) { NodeType = PathNodeType.Start | PathNodeType.Junction, NodeIndex = 1, NextMainNode = 1, NextSidingNode = -1 },
                new PathNode(new WorldLocation(new Tile(1, 0), new Vector3(50, 0, 0))) { NodeType = PathNodeType.Via, NodeIndex = 3, NextMainNode = 2, NextSidingNode = -1 },
                new PathNode(new WorldLocation(new Tile(1, 0), new Vector3(100, 0, 0))) { NodeType = PathNodeType.End | PathNodeType.Junction, NodeIndex = 2, NextMainNode = -1, NextSidingNode = -1 }),
        };

        private static TrackWorld CreateLongPassingTrackWorld()
        {
            Tile tile = new Tile(1, 0);
            WorldLocation start = new WorldLocation(tile, Vector3.Zero);
            WorldLocation end = new WorldLocation(tile, new Vector3(100, 0, 0));
            VectorSectionNode Section(float x, float z, float endX, float endZ, float heading) => new VectorSectionNode(
                new WorldLocation(tile, new Vector3(x, 0, z)), tile, new Vector3(0, heading, 0),
                new WorldLocation(tile, new Vector3(endX, 0, endZ)))
            { NodeIndex = 1 };
            TrackNodeConnectorIndex Pins(int index, params (int Link, ConnectorType Type, TrackDirection Direction)[] pins)
                => new TrackNodeConnectorIndex
                {
                    NodeIndex = index,
                    TrackNodeConnectors = pins.Select(pin => new TrackNodeConnector
                    { Link = pin.Link, ConnectorType = pin.Type, Direction = pin.Direction }).ToImmutableArray()
                };
            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = ImmutableArray.Create<TrackNodeBase>(null,
                    new JunctionNode(start, tile, Vector3.Zero) { NodeIndex = 1 },
                    new JunctionNode(end, tile, Vector3.Zero) { NodeIndex = 2 },
                    new VectorNode(start, tile, end) { NodeIndex = 3, VectorSections = ImmutableArray.Create(Section(0, 0, 100, 0, MathHelper.PiOver2)) },
                    new VectorNode(start, tile, end) { NodeIndex = 4, VectorSections = ImmutableArray.Create(Section(0, 0, 100, 0, MathHelper.PiOver2)) },
                    new VectorNode(start, tile, end)
                    {
                        NodeIndex = 5,
                        VectorSections = ImmutableArray.Create(
                        Section(0, 0, 0, 100, 0), Section(0, 100, 100, 100, MathHelper.PiOver2), Section(100, 100, 100, 0, MathHelper.Pi))
                    }),
                TrackNodeConnectors = ImmutableArray.Create(new TrackNodeConnectorIndex(),
                    Pins(1, (3, ConnectorType.OutPin, TrackDirection.Reverse), (4, ConnectorType.OutPin, TrackDirection.Reverse), (5, ConnectorType.OutPin, TrackDirection.Reverse)),
                    Pins(2, (3, ConnectorType.OutPin, TrackDirection.Ahead), (4, ConnectorType.OutPin, TrackDirection.Ahead), (5, ConnectorType.OutPin, TrackDirection.Ahead)),
                    Pins(3, (1, ConnectorType.InPin, TrackDirection.Ahead), (2, ConnectorType.OutPin, TrackDirection.Ahead)),
                    Pins(4, (1, ConnectorType.InPin, TrackDirection.Ahead), (2, ConnectorType.OutPin, TrackDirection.Ahead)),
                    Pins(5, (1, ConnectorType.InPin, TrackDirection.Ahead), (2, ConnectorType.OutPin, TrackDirection.Ahead))),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            TrackSectionModel sections = new TrackSectionModel
            {
                TrackSections = ImmutableDictionary<int, TrackSection>.Empty.Add(1,
                new TrackSection { SectionIndex = 1, Gauge = 1.435f, Length = 100 })
            };
            TrackWorld world = TrackWorld.Initialize(null, new TrackModel { TrackDatabase = database }, sections);
            GameService<RuntimeDataResolver>.Set(null, new TestRuntimeDataResolver(sections, world));
            world.SetSegmentSections(database.VectorNodes.Select(node => new TrackSegmentSection(node.NodeIndex,
                node.VectorSections.Select((section, index) => new TrackSegment(section, node.NodeIndex, index)))));
            world.SetJunctions(database.JunctionNodes.Select(node => new JunctionWidget(node,
                database.TrackNodeConnectors[node.NodeIndex].OutConnectors[node.MainRoute].Link)));
            return world;
        }

        /// <summary>Previewing either upstream choice lets the resolver choose its compatible, unselected suffix.</summary>
        [TestMethod]
        [DataRow(0, new int[] { 1, 2, 4, 6, 8 })]
        [DataRow(1, new int[] { 1, 3, 4, 5, 8 })]
        public async Task WhenUpstreamCandidateIsPreviewedThenUnselectedSuffixIsCoherent(int candidateIndex, int[] expectedRoute)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            PathModel source = CreateSourcePath(trackWorld);
            TestPathEditorContext context = new TestPathEditorContext(trackWorld);
            using (PathEditor editor = new PathEditor(context))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginPendingCandidates(editor, trackWorld);

                PathEditResult preview = editor.PreviewPendingRouteCandidate(0, candidateIndex);

                Assert.IsTrue(preview.Success, preview.Message);
                AssertJunctionRoute(expectedRoute, context.LastRenderedPathModel.PathNodes.Select(node => node.NodeIndex));
                Assert.AreEqual(0, editor.PreviewedRouteCandidateFromNodeIndex);
                Assert.AreEqual(candidateIndex, editor.PreviewedRouteCandidateIndex);
                Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit);
                AssertSourceUnchanged(editor, source);
                editor.CancelPendingRouteCandidate();
                AssertSourceUnchanged(editor, source);
            }
        }

        /// <summary>A coherent automatic suffix preview does not count as an explicit downstream selection.</summary>
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public async Task WhenOnlyUpstreamCandidateIsSelectedThenAcceptanceRequiresDownstreamSelection(int candidateIndex)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            PathModel source = CreateSourcePath(trackWorld);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginPendingCandidates(editor, trackWorld);
                PathEditResult preview = editor.PreviewPendingRouteCandidate(0, candidateIndex);
                Assert.IsTrue(preview.Success, preview.Message);

                PathEditResult acceptance = editor.AcceptPendingRouteCandidate(0, candidateIndex);

                Assert.IsFalse(acceptance.Success);
                Assert.Contains("Select a route candidate for the span starting at node 1.", acceptance.Message);
                Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit);
                AssertSourceUnchanged(editor, source);
                editor.CancelPendingRouteCandidate();
                AssertSourceUnchanged(editor, source);
            }
        }

        /// <summary>Explicitly compatible upstream and downstream choices commit exactly the selected chain.</summary>
        [TestMethod]
        [DataRow(0, 0, new int[] { 1, 2, 4, 6, 8 })]
        [DataRow(1, 1, new int[] { 1, 3, 4, 5, 8 })]
        public async Task WhenCompatibleCandidatesAreSelectedThenAcceptanceCommitsExactRoute(int upstreamCandidateIndex,
            int downstreamCandidateIndex, int[] expectedRoute)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            PathModel source = CreateSourcePath(trackWorld);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginPendingCandidates(editor, trackWorld);
                PathEditResult upstream = editor.PreviewPendingRouteCandidate(0, upstreamCandidateIndex);
                Assert.IsTrue(upstream.Success, upstream.Message);
                PathEditResult downstream = editor.PreviewPendingRouteCandidate(1, downstreamCandidateIndex);
                Assert.IsTrue(downstream.Success, downstream.Message);
                AssertSourceUnchanged(editor, source);

                PathEditResult acceptance = editor.AcceptPendingRouteCandidate(1, downstreamCandidateIndex);

                Assert.IsTrue(acceptance.Success, acceptance.Message);
                AssertJunctionRoute(expectedRoute, editor.TryCaptureCurrentPathModel().PathNodes.Select(node => node.NodeIndex));
                Assert.IsFalse(editor.HasPendingAmbiguousSpanCommit);
                Assert.IsTrue(editor.HasUnsavedChanges);
                Assert.IsTrue(editor.CanUndo);
                Assert.IsFalse(editor.CanRedo);
            }
        }

        /// <summary>Accepting both correlated selections records only one undo snapshot of the original sparse path.</summary>
        [TestMethod]
        [DataRow(0, 0)]
        [DataRow(1, 1)]
        public async Task WhenCompatibleCandidatesAreAcceptedThenOneUndoRestoresSource(int upstreamCandidateIndex,
            int downstreamCandidateIndex)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            PathModel source = CreateSourcePath(trackWorld);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginPendingCandidates(editor, trackWorld);
                PathEditResult upstream = editor.PreviewPendingRouteCandidate(0, upstreamCandidateIndex);
                Assert.IsTrue(upstream.Success, upstream.Message);
                PathEditResult downstream = editor.PreviewPendingRouteCandidate(1, downstreamCandidateIndex);
                Assert.IsTrue(downstream.Success, downstream.Message);
                PathEditResult acceptance = editor.AcceptPendingRouteCandidate(1, downstreamCandidateIndex);
                Assert.IsTrue(acceptance.Success, acceptance.Message);

                Assert.IsTrue(editor.Undo());

                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.AreEqual(source.Id, editor.TryCaptureCurrentPathModel().Id);
                Assert.IsFalse(editor.HasPendingAmbiguousSpanCommit);
                Assert.IsFalse(editor.CanUndo);
                Assert.IsTrue(editor.CanRedo);
            }
        }

        /// <summary>Incompatible explicit choices are refused without changing the committed model or history.</summary>
        [TestMethod]
        [DataRow(0, 1)]
        [DataRow(1, 0)]
        public async Task WhenExplicitCandidatesAreIncompatibleThenAcceptanceLeavesSourceUnchanged(int upstreamCandidateIndex,
            int downstreamCandidateIndex)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            PathModel source = CreateSourcePath(trackWorld);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginPendingCandidates(editor, trackWorld);
                PathEditResult upstream = editor.PreviewPendingRouteCandidate(0, upstreamCandidateIndex);
                Assert.IsTrue(upstream.Success, upstream.Message);

                PathEditResult acceptance = editor.AcceptPendingRouteCandidate(1, downstreamCandidateIndex);

                Assert.IsFalse(acceptance.Success, $"{acceptance.Message} Committed junction route: " +
                    string.Join("->", editor.TryCaptureCurrentPathModel().PathNodes
                        .Where(node => node.NodeIndex is >= 1 and <= 8).Select(node => node.NodeIndex)));
                Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit);
                AssertSourceUnchanged(editor, source);
                editor.CancelPendingRouteCandidate();
                AssertSourceUnchanged(editor, source);
            }
        }

        /// <summary>A provisional via insertion is not an intervening edit that invalidates its own candidate.</summary>
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public async Task WhenInteractiveViaCandidateIsPreviewedThenProvisionalInsertionIsNotStale(int candidateIndex)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            PathModel source = CreateInteractiveViaSourcePath(trackWorld);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginInteractiveViaCandidates(editor, trackWorld);
                PathModel provisional = editor.TryCaptureCurrentPathModel();

                PathEditResult preview = editor.PreviewPendingRouteCandidate(1, candidateIndex);

                Assert.IsTrue(preview.Success, preview.Message);
                Assert.AreSame(provisional, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual((true, 1, candidateIndex, false, false), (editor.HasPendingAmbiguousSpanCommit,
                    editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex, editor.CanUndo, editor.CanRedo));
            }
        }

        /// <summary>Accepting a pointer-placed via records the original path, not the provisional insertion, for undo.</summary>
        [TestMethod]
        [DataRow(0, new int[] { 7, 1, 2, 4, 6, 8 })]
        [DataRow(1, new int[] { 7, 1, 3, 4, 5, 8 })]
        public async Task WhenInteractiveViaCandidateIsAcceptedThenOneUndoRestoresOriginalPath(int candidateIndex, int[] expectedRoute)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            PathModel source = CreateInteractiveViaSourcePath(trackWorld);
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                BeginInteractiveViaCandidates(editor, trackWorld);
                PathEditResult downstream = editor.PreviewPendingRouteCandidate(1, candidateIndex);
                Assert.IsTrue(downstream.Success, downstream.Message);

                PathEditResult acceptance = editor.AcceptPendingRouteCandidate(1, candidateIndex);

                Assert.IsTrue(acceptance.Success, acceptance.Message);
                AssertJunctionRoute(expectedRoute, editor.TryCaptureCurrentPathModel().PathNodes.Select(node => node.NodeIndex));
                Assert.AreEqual((false, false, true, true), (editor.HasPendingAmbiguousSpanCommit, editor.IsPlacementActive, editor.HasUnsavedChanges, editor.CanUndo));
                Assert.IsTrue(editor.Undo());
                Assert.AreSequenceEqual(source.PathNodes, editor.TryCaptureCurrentPathModel().PathNodes);
                Assert.AreEqual(source.Id, editor.TryCaptureCurrentPathModel().Id);
                Assert.IsFalse(editor.CanUndo, "Candidate acceptance must create exactly one undo step from the original model.");
                Assert.IsTrue(editor.CanRedo);
            }
        }

        /// <summary>Candidate Cancel and the command used by Escape roll back the whole provisional via interaction.</summary>
        [TestMethod]
        [DataRow("CandidateCancel", false)]
        [DataRow("CandidateCancel", true)]
        [DataRow("Escape", false)]
        [DataRow("Escape", true)]
        public async Task WhenInteractiveViaCandidateIsCanceledThenOriginalModelModeDirtyStateAndHistoryAreRestored(string cancellation, bool withHistory)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            TestPathEditorContext context = new TestPathEditorContext(trackWorld);
            using (PathEditor editor = new PathEditor(context))
            {
                Assert.IsTrue(await editor.InitializePathAsync(CreateInteractiveViaSourcePath(trackWorld), TestContext.CancellationToken).ConfigureAwait(false));
                PrepareExistingHistory(editor, withHistory);
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                (bool, ToolboxContentMode, bool, bool, bool) baselineState =  (editor.EditMode, context.ContentMode, editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo);
                PathModel[] undoHistory = CaptureHistory(editor, "undoHistory");
                PathModel[] redoHistory = CaptureHistory(editor, "redoHistory");
                BeginInteractiveViaCandidates(editor, trackWorld);

                CancelViaCandidate(editor, cancellation);

                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual(baselineState, (editor.EditMode, context.ContentMode, editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo));
                Assert.AreSequenceEqual(undoHistory, CaptureHistory(editor, "undoHistory"));
                Assert.AreSequenceEqual(redoHistory, CaptureHistory(editor, "redoHistory"));
                Assert.AreEqual((false, false, -1, -1), (editor.IsPlacementActive, editor.HasPendingAmbiguousSpanCommit, editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
            }
        }

        /// <summary>A real replacement while the via candidate is pending must not be overwritten by preview or acceptance.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenInteractiveViaCandidateModelIsReplacedThenStaleChoicePreservesInterveningState(bool accept)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            TestPathEditorContext context = new TestPathEditorContext(trackWorld);
            using (PathEditor editor = new PathEditor(context))
            {
                Assert.IsTrue(await editor.InitializePathAsync(CreateInteractiveViaSourcePath(trackWorld), TestContext.CancellationToken).ConfigureAwait(false));
                PrepareExistingHistory(editor, true);
                BeginInteractiveViaCandidates(editor, trackWorld);
                PathModel replacement = CreateSourcePath(trackWorld) with { Id = "intervening-path", Name = "Intervening path" };
                typeof(PathEditor).GetMethod("RestoreSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(editor, new object[] { replacement });
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                (bool, ToolboxContentMode, bool) baselineState =
                    (editor.EditMode, context.ContentMode, editor.HasUnsavedChanges);
                PathModel[] undoHistory = CaptureHistory(editor, "undoHistory");
                PathModel[] redoHistory = CaptureHistory(editor, "redoHistory");
                Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit, "The existing snapshot seam must leave a genuinely stale candidate to validate.");

                PathEditResult result = SelectStaleCandidate(editor, accept);

                Assert.IsFalse(result.Success);
                Assert.Contains("The path changed", result.Message);
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.AreSame(baseline, result.PathModel);
                Assert.AreEqual(baselineState, (editor.EditMode, context.ContentMode, editor.HasUnsavedChanges));
                Assert.AreSequenceEqual(undoHistory, CaptureHistory(editor, "undoHistory"));
                Assert.AreSequenceEqual(redoHistory, CaptureHistory(editor, "redoHistory"));
                Assert.AreEqual((false, false, -1, -1), (editor.HasPendingAmbiguousSpanCommit, editor.IsPlacementActive,
                    editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
            }
        }

        /// <summary>Finish, Finish Here, and repeated placement commits cannot bypass an outstanding route choice.</summary>
        [TestMethod]
        [DataRow("Finish")]
        [DataRow("FinishHere")]
        [DataRow("Commit")]
        public async Task WhenBuildRouteCandidateIsPendingThenFinishAndCommitCommandsRefuseWithoutMutation(string command)
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            TestPathEditorContext context = new TestPathEditorContext(trackWorld);
            using (PathEditor editor = new PathEditor(context))
            {
                Assert.IsTrue(await editor.InitializePathAsync(CreateSourcePath(trackWorld), TestContext.CancellationToken).ConfigureAwait(false));
                BeginInteractiveBuildRouteCandidate(editor, trackWorld);
                PathEditResult preview = editor.PreviewPendingRouteCandidate(0, 0);
                Assert.IsTrue(preview.Success, preview.Message);
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                object pending = typeof(PathEditor).GetField("pendingAmbiguousSpanCommit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                (bool, ToolboxContentMode, bool, bool, bool) baselineState =
                    (editor.EditMode, context.ContentMode, editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo);

                PathEditorCommandResult result = ExecutePendingFinishCommand(editor, command);

                Assert.IsFalse(result.Success, result.Message);
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.AreSame(pending, typeof(PathEditor).GetField("pendingAmbiguousSpanCommit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor),
                    "Refusal must preserve the pending choice rather than replace it with a new ambiguity.");
                Assert.AreEqual(baselineState, (editor.EditMode, context.ContentMode, editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo));
                Assert.AreEqual((true, true, 0, 0), (editor.IsBuildingRoute, editor.HasPendingAmbiguousSpanCommit,
                    editor.PreviewedRouteCandidateFromNodeIndex, editor.PreviewedRouteCandidateIndex));
            }
        }

        /// <summary>The existing snapshot must not advertise Finish while a route candidate awaits a decision.</summary>
        [TestMethod]
        public async Task WhenBuildRouteCandidateIsPendingThenSnapshotDisablesFinish()
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(CreateSourcePath(trackWorld), TestContext.CancellationToken).ConfigureAwait(false));
                BeginInteractiveBuildRouteCandidate(editor, trackWorld);
                TrainPathToolWindow bridge = CreateCandidateBridge(editor);

                bridge.RefreshSnapshot();

                Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CanFinishPath);
            }
        }

        /// <summary>Canceling the candidate restores normal Finish availability for the already complete path.</summary>
        [TestMethod]
        public async Task WhenBuildRouteCandidateIsCanceledThenNormalFinishRetainsCommittedEnd()
        {
            TrackWorld trackWorld = CreateCorrelatedTrackWorld();
            using (PathEditor editor = new PathEditor(new TestPathEditorContext(trackWorld)))
            {
                Assert.IsTrue(await editor.InitializePathAsync(CreateSourcePath(trackWorld), TestContext.CancellationToken).ConfigureAwait(false));
                BeginInteractiveBuildRouteCandidate(editor, trackWorld);
                PathModel baseline = editor.TryCaptureCurrentPathModel();
                editor.CancelPendingRouteCandidate();
                TrainPathToolWindow bridge = CreateCandidateBridge(editor);
                bridge.RefreshSnapshot();
                Assert.IsTrue(bridge.CaptureTrainPathSnapshot().CanFinishPath);

                PathEditorCommandResult result = editor.FinishPathCommand();

                Assert.IsTrue(result.Success, result.Message);
                Assert.AreSame(baseline, editor.TryCaptureCurrentPathModel());
                Assert.AreEqual((false, false, false, false, false), (editor.IsBuildingRoute, editor.HasPendingAmbiguousSpanCommit,
                    editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo));
            }
        }

        private static PathModel CreateInteractiveViaSourcePath(TrackWorld trackWorld)
            => CreateSourcePath(trackWorld) with
            {
                PathNodes = ImmutableArray.Create(CreateAnchor(trackWorld, 7, PathNodeType.Start, 1),
                    CreateAnchor(trackWorld, 8, PathNodeType.End, -1)),
            };

        private static void BeginInteractiveViaCandidates(PathEditor editor, TrackWorld trackWorld)
        {
            PathModel original = editor.TryCaptureCurrentPathModel();
            Assert.IsTrue(editor.BeginViaPointPlacementAt(0, CreateAnchor(trackWorld, 7, PathNodeType.Via, -1)).Success);
            Assert.AreNotSame(original, editor.TryCaptureCurrentPathModel());
            Assert.HasCount(original.PathNodes.Length + 1, editor.TryCaptureCurrentPathModel().PathNodes);
            // Carry the exact incoming-tail vector selected by the map; collinear fixture geometry cannot choose another vector.
            int vectorIndex = trackWorld.TrackDatabase.TrackNodeConnectors[7].TrackNodeConnectors.Single().Link;
            TrackSegmentBase segment = trackWorld.SegmentSections[vectorIndex].SectionSegments.Single();
            PointD pointer = PointD.FromWorldLocation(new WorldLocation(new Tile(1, 0), new Vector3(450, 0, 0)));
            editor.UpdatePointerLocation(pointer, segment);
            Assert.IsTrue(editor.CanCommitPlacement, "The production pointer path must produce a usable preview.");

            PathEditResult placement = editor.CommitPlacement();

            Assert.IsFalse(placement.Success, placement.Message);
            Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit, placement.Message);
            Assert.HasCount(1, editor.PendingAmbiguousSpans);
            Assert.AreEqual(1, editor.PendingAmbiguousSpans[0].FromNodeIndex);
            Assert.HasCount(2, editor.PendingAmbiguousSpans[0].Candidates);
            Assert.IsTrue(editor.IsMovingNode);
        }

        private static void BeginInteractiveBuildRouteCandidate(PathEditor editor, TrackWorld trackWorld)
        {
            Assert.IsTrue(editor.TryCaptureCurrentPathModel().PathNodes.Any(node => node.NodeType.Includes(PathNodeType.End)));
            Assert.IsTrue(editor.ContinuePathCommand().Success);
            // The context command retains the junction anchor; a pointer-selected vector constrains the endpoint approach.
            PathEditorCommandResult placement = editor.AddRoutePointHereCommand(
                CreateAnchor(trackWorld, 8, PathNodeType.End, -1), true, false);

            Assert.IsFalse(placement.Success, placement.Message);
            Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit, placement.Message);
            Assert.HasCount(1, editor.PendingAmbiguousSpans);
            Assert.AreEqual(0, editor.PendingAmbiguousSpans[0].FromNodeIndex);
            Assert.IsTrue(editor.IsBuildingRoute);
        }

        private static void PrepareExistingHistory(PathEditor editor, bool withHistory)
        {
            if (!withHistory)
                return;

            Assert.IsTrue(editor.SetMetadataCommand("Existing edit", "Origin", "Destination", false).Success);
            Assert.IsTrue(editor.SetMetadataCommand("Redo edit", "Origin", "Destination", false).Success);
            Assert.IsTrue(editor.Undo());
            Assert.AreEqual((true, true, true), (editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo));
        }

        private static PathModel[] CaptureHistory(PathEditor editor, string fieldName)
            => ((IEnumerable<PathModel>)typeof(PathEditor).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor)).ToArray();

        private static TrainPathToolWindow CreateCandidateBridge(PathEditor editor)
            => new TrainPathToolWindow(() => editor, () => null, action => action(), () => { }, () => { }, _ => { }, () => { }, () => { }) { Active = true };

        private static TrainPathToolWindow CreateQueuedCandidateBridge(Func<PathEditor> owner, Queue<Action> commands)
            => new TrainPathToolWindow(owner, () => null, commands.Enqueue, () => { }, () => { }, _ => { }, () => { }, () => { }) { Active = true };

        private static PathModel CaptureCandidatePreview(PathEditor editor)
            => (PathModel)typeof(PathEditorBase).GetProperty("PreviewPathModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);

        private static void QueueCandidatePreview(TrainPathToolWindow bridge, int fromNodeIndex, int candidateIndex, bool cycle)
        {
            if (cycle)
                bridge.CycleRouteCandidate(1);
            else
                bridge.PreviewRouteCandidate(fromNodeIndex, candidateIndex);
        }

        private static void QueueCandidateAcceptance(TrainPathToolWindow bridge, int fromNodeIndex, bool acceptPreviewed)
        {
            if (acceptPreviewed)
                bridge.AcceptPreviewedRouteCandidate();
            else
                bridge.AcceptRouteCandidate(fromNodeIndex, 0);
        }

        private async Task ReplaceGuidedPreviewSourceAsync(PathEditor editor, TrackWorld world, string replacement)
        {
            PathModel source = editor.TryCaptureCurrentPathModel();
            switch (replacement)
            {
                case "Draft":
                    Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                    break;
                case "LoadSame":
                    Assert.IsTrue(await editor.InitializePathAsync(source, TestContext.CancellationToken).ConfigureAwait(false));
                    break;
                case "LoadOther":
                    Assert.IsTrue(await editor.InitializePathAsync(source with { Id = "replacement-guided-source" }, TestContext.CancellationToken).ConfigureAwait(false));
                    break;
                case "EditUndo":
                    Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                    Assert.IsTrue(editor.SetMetadataCommand("Replacement edit", "Origin", "Destination", false).Success);
                    Assert.IsTrue(editor.Undo());
                    break;
                case "Cancel":
                    Assert.IsTrue(editor.CancelPassingBranchCommand().Success);
                    return;
                case "Unload":
                    Assert.IsTrue(await editor.InitializePathAsync(null, TestContext.CancellationToken).ConfigureAwait(false));
                    return;
                case "Stop":
                    editor.Stop();
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(replacement));
            }

            BeginGuidedPhase(editor, world, 3);
            Assert.IsTrue(editor.PreviewRouteCandidateCommand(editor.GetAmbiguousSpans()[0].FromNodeIndex, 1).Success);
        }

        private static void CancelViaCandidate(PathEditor editor, string cancellation)
        {
            switch (cancellation)
            {
                case "CandidateCancel":
                    editor.CancelPendingRouteCandidate();
                    break;
                case "Escape":
                    // GameWindow.CancelPathEditorInteraction routes Escape to this production bridge command.
                    CreateCandidateBridge(editor).CancelPathInteraction();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cancellation));
            }
        }

        private static PathEditResult SelectStaleCandidate(PathEditor editor, bool accept)
            => accept ? editor.AcceptPendingRouteCandidate(1, 0) : editor.PreviewPendingRouteCandidate(1, 0);

        private static PathEditorCommandResult ExecutePendingFinishCommand(PathEditor editor, string command)
            => command switch
            {
                "Finish" => editor.FinishPathCommand(),
                "FinishHere" => editor.FinishPathHereCommand(),
                "Commit" => editor.CommitPlacementCommand(),
                _ => throw new ArgumentOutOfRangeException(nameof(command)),
            };

        private static void BeginPendingCandidates(PathEditor editor, TrackWorld trackWorld)
        {
            Assert.IsFalse(editor.IsRepairMode);
            PathEditorCommandResult insertion = editor.AddViaPointHereCommand(0,
                CreateAnchor(trackWorld, 4, PathNodeType.Via, -1), true);

            Assert.IsFalse(insertion.Success);
            Assert.IsTrue(editor.HasPendingAmbiguousSpanCommit, insertion.Message);
            Assert.HasCount(2, editor.PendingAmbiguousSpans);
            ResolvedPathSpan upstream = editor.PendingAmbiguousSpans[0];
            ResolvedPathSpan downstream = editor.PendingAmbiguousSpans[1];
            Assert.AreEqual(0, upstream.FromNodeIndex);
            Assert.AreEqual(1, downstream.FromNodeIndex);
            Assert.AreEqual(PathRouteSpanStatus.Ambiguous, upstream.Status);
            Assert.AreEqual(PathRouteSpanStatus.Ambiguous, downstream.Status);
            Assert.HasCount(2, upstream.Candidates);
            Assert.HasCount(2, downstream.Candidates);
            AssertJunctionRoute(expectedArray124, upstream.Candidates[0].RouteNodeIndexes);
            AssertJunctionRoute(expectedArray134, upstream.Candidates[1].RouteNodeIndexes);
            AssertJunctionRoute(expectedArray468, downstream.Candidates[0].RouteNodeIndexes);
            AssertJunctionRoute(expectedArray458, downstream.Candidates[1].RouteNodeIndexes);
        }

        private static void AssertJunctionRoute(int[] expectedRoute, IEnumerable<int> route)
        {
            // Only IDs 1..8 are junctions; inserted vectors and generated direction witnesses are not detours.
            Assert.AreSequenceEqual(expectedRoute, route.Where(index => index is >= 1 and <= 8).ToArray());
        }

        private static void AssertSourceUnchanged(PathEditor editor, PathModel source)
        {
            Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
            Assert.IsFalse(editor.HasUnsavedChanges);
            Assert.IsFalse(editor.CanUndo);
            Assert.IsFalse(editor.CanRedo);
        }

        private static PathModel CreateSourcePath(TrackWorld trackWorld)
        {
            return new PathModel
            {
                Id = "correlated-candidates",
                Name = "Correlated candidates",
                PathNodes = ImmutableArray.Create(
                    CreateAnchor(trackWorld, 1, PathNodeType.Start, 1),
                    CreateAnchor(trackWorld, 8, PathNodeType.End, -1)),
            };
        }

        private static PathNode CreateAnchor(TrackWorld trackWorld, int trackNodeIndex, PathNodeType nodeType, int nextMainNode)
        {
            return new PathNode(trackWorld.TrackDatabase.TrackNodes[trackNodeIndex].Location)
            {
                NodeIndex = trackNodeIndex,
                NodeType = nodeType | PathNodeType.Junction,
                NextMainNode = nextMainNode,
                NextSidingNode = -1,
            };
        }

        private static TrackWorld CreateCorrelatedTrackWorld()
            => CreateCorrelatedTrackWorld(false);

        private static TrackWorld CreateCorrelatedTrackWorld(bool withMainBypass)
        {
            TrackNodeConnectorIndex[] connectors = new TrackNodeConnectorIndex[]
            {
                new TrackNodeConnectorIndex(),
                CreateConnectors(1, (2, ConnectorType.OutPin), (3, ConnectorType.OutPin), (7, ConnectorType.InPin)),
                CreateConnectors(2, (1, ConnectorType.InPin), (4, ConnectorType.OutPin)),
                CreateConnectors(3, (1, ConnectorType.InPin), (4, ConnectorType.OutPin)),
                CreateConnectors(4, (2, ConnectorType.InPin), (3, ConnectorType.OutPin), (5, ConnectorType.InPin), (6, ConnectorType.OutPin)),
                CreateConnectors(5, (4, ConnectorType.OutPin), (8, ConnectorType.InPin)),
                CreateConnectors(6, (4, ConnectorType.InPin), (8, ConnectorType.OutPin)),
                CreateConnectors(7, (1, ConnectorType.OutPin)),
                CreateConnectors(8, (5, ConnectorType.OutPin), (6, ConnectorType.InPin)),
            };
            if (withMainBypass)
            {
                connectors[1] = connectors[1] with { TrackNodeConnectors = connectors[1].TrackNodeConnectors.Add(new TrackNodeConnector { Link = 8, ConnectorType = ConnectorType.OutPin }) };
                connectors[8] = connectors[8] with { TrackNodeConnectors = connectors[8].TrackNodeConnectors.Add(new TrackNodeConnector { Link = 1, ConnectorType = ConnectorType.InPin }) };
            }
            Tile tile = new Tile(1, 0);
            List<TrackNodeBase> nodes = new TrackNodeBase[] { null }.Concat(Enumerable.Range(1, 8).Select(index =>
                (TrackNodeBase)new JunctionNode(new WorldLocation(tile, new Vector3(index * 100, 0, 0)),
                    tile, Vector3.Zero)
                { NodeIndex = index })).ToList();
            List<TrackNodeConnectorIndex> vectorConnectors = connectors.ToList();
            ImmutableDictionary<int, TrackSection>.Builder sectionDefinitions = ImmutableDictionary.CreateBuilder<int, TrackSection>();

            // Keep every original junction pin side, inserting one real vector per undirected edge.
            // ponytail: Collinear, overlapping tracks suffice for candidate rendering, not pointer/snap disambiguation;
            // use spatially separated branches if this fixture grows to cover map interaction.
            for (int startIndex = 1; startIndex <= 8; startIndex++)
            {
                foreach (TrackNodeConnector connector in connectors[startIndex].TrackNodeConnectors.Where(pin => pin.Link > startIndex))
                {
                    int endIndex = connector.Link;
                    int vectorIndex = nodes.Count;
                    WorldLocation start = nodes[startIndex].Location;
                    WorldLocation end = nodes[endIndex].Location;
                    VectorSectionNode section = new VectorSectionNode(start, tile, new Vector3(0, MathHelper.PiOver2, 0), end)
                    {
                        NodeIndex = vectorIndex,
                    };
                    nodes.Add(new VectorNode(start, tile, end)
                    {
                        NodeIndex = vectorIndex,
                        VectorSections = ImmutableArray.Create(section),
                    });
                    sectionDefinitions.Add(vectorIndex, new TrackSection
                    {
                        SectionIndex = vectorIndex,
                        Gauge = 1.435f,
                        Length = (endIndex - startIndex) * 100,
                    });
                    // Vector pins are ordered start/finish. Their directions describe the linked junctions.
                    vectorConnectors.Add(CreateConnectors(vectorIndex, (startIndex, ConnectorType.InPin), (endIndex, ConnectorType.OutPin)));
                    vectorConnectors[startIndex] = vectorConnectors[startIndex] with
                    {
                        TrackNodeConnectors = vectorConnectors[startIndex].TrackNodeConnectors.Select(pin => pin.Link == endIndex
                            ? pin with { Link = vectorIndex, Direction = TrackDirection.Reverse } : pin).ToImmutableArray(),
                    };
                    vectorConnectors[endIndex] = vectorConnectors[endIndex] with
                    {
                        TrackNodeConnectors = vectorConnectors[endIndex].TrackNodeConnectors.Select(pin => pin.Link == startIndex
                            ? pin with { Link = vectorIndex, Direction = TrackDirection.Ahead } : pin).ToImmutableArray(),
                    };
                }
            }

            TrackDatabase database = new TrackDatabase
            {
                TrackNodes = nodes.ToImmutableArray(),
                TrackNodeConnectors = vectorConnectors.ToImmutableArray(),
            };
            TrackWorldTestFixture.InitializeTrackDatabase(database);
            TrackSectionModel sections = new TrackSectionModel { TrackSections = sectionDefinitions.ToImmutable() };
            TrackWorld world = TrackWorld.Initialize(null, new TrackModel { TrackDatabase = database }, sections);
            GameService<RuntimeDataResolver>.Set(null, new TestRuntimeDataResolver(sections, world));
            world.SetSegmentSections(database.VectorNodes.Select(node => new TrackSegmentSection(node.NodeIndex,
                node.VectorSections.Select((section, index) => new TrackSegment(section, node.NodeIndex, index)))));
            world.SetJunctions(database.JunctionNodes.Select(node => new JunctionWidget(node,
                database.TrackNodeConnectors[node.NodeIndex].OutConnectors[node.MainRoute].Link)));
            return world;
        }

        private static TrackNodeConnectorIndex CreateConnectors(int nodeIndex, params (int Link, ConnectorType Type)[] connectors)
        {
            return new TrackNodeConnectorIndex
            {
                NodeIndex = nodeIndex,
                TrackNodeConnectors = connectors.Select(connector => new TrackNodeConnector
                {
                    Link = connector.Link,
                    ConnectorType = connector.Type,
                }).ToImmutableArray(),
            };
        }

        private sealed class TestRuntimeDataResolver : RuntimeDataResolver
        {
            public TestRuntimeDataResolver(TrackSectionModel sections, TrackWorld world)
                : base(new RouteModel(WorldLocation.None) { Name = "Pointer fixture" }, sections, null, world, true, null)
            {
            }
        }

        private sealed class TestPathEditorContext : IPathEditorContext, IPathEditorContextServicesAccessor, IPathEditorServices
        {
            private readonly PathEditorServices services;

            /// <summary>Captures the latest model passed to the production editor rendering service, including previews.</summary>
            public PathModel LastRenderedPathModel { get; private set; }

            /// <summary>No map renderer is needed to exercise candidate state.</summary>
            public IMapRenderer Renderer => null;

            /// <summary>No viewport is needed to exercise candidate state.</summary>
            public IMapViewport Viewport => null;

            /// <summary>Retains the content mode requested by the editor.</summary>
            public ToolboxContentMode ContentMode { get; set; }

            /// <summary>Retains the active editor registered by its base constructor.</summary>
            public PathEditorBase PathEditor { get; set; }

            IPathEditorServices IPathEditorContextServicesAccessor.Services => this;

            TrackWorld IPathEditorServices.TrackWorld => services.TrackWorld;

            /// <summary>Uses the production rendering service with the synthetic initialized track world.</summary>
            public TestPathEditorContext(TrackWorld trackWorld)
            {
                services = new PathEditorServices(trackWorld);
            }

            EditorTrainPath IPathEditorServices.CreateEditorTrainPath(PathModel pathModel)
            {
                LastRenderedPathModel = pathModel;
                return services.CreateEditorTrainPath(pathModel);
            }

            /// <summary>Candidate-state tests do not need to draw the map.</summary>
            public void RequestRedraw()
            {
            }
        }
    }
}
