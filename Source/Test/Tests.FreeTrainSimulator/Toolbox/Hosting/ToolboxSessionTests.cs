using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.Settings;
using FreeTrainSimulator.Toolbox.ToolWindows;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Tests.FreeTrainSimulator.Common;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    [TestClass]
    public sealed partial class ToolboxSessionTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenStoppedThenNewFolderSelectionIsRejected()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).ConfigureAwait(false);
                FolderModel folder = new FolderModel("New", "C:\\Unused", null);

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scope.Game.InvokeOnGameThreadAsync(
                    () => scope.Session.SelectFolderAsync(folder))).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenStoppedThenLateScreenshotSubmissionIsRejectedBeforeDeviceAccess()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).ConfigureAwait(false);

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scope.Game.InvokeOnGameThreadAsync(
                    () => scope.Game.SaveScreenshotAsync("unused.png"))).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenStoppedThenLateSaveSubmissionIsRejected()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).ConfigureAwait(false);
                TrainPathSaveRequest request = new TrainPathSaveRequest(new PathModelHeader { Id = "path" }, "path", false);

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scope.Game.InvokeOnGameThreadAsync(
                    () => scope.Session.SubmitTrainPathSaveAsync(request))).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenStoppedThenValidationIsRejectedBeforeReadingRoute()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).ConfigureAwait(false);

                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scope.Game.InvokeOnGameThreadAsync(
                    () => scope.Session.ValidateAllPathsAsync(_ => Assert.Fail("Rejected validation must not refresh.")))).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenValidationIsPausedThenStopWaitsForPersistenceWithoutPublishing()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                RouteModel route = CreateValidationRoute();
                PathModel source = new PathModel() { Id = "pending-validation", Name = "Pending Validation" };
                _ = await route.Save(source, TestContext.CancellationToken).ConfigureAwait(false);
                PathModelHeader header = (await route.GetRoutePaths(TestContext.CancellationToken).ConfigureAwait(false)).Single();
                TaskCompletionSource<PathModelHeader> read = new TaskCompletionSource<PathModelHeader>(TaskCreationOptions.RunContinuationsAsynchronously);
                PathCache.Set(header.Hierarchy(), read.Task);
                bool refreshed = false;
                Task<bool> validation = null;
                Task drain = null;
                bool waits = false;
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetValidationRoute(scope.Session, route);
                    validation = scope.Session.ValidateAllPathsAsync(_ => refreshed = true);
                    drain = scope.Session.StopAndDrainAsync();
                    waits = !drain.IsCompleted;
                }).ConfigureAwait(false);
                try
                {
                    read.SetResult(source);
                    await Task.WhenAll(validation, drain).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    PathModelHeader persisted = (await route.GetPaths(TestContext.CancellationToken).ConfigureAwait(false)).Single();

                    Assert.AreEqual((true, true, false, PathValidationState.Invalid),
                        (waits, await validation.ConfigureAwait(false), refreshed, persisted.ValidationState));
                }
                finally
                {
                    read.TrySetResult(source);
                    await validation.ConfigureAwait(false);
                    PathCache.Remove(header.Hierarchy());
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenValidationCompletesThenRefreshRunsOnOwnerBeforeCompletion()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                RouteModel route = CreateValidationRoute();
                _ = await route.Save(new PathModel { Id = "validate", Name = "Validate" }, TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() => SetValidationRoute(scope.Session, route)).ConfigureAwait(false);
                int refreshThread = 0;
                PathValidationState state = PathValidationState.NotValidated;

                bool validated = await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.ValidateAllPathsAsync(paths =>
                {
                    refreshThread = Environment.CurrentManagedThreadId;
                    state = paths.Single().ValidationState;
                })).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreEqual((true, scope.Host.OwnerThreadId, PathValidationState.Invalid), (validated, refreshThread, state));
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenRouteChangesDuringValidationThenOldPathsAreNotPublished()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                RouteModel route = CreateValidationRoute();
                PathModel source = new PathModel() { Id = "old-validation", Name = "Old Validation" };
                _ = await route.Save(source, TestContext.CancellationToken).ConfigureAwait(false);
                PathModelHeader header = (await route.GetRoutePaths(TestContext.CancellationToken).ConfigureAwait(false)).Single();
                TaskCompletionSource<PathModelHeader> read = new TaskCompletionSource<PathModelHeader>(TaskCreationOptions.RunContinuationsAsynchronously);
                PathCache.Set(header.Hierarchy(), read.Task);
                bool refreshed = false;
                Task<bool> validation = null;
                await scope.Host.OnOwnerAsync(() =>
                {
                    SetValidationRoute(scope.Session, route);
                    validation = scope.Session.ValidateAllPathsAsync(_ => refreshed = true);
                    SetField(scope.Session, "selectedRoute", CreateValidationRoute());
                }).ConfigureAwait(false);
                try
                {
                    read.SetResult(source);
                    _ = await validation.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                    Assert.IsFalse(refreshed);
                }
                finally
                {
                    read.TrySetResult(source);
                    await validation.ConfigureAwait(false);
                    PathCache.Remove(header.Hierarchy());
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenStoppedThenGameLevelMoveReleaseDoesNotCommit()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                PathEditor editor = null;
                PathModel source = null;
                UserCommandArgs release = new UserCommandArgs();
                await scope.Host.OnOwnerAsync(() =>
                {
                    editor = CreateEditor();
                    editor.InitializeNewPath();
                    source = editor.TryCaptureCurrentPathModel();
                    SetField(scope.Session, "pathEditor", editor);
                    _ = scope.Session.StopAndDrainAsync();
                    SetField(editor, "placementMode", PathEditorPlacementMode.MoveNode);
                    SetField(editor, "movingNodeIndex", 0);
                    SetField(editor, "movePreviewModel", new PathModel { Id = "stale-preview" });
                    scope.Game.CommitPendingNodeMove(release);
                }).ConfigureAwait(false);

                Assert.AreEqual((source, false), (editor.TryCaptureCurrentPathModel(), release.Handled));
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenStoppedThenSettingsSettersResetAndAppliersHaveNoEffects()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                ProfileToolboxSettingsModel settings = scope.Game.ToolboxSettings;
                settings.RestoreLastView = false;
                settings.FontOutline = false;
                settings.LimitTrackWidth = false;
                settings.ColorSettings[ColorSetting.Background] = "Red";
                settings.ViewSettings[MapContentType.Tracks] = false;
                scope.Game.ToolboxUserSettings.LogLevel = TraceEventType.Critical;
                scope.Game.ToolboxUserSettings.Language = "unchanged";
                SettingsToolWindow bridge = new SettingsToolWindow(settings, scope.Game.ToolboxUserSettings, scope.Game, scope.Game.InvokeSessionCommandAsync);
                await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).ConfigureAwait(false);

                bridge.SetRestoreLastView(true);
                bridge.SetItemVisibility(MapContentType.Tracks, true);
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => bridge.ResetToDefaultsAsync()).ConfigureAwait(false);
                bridge.SetEnableLogging(true);
                bridge.SetFontOutline(true);
                bridge.SetRealTrackWidth(false);
                bridge.SetColorPreference(ColorSetting.Background, "Blue");
                bridge.SetLanguage("changed");
                ((ISettingsApplier)scope.Game).ReapplyAppearance();
                await scope.Host.OnOwnerAsync(() => { }).ConfigureAwait(false);

                Assert.AreEqual((false, false, false, "Red", false, TraceEventType.Critical, "unchanged"),
                    (settings.RestoreLastView, settings.FontOutline, settings.LimitTrackWidth, settings.ColorSettings[ColorSetting.Background],
                    settings.ViewSettings[MapContentType.Tracks], scope.Game.ToolboxUserSettings.LogLevel, scope.Game.ToolboxUserSettings.Language));
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenSettingsCommandIsQueuedBeforeStopThenAdmissionRejectsItOnOwner()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                scope.Game.ToolboxSettings.RestoreLastView = false;
                SettingsToolWindow bridge = new SettingsToolWindow(scope.Game.ToolboxSettings, scope.Game.ToolboxUserSettings, scope.Game, scope.Game.InvokeSessionCommandAsync);
                TaskCompletionSource entered = Completion();
                using (ManualResetEventSlim release = new ManualResetEventSlim())
                {
                    Task blocker = scope.Host.OnOwnerAsync(() =>
                    {
                        entered.SetResult();
                        release.Wait(TestContext.CancellationToken);
                        _ = scope.Session.StopAndDrainAsync();
                    });
                    try
                    {
                        await entered.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        bridge.SetRestoreLastView(true);
                    }
                    finally
                    {
                        release.Set();
                        await blocker.ConfigureAwait(false);
                    }
                    await scope.Host.OnOwnerAsync(() => { }).ConfigureAwait(false);

                    Assert.IsFalse(scope.Game.ToolboxSettings.RestoreLastView);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenJobIsPausedThenStopWaitsForItsRequiredOwnerCleanup()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                TaskCompletionSource started = Completion();
                TaskCompletionSource releaseWork = Completion();
                TaskCompletionSource cleanupEntered = Completion();
                TaskCompletionSource releaseCleanup = Completion();
                int cleanupThread = 0;
                Task job = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.RunAsync(async () =>
                {
                    started.SetResult();
                    await releaseWork.Task.ConfigureAwait(false);
                    await scope.Game.InvokeOnGameThreadAsync(async () =>
                    {
                        cleanupThread = Environment.CurrentManagedThreadId;
                        cleanupEntered.SetResult();
                        await releaseCleanup.Task.ConfigureAwait(true);
                    }).ConfigureAwait(false);
                }));
                try
                {
                    await started.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    Task drain = null;
                    Task secondStop = null;
                    await scope.Host.OnOwnerAsync(() =>
                    {
                        drain = scope.Session.StopAndDrainAsync();
                        secondStop = scope.Session.StopAndDrainAsync();
                    }).ConfigureAwait(false);
                    bool waitsForWork = !drain.IsCompleted;
                    releaseWork.SetResult();
                    await cleanupEntered.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    bool waitsForCleanup = !drain.IsCompleted;
                    releaseCleanup.SetResult();
                    await Task.WhenAll(job, drain).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                    Assert.AreEqual((true, true, true, scope.Host.OwnerThreadId),
                        (waitsForWork, waitsForCleanup, ReferenceEquals(drain, secondStop), cleanupThread));
                }
                finally
                {
                    releaseWork.TrySetResult();
                    releaseCleanup.TrySetResult();
                    await job.ConfigureAwait(false);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenInlineJobRequestsStopThenItWasAlreadyAdmitted()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                TaskCompletionSource release = Completion();
                Task drain = null;
                bool waited = false;
                Task job = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.RunAsync(() =>
                {
                    drain = scope.Session.StopAndDrainAsync();
                    waited = !drain.IsCompleted;
                    return release.Task;
                }));
                try
                {
                    await scope.Host.OnOwnerAsync(() => release.SetResult()).ConfigureAwait(false);
                    await job.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await drain.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                    Assert.IsTrue(waited);
                }
                finally
                {
                    release.TrySetResult();
                    await job.ConfigureAwait(false);
                }
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenAdmittedJobFailsDuringStopThenDrainReportsItsFailure()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            try
            {
                TaskCompletionSource started = Completion();
                TaskCompletionSource result = Completion();
                InvalidOperationException expected = new InvalidOperationException("Persistence failed.");
                Task job = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.RunAsync(() =>
                {
                    started.SetResult();
                    return result.Task;
                }));
                await started.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                Task drain = scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync);
                await scope.Host.OnOwnerAsync(() => result.SetException(expected)).ConfigureAwait(false);
                GameWindow.ObserveGameThreadOperation(job);

                InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => drain.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);

                Assert.AreSame(expected, actual);
            }
            finally
            {
                // Disposal releases on the owner but must not hide the cached persistence diagnostic.
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask()).ConfigureAwait(false);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenStoppedDuringConfirmationThenPendingInteractionCompletesWithoutDiscardingEditor()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                TaskCompletionSource<UnsavedPathConfirmationEventArgs> requested = new TaskCompletionSource<UnsavedPathConfirmationEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = CreateEditor();
                    SetField(editor, "unsavedChanges", true);
                    SetField(scope.Session, "pathEditor", editor);
                    scope.Game.UnsavedPathConfirmationRequested += (_, args) => requested.SetResult(args);
                }).ConfigureAwait(false);
                Task<bool> confirmation = scope.Game.InvokeOnGameThreadAsync(scope.Session.ConfirmDiscardUnsavedPathsAsync);
                UnsavedPathConfirmationEventArgs prompt = await requested.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                bool accepted = await confirmation.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreEqual((false, true, true), (accepted, prompt.Completion.Task.IsCompletedSuccessfully, scope.Session.ExistingPathEditor.HasUnsavedChanges));
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenOlderConfirmationIsAcceptedAfterNewIntentThenAcceptedFolderIsPreserved()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                FolderModel original = new FolderModel("Original", "C:\\Original", null);
                TaskCompletionSource<UnsavedPathConfirmationEventArgs> firstPrompt = new TaskCompletionSource<UnsavedPathConfirmationEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource<UnsavedPathConfirmationEventArgs> secondPrompt = new TaskCompletionSource<UnsavedPathConfirmationEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
                await scope.Host.OnOwnerAsync(() =>
                {
                    PathEditor editor = CreateEditor();
                    SetField(editor, "unsavedChanges", true);
                    SetField(scope.Session, "pathEditor", editor);
                    SetField(scope.Session, "selectedFolder", original);
                    scope.Game.UnsavedPathConfirmationRequested += (_, args) => firstPrompt.TrySetResult(args);
                }).ConfigureAwait(false);
                Task first = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SelectFolderAsync(new FolderModel("First", "C:\\First", null)));
                UnsavedPathConfirmationEventArgs firstConfirmation = await firstPrompt.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() => scope.Game.UnsavedPathConfirmationRequested += (_, args) => secondPrompt.TrySetResult(args)).ConfigureAwait(false);
                Task second = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.SelectFolderAsync(new FolderModel("Second", "C:\\Second", null)));
                UnsavedPathConfirmationEventArgs secondConfirmation = await secondPrompt.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                firstConfirmation.Completion.SetResult(true);
                secondConfirmation.Completion.SetResult(false);
                await Task.WhenAll(first, second).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                Assert.AreSame(original, scope.Session.SelectedFolder);
            }
        }

        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenSessionPathWaiterIsCanceledThenSharedReadRemainsUsable()
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                RouteModel route = new RouteModel(WorldLocation.None) { Id = Guid.NewGuid().ToString("N") };
                route.Initialize(new FolderModel("Session", "C:\\Unused", null));
                PathModelHeader header = new PathModelHeader() { Id = "shared" };
                header.Initialize(route);
                TaskCompletionSource<PathModelHeader> sharedRead = new TaskCompletionSource<PathModelHeader>(TaskCreationOptions.RunContinuationsAsynchronously);
                PathCache.Set(header.Hierarchy(), sharedRead.Task);
                await scope.Host.OnOwnerAsync(() => SetField(scope.Session, "pathEditor", CreateEditor())).ConfigureAwait(false);
                try
                {
                    Task<bool> waiter = scope.Game.InvokeOnGameThreadAsync(() => scope.Session.LoadPathAsync(header));
                    await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                    await Assert.ThrowsAsync<OperationCanceledException>(() => waiter.WaitAsync(TestContext.CancellationToken)).ConfigureAwait(false);
                    PathModel loaded = new PathModel() { Id = header.Id, PathNodes = ImmutableArray<PathNode>.Empty };
                    loaded.Initialize(route);
                    sharedRead.SetResult(loaded);

                    PathModel reused = await header.GetExtended(CancellationToken.None).ConfigureAwait(false);

                    Assert.AreSame(loaded, reused);
                }
                finally
                {
                    sharedRead.TrySetResult(null);
                    PathCache.Remove(header.Hierarchy());
                }
            }
        }

        [TestMethod]
        [DataRow("saveDialogEditor")]
        [DataRow("saveDialogSource")]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenRouteIsUnloadedThenSaveDialogReferenceIsReleased(string fieldName)
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                RouteModelHeader route = new RouteModel(WorldLocation.None) { Id = "save-dialog-route" };
                FieldInfo captureField = typeof(GameWindow.ToolboxSession).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                bool captured = false;
                bool released = false;
                await scope.Host.OnOwnerAsync(() =>
            {
                PathEditor editor = CreateEditor();
                editor.InitializeNewPath();
                SetField(scope.Session, "pathEditor", editor);
                SetField(scope.Session, "selectedRoute", route);
                scope.Game.UnsavedPathConfirmationRequested += (_, args) => args.Completion.SetResult(true);
                _ = scope.Session.CaptureTrainPathSaveState();
                captured = captureField.GetValue(scope.Session) != null;
            }).ConfigureAwait(false);

                await scope.Game.InvokeOnGameThreadAsync(() => scope.Session.ToggleRouteAsync(route))
                    .WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                await scope.Host.OnOwnerAsync(() => released = captureField.GetValue(scope.Session) == null).ConfigureAwait(false);

                Assert.AreEqual((true, true), (captured, released));
            }
        }

        private static TaskCompletionSource Completion() => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        private static RouteModel CreateValidationRoute()
        {
            string suffix = Guid.NewGuid().ToString("N");
            RouteModel route = new RouteModel(WorldLocation.None) { Id = suffix, Name = "Session Validation" };
            route.Initialize(new FolderModel(suffix, Path.Combine(Path.GetTempPath(), suffix), new ContentModel()));
            return route;
        }

        private static void SetValidationRoute(GameWindow.ToolboxSession session, RouteModelHeader route)
        {
            ToolboxContent content = (ToolboxContent)RuntimeHelpers.GetUninitializedObject(typeof(ToolboxContent));
            typeof(ContentBase).GetField("trackWorld", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(content,
                TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld());
            SetField(session, "toolboxContent", content);
            SetField(session, "selectedRoute", route);
        }

        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static PathEditor CreateEditor() => new PathEditor(new EditorContext());

        private static GameWindow CreateSessionGame(System.Windows.Forms.Form renderForm, System.Windows.Forms.Control ownerControl)
        {
            GameWindow game = (GameWindow)RuntimeHelpers.GetUninitializedObject(typeof(GameWindow));
#pragma warning disable CA1816 // Intentional: uninitialized test object must not be finalized.
            GC.SuppressFinalize(game);
#pragma warning restore CA1816
            SetField(game, "windowForm", renderForm);
            SetField(game, "ownerControl", ownerControl);
            SetField(game, "<ToolboxSettings>k__BackingField", new ProfileToolboxSettingsModel());
            SetField(game, "<ToolboxUserSettings>k__BackingField", new ProfileUserSettingsModel());
            HostedToolboxMenu menu = new HostedToolboxMenu(game);
            SetField(game, "hostedMenu", menu);
            SetField(game, "menu", menu);
            return game;
        }

        private sealed class EditorContext : IPathEditorContext, IPathEditorContextServicesAccessor
        {
            private readonly IPathEditorServices services = new PathEditorServices(TrackWorldTestFixture.CreateSingleVectorNodeTrackWorld());
            public IMapRenderer Renderer => null;
            public IMapViewport Viewport => null;
            public ToolboxContentMode ContentMode { get; set; }
            public PathEditorBase PathEditor { get; set; }
            public void RequestRedraw() { }
            IPathEditorServices IPathEditorContextServicesAccessor.Services => services;
        }

        private abstract class PathCache : ContentHandlerBase<PathModelHeader>
        {
            internal static void Set(string key, Task<PathModelHeader> task) => modelTaskCache[key] = task;
            internal static void Remove(string key) => modelTaskCache.TryRemove(key, out _);
        }

        internal sealed class SessionScope : IAsyncDisposable
        {
            internal StaControlHost Host { get; }
            internal GameWindow Game { get; }
            internal GameWindow.ToolboxSession Session { get; }

            private SessionScope(StaControlHost host)
            {
                Host = host;
                Game = CreateSessionGame(host.Target, host.DispatchControl);
                Session = new GameWindow.ToolboxSession(Game);
                try
                {
                    SetField(Game, "session", Session);
                }
                catch
                {
                    Session.Dispose();
                    throw;
                }
            }

            internal static async Task<SessionScope> StartAsync()
            {
                StaControlHost host = null;
                try
                {
                    host = await StaControlHost.StartAsync().ConfigureAwait(false);
                    SessionScope scope = null;
                    await host.OnOwnerAsync(() => scope = new SessionScope(host)).ConfigureAwait(false);
                    host = null; // The returned scope now owns the host.
                    return scope;
                }
                finally
                {
                    if (host != null)
                        await host.DisposeAsync().ConfigureAwait(false);
                }
            }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    await Game.InvokeOnGameThreadAsync(Session.StopAndDrainAsync).ConfigureAwait(false);
                }
                finally
                {
                    try
                    {
                        await Host.OnOwnerAsync(Session.Dispose).ConfigureAwait(false);
                    }
                    finally
                    {
                        await Host.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
