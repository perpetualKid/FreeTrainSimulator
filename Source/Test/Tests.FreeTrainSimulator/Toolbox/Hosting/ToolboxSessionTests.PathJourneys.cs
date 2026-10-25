using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

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

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Simulator;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenHostedMainPathIsAuthoredThenShellSaveAndSessionReopenPreserveNativePath()
        {
            await VerifyHostedPathJourneyAsync(false).ConfigureAwait(false);
        }

        [TestMethod]
        [Timeout(30000, CooperativeCancellation = true)]
        public async Task WhenHostedPassingPathIsAuthoredThenShellSaveAndSessionReopenPreserveNativePath()
        {
            await VerifyHostedPathJourneyAsync(true).ConfigureAwait(false);
        }

        private async Task VerifyHostedPathJourneyAsync(bool passing)
        {
            RuntimeDataResolver previousRuntime = RuntimeDataResolver.Instance;
            TrackWorld previousWorld = TrackWorld.Instance;
            SyntheticPathFixture fixture = new SyntheticPathFixture(TestContext);
            RouteModel route = fixture.Route;
            CancellationToken cancellationToken = TestContext.CancellationToken;
            // Infrastructure only: the path itself must be authored and saved through the hosted workflow.
            await RouteModelHandler.ToFile(route, cancellationToken).ConfigureAwait(false);
            await TrackModelHandler.Create(fixture.Track, route, cancellationToken).ConfigureAwait(false);
            await TrackSectionsModelHandler.Create(fixture.Sections, route, cancellationToken).ConfigureAwait(false);
            await SignalConfigurationModelHandler.Create(new SignalConfigurationModel
            {
                Id = route.Id,
                Tags = ImmutableDictionary<string, string>.Empty,
            }, route, cancellationToken).ConfigureAwait(false);
            RuntimeDataResolver runtime = await RuntimeDataResolver.PrepareAsync(route, true, null, cancellationToken).ConfigureAwait(false);
            string savedId = "HostedJourney-" + Guid.NewGuid().ToString("N");
            SessionScope scope = await SessionScope.StartAsync(true).ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                StaControlHost shellOwner = await StaControlHost.StartAsync().ConfigureAwait(false);
                await using (shellOwner.ConfigureAwait(false))
                {
                    ConcurrentQueue<Task> commands = new ConcurrentQueue<Task>();
                    TrainPathToolWindow bridge = null;
                    ToolboxGameHostControl host = null;
                    MainWindow shell = null;
                    SavePreflightSynchronizationContext observer = null;
                    EventHandler shellSave = null;
                    TaskCompletionSource<Task> saveDispatch = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
                    EventHandler requested = (sender, _) =>
                    {
                        Assert.AreSame(scope.Game, sender);
                        Assert.AreEqual(scope.Host.OwnerThreadId, Environment.CurrentManagedThreadId);
                        // Replace only the hardware host's event transport, not the shell save handler.
                        saveDispatch.SetResult(shellOwner.OnOwnerAsync(() =>
                        {
                            SynchronizationContext previous = SynchronizationContext.Current;
                            try
                            {
                                SynchronizationContext.SetSynchronizationContext(observer);
                                shellSave(host, EventArgs.Empty);
                            }
                            finally
                            {
                                SynchronizationContext.SetSynchronizationContext(previous);
                            }
                        }));
                    };
                    try
                    {
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            SetField(scope.Game, "userCommandController", new UserCommandController<global::FreeTrainSimulator.Toolbox.UserCommand>());
                            SetField(scope.Game, "pointerInputCapture", Activator.CreateInstance(
                                typeof(GameWindow).GetNestedType("PointerInputCapture", BindingFlags.NonPublic), true));
                            SetField(scope.Game, "<Catalog>k__BackingField", new GetText.Catalog());
                            // Real runtime-bearing ToolboxContent lets the session create and wire its own editor.
                            // Only device composition is headless; no routeData/editor-model injection is used.
                            ToolboxContent content = new ToolboxContent(new MapContentContext(new MapRuntimeServices(runtime),
                                new JourneyMapSession(), null, null));
                            SetField(scope.Session, "toolboxContent", content);
                            FolderModel folder = (FolderModel)route.Parent;
                            SetMenuState(scope, folder, [folder], [route], route);
                            // Match GameWindow's bridge callbacks, retaining the actual tasks behind void callbacks.
                            bridge = new TrainPathToolWindow(() => scope.Game.HostedPathEditor, () => scope.Game.HostedTrainPathToolingContext,
                                action => commands.Enqueue(scope.Game.InvokeSessionCommandAsync(action)), scope.Game.EditPath, scope.Game.SavePath,
                                path => commands.Enqueue(scope.Game.LoadPathAsync(path)), () => commands.Enqueue(scope.Game.UnloadPathAsync()),
                                scope.Game.ActivatePathEditingInput) { Active = true };
                            SetField(scope.Game, "hostedServices", new HostedToolboxServices
                            {
                                Menu = scope.Game.HostedMenu,
                                TrainPathToolWindow = bridge,
                            });
                            scope.Game.SaveTrainPathRequested += requested;
                        }).ConfigureAwait(false);
                        await shellOwner.OnOwnerAsync(() =>
                        {
                            Assert.AreNotEqual(scope.Host.OwnerThreadId, shellOwner.OwnerThreadId);
                            TaskCompletionSource<GameWindow> ready = new TaskCompletionSource<GameWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
                            TaskCompletionSource terminated = Completion();
                            host = CreateShutdownHost(ready, terminated);
                            SetField(host, "gameWindow", scope.Game);
                            SetField(host, "hostPanel", Activator.CreateInstance(
                                typeof(ToolboxGameHostControl).GetNestedType("HostPanel", BindingFlags.NonPublic),
                                new object[] { (Action)(() => { }) }));
                            ready.SetResult(scope.Game);
                            terminated.SetResult();
                            shell = CreateShutdownShell(host);
                            Assert.IsNotNull(SynchronizationContext.Current);
                            observer = new SavePreflightSynchronizationContext(SynchronizationContext.Current, shellOwner.OwnerThreadId);
                            shellSave = (EventHandler)typeof(MainWindow).GetMethod("MapHost_SaveTrainPathRequested", BindingFlags.Instance | BindingFlags.NonPublic)
                                .CreateDelegate(typeof(EventHandler), shell);
                        }).ConfigureAwait(false);

                        await CommandAsync(bridge.StartNewPathPlacement).ConfigureAwait(false);
                        await CommandAsync(() => bridge.SetStartAnchor(SyntheticPathFixture.Node(PathNodeType.Start, 1, -50, 0, -1, -1), false)).ConfigureAwait(false);
                        await CommandAsync(() => bridge.AddRoutePointHere(SyntheticPathFixture.Node(PathNodeType.Junction, 3, 0, 0, -1, -1), true, false)).ConfigureAwait(false);
                        // Pin main vector 4 explicitly: no ambiguous main choice can accidentally select vector 5.
                        await CommandAsync(() => bridge.AddRoutePointHere(SyntheticPathFixture.Node(PathNodeType.Via, 4, 50, 0, -1, -1), false, false)).ConfigureAwait(false);
                        await CommandAsync(() => bridge.AddRoutePointHere(SyntheticPathFixture.Node(PathNodeType.Junction, 6, 100, 0, -1, -1), true, false)).ConfigureAwait(false);
                        await CommandAsync(() => bridge.AddRoutePointHere(SyntheticPathFixture.Node(PathNodeType.End, 2, 150, 0, -1, -1), false, false)).ConfigureAwait(false);
                        await CommandAsync(bridge.FinishPath).ConfigureAwait(false);
                        await CommandAsync(() => bridge.SetMetadata(savedId, "Journey departure", "Journey arrival", false)).ConfigureAwait(false);
                        if (passing)
                        {
                            int split = -1;
                            int rejoin = -1;
                            await scope.Host.OnOwnerAsync(() =>
                            {
                                PathModel main = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel();
                                split = main.PathNodes.IndexOf(main.PathNodes.Single(node => node.Location == SyntheticPathFixture.Location(0, 0)));
                                rejoin = main.PathNodes.IndexOf(main.PathNodes.Single(node => node.Location == SyntheticPathFixture.Location(100, 0)));
                            }).ConfigureAwait(false);
                            await CommandAsync(() => bridge.BeginPassingBranch(split)).ConfigureAwait(false);
                            Assert.AreEqual(PassingBranchAuthoringPhase.SelectingRejoin, bridge.CaptureTrainPathSnapshot().PassingBranchPhase);
                            await CommandAsync(() => bridge.CompletePassingBranch(rejoin)).ConfigureAwait(false);
                            // This geometry has exactly one alternate traversal; there is no candidate to accept.
                            Assert.IsFalse(bridge.CaptureTrainPathSnapshot().HasPendingPassingBranchCandidate);
                            Assert.AreEqual(PassingBranchAuthoringPhase.Idle, bridge.CaptureTrainPathSnapshot().PassingBranchPhase);
                        }

                        await scope.Host.OnOwnerAsync(() =>
                        {
                            PathEditor editor = scope.Session.ExistingPathEditor;
                            Assert.AreEqual((PathEditor.NewPathId, savedId, true, false),
                                (editor.PathId, editor.TryCaptureCurrentPathModel().Name, editor.HasUnsavedChanges, editor.CanCancelPathInteraction));
                            Assert.IsTrue(bridge.CanSavePath);
                            AssertJourneyTopology(editor.TryCaptureCurrentPathModel(), runtime.TrackWorld, passing);
                        }).ConfigureAwait(false);
                        Assert.IsEmpty(await route.GetRoutePaths(cancellationToken).ConfigureAwait(false));

                        await CommandAsync(bridge.SavePath).ConfigureAwait(false);
                        Task postedSave = await saveDispatch.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                        await postedSave.WaitAsync(cancellationToken).ConfigureAwait(false);
                        await observer.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
                        Assert.AreEqual((1, 1, 0), (observer.StartedOperations, observer.CompletedOperations, observer.OffOwnerCallbacks));
                        Assert.IsEmpty(observer.PostedExceptions.ToArray());
                        PathModel saved = null;
                        TrainPathBase savedRenderer = null;
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            bridge.RefreshSnapshot();
                            saved = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel();
                            savedRenderer = scope.Session.ExistingPathEditor.TrainPath;
                            Assert.AreEqual(savedId, saved.Id);
                            AssertJourneySnapshot(scope, bridge, saved, passing);
                            Assert.AreEqual("Path saved.", bridge.CaptureTrainPathSnapshot().CommandResultMessage);
                        }).ConfigureAwait(false);

                        // Bypass all model caches to verify bytes written by the shell/session save, not the editor model.
                        PathModel disk = await JourneyPathStore.ReadPathAsync(savedId, route, cancellationToken).ConfigureAwait(false);
                        Assert.IsNotNull(disk);
                        Assert.AreNotSame(saved, disk);
                        AssertJourneyModel(saved, disk);
                        AssertJourneyTopology(disk, runtime.TrackWorld, passing);
                        TestContext.WriteLine(ModelFileResolver<PathModelHeader>.FilePath(disk) + ContentHandlerBase<PathModelHeader>.SaveStateExtension);

                        await CommandAsync(() => bridge.SelectPath(null)).ConfigureAwait(false);
                        Assert.IsNull(bridge.CaptureTrainPathSnapshot().SelectedPathId);
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            Assert.IsNull(scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel());
                            Assert.IsNull(scope.Session.ExistingPathEditor.TrainPath);
                            Assert.IsFalse(scope.Session.HasUnsavedPathChanges);
                        }).ConfigureAwait(false);
                        PathCache.Remove(route.Hierarchy(savedId));
                        PathModelHeader freshHeader = await JourneyPathStore.ReadHeaderAsync(savedId, route, cancellationToken).ConfigureAwait(false);
                        Assert.IsNotNull(freshHeader);
                        Assert.IsNotInstanceOfType<PathModel>(freshHeader);
                        await scope.Host.OnOwnerAsync(() => bridge.UpdatePaths([freshHeader])).ConfigureAwait(false);
                        await CommandAsync(() => bridge.SelectPath(freshHeader.Id)).ConfigureAwait(false);
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            PathModel reopened = scope.Session.ExistingPathEditor.TryCaptureCurrentPathModel();
                            Assert.AreNotSame(saved, reopened);
                            Assert.AreNotSame(disk, reopened);
                            Assert.AreNotSame(savedRenderer, scope.Session.ExistingPathEditor.TrainPath);
                            Assert.AreEqual(savedRenderer.Length, scope.Session.ExistingPathEditor.TrainPath.Length, 0.01f);
                            AssertJourneyModel(disk, reopened);
                            AssertJourneyTopology(reopened, runtime.TrackWorld, passing);
                            AssertJourneySnapshot(scope, bridge, reopened, passing);
                            Assert.AreEqual((false, false), (scope.Session.ExistingPathEditor.CanUndo, scope.Session.ExistingPathEditor.CanRedo));
                            Assert.IsTrue(bridge.CaptureTrainPathSnapshot().CanContinuePath);
                            Assert.IsFalse(bridge.CanSavePath);
                        }).ConfigureAwait(false);
                    }
                    finally
                    {
                        await scope.Host.OnOwnerAsync(() => scope.Game.SaveTrainPathRequested -= requested).ConfigureAwait(false);
                        await scope.Game.StopForExitAsync().ConfigureAwait(false);
                        PathCache.Remove(route.Hierarchy(savedId));
                        if (host != null)
                        {
                            await shellOwner.OnOwnerAsync(() =>
                            {
                                SetField(host, "ownerResourcesReleased", true);
                                if (shell != null)
                                {
                                    Task shutdown = (Task)typeof(MainWindow).GetMethod("ShutdownAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null);
                                    shutdown.GetAwaiter().GetResult();
                                    shell.Dispose();
                                }
                                else
                                {
                                    host.Dispose();
                                }
                            }).ConfigureAwait(false);
                        }
                    }

                    async Task CommandAsync(Action command)
                    {
                        await shellOwner.OnOwnerAsync(command).ConfigureAwait(false);
                        Assert.IsFalse(commands.IsEmpty, "The bridge must admit a real session command.");
                        while (commands.TryDequeue(out Task operation))
                            await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            bridge.RefreshSnapshot();
                            Assert.IsFalse(bridge.CaptureTrainPathSnapshot().CommandResultIsWarning,
                                bridge.CaptureTrainPathSnapshot().CommandResultMessage);
                        }).ConfigureAwait(false);
                    }
                }
            }
            // Preparing a route-local runtime must not replace the process-wide services.
            Assert.AreSame(previousRuntime, RuntimeDataResolver.Instance);
            Assert.AreSame(previousWorld, TrackWorld.Instance);
        }

        private static void AssertJourneyModel(PathModel expected, PathModel actual)
        {
            Assert.AreEqual((expected.Id, expected.Name, expected.Start, expected.End, expected.PlayerPath, expected.ValidationState),
                (actual.Id, actual.Name, actual.Start, actual.End, actual.PlayerPath, actual.ValidationState));
            Assert.AreSequenceEqual(expected.PathNodes, actual.PathNodes);
        }

        private static void AssertJourneyTopology(PathModel model, TrackWorld world, bool passing)
        {
            PathRouteResolution resolution = PathRouteResolver.Resolve(model, world, CancellationToken.None);
            Assert.IsTrue(resolution.IsValid, string.Join(Environment.NewLine, resolution.Diagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.IsTrue(resolution.MainRoute.Spans.All(span => span.Status == PathRouteSpanStatus.Resolved));
            Assert.AreSequenceEqual(new[] { 1, 4, 2 }, resolution.MainRoute.Spans.SelectMany(span => span.TrackVectorNodeIndexes).Distinct().ToArray());
            Assert.HasCount(passing ? 1 : 0, resolution.PassingRoutes);
            if (passing)
            {
                ResolvedPathRoute branch = resolution.PassingRoutes.Single();
                Assert.IsTrue(branch.Spans.All(span => span.Status == PathRouteSpanStatus.Resolved));
                Assert.AreSequenceEqual(new[] { 5 }, branch.Spans.SelectMany(span => span.TrackVectorNodeIndexes).Distinct().ToArray());
                Assert.AreEqual(SyntheticPathFixture.Location(0, 0), model.PathNodes[branch.StartNodeIndex].Location);
                Assert.AreEqual(SyntheticPathFixture.Location(100, 0), model.PathNodes[branch.EndNodeIndex].Location);
                Assert.IsTrue(model.PathNodes[branch.StartNodeIndex].NextSidingNode >= 0);
            }
            else
            {
                Assert.IsTrue(model.PathNodes.All(node => node.NextSidingNode == -1));
            }
        }

        private static void AssertJourneySnapshot(SessionScope scope, TrainPathToolWindow bridge, PathModel model, bool passing)
        {
            TrainPathSnapshot snapshot = bridge.CaptureTrainPathSnapshot();
            Assert.AreEqual((model.Id, model.Name, "Journey departure", "Journey arrival", false),
                (snapshot.SelectedPathId, snapshot.PathName, snapshot.PathStart, snapshot.PathEnd, snapshot.PlayerPath));
            Assert.AreEqual((false, false, false, false, PathEditorPlacementMode.None, PassingBranchAuthoringPhase.Idle),
                (snapshot.IsRepairMode, snapshot.CanCancelPathInteraction, snapshot.HasPendingAmbiguousSpanCommit,
                snapshot.HasPendingPassingBranchCandidate, snapshot.PlacementMode, snapshot.PassingBranchPhase));
            Assert.IsFalse(snapshot.Diagnostics.Any(diagnostic => diagnostic.Severity >= PathRouteDiagnosticSeverity.Error));
            Assert.AreEqual((model.Id, false), (snapshot.Paths.Single().Id, snapshot.Paths.Single().HasUnsavedChanges));
            Assert.AreEqual(PathValidationState.Valid, model.ValidationState);
            Assert.IsFalse(scope.Session.HasUnsavedPathChanges);
            Assert.IsFalse(scope.Session.ExistingPathEditor.IsSaveInProgress);
            Assert.IsInstanceOfType<EditorTrainPath>(scope.Session.ExistingPathEditor.TrainPath);
            Assert.HasCount(model.PathNodes.Length, snapshot.Nodes);
        }

        private abstract class JourneyPathStore : ContentHandlerBase<PathModelHeader>
        {
            internal static Task<PathModel> ReadPathAsync(string id, RouteModelHeader route, CancellationToken cancellationToken)
                => FromFile<PathModel, RouteModelHeader>(id, route, cancellationToken);

            internal static Task<PathModelHeader> ReadHeaderAsync(string id, RouteModelHeader route, CancellationToken cancellationToken)
                => FromFile(id, route, cancellationToken);
        }

        /// <summary>Fixture-only device composition; real ToolboxContent/runtime/editor services remain in use.</summary>
        private sealed class JourneyMapSession : IMapSessionComposer, IXnaMapShellSession
        {
            public IMapRenderer Renderer => null;
            public IMapViewport Viewport => null;
            public IMapHostControl HostControl => null;
            public IXnaMapShellHost ShellHost { get; }
            IMapShellHost IMapShellSession.ShellHost => ShellHost;

            internal JourneyMapSession()
            {
                // Selection only needs ContentArea's redraw flag. Never initialize, draw or dispose this GPU-less shell.
                ContentArea redrawTarget = (ContentArea)RuntimeHelpers.GetUninitializedObject(typeof(ContentArea));
#pragma warning disable CA1816 // Intentional: uninitialized test object must not be finalized.
                GC.SuppressFinalize(redrawTarget);
#pragma warning restore CA1816
                ShellHost = new XnaMapShellHost(redrawTarget);
            }

            public IMapSession Compose(MapSessionRequest request) => this;

            public void Dispose() { }
        }
    }
}
