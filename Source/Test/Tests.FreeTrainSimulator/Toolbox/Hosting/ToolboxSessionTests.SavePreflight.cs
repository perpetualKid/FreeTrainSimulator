using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.PathEditing;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    public sealed partial class ToolboxSessionTests
    {
        [TestMethod]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenShellSaveCapturesStoppedSessionThenPreflightCompletesWithoutChangingEditorOrStoredPath()
        {
            await VerifySavePreflightFailureAsync(true, null).ConfigureAwait(false);
        }

        [TestMethod]
        [DataRow(nameof(InvalidOperationException))]
        [DataRow(nameof(IOException))]
        [DataRow(nameof(UnauthorizedAccessException))]
        [DataRow("Canceled")]
        [Timeout(10000, CooperativeCancellation = true)]
        public async Task WhenShellSaveAsLookupFailsThenPreflightCompletesWithoutChangingEditorOrStoredPath(string failure)
        {
            Exception lookupFailure = failure switch
            {
                nameof(InvalidOperationException) => new InvalidOperationException("Save preflight lookup failed."),
                nameof(IOException) => new IOException("Save preflight lookup failed."),
                nameof(UnauthorizedAccessException) => new UnauthorizedAccessException("Save preflight lookup failed."),
                "Canceled" => null,
                _ => throw new ArgumentOutOfRangeException(nameof(failure)),
            };
            await VerifySavePreflightFailureAsync(false, lookupFailure).ConfigureAwait(false);
        }

        private async Task VerifySavePreflightFailureAsync(bool stopBeforeCapture, Exception lookupFailure)
        {
            SessionScope scope = await SessionScope.StartAsync().ConfigureAwait(false);
            await using (scope.ConfigureAwait(false))
            {
                StaControlHost shellOwner = await StaControlHost.StartAsync().ConfigureAwait(false);
                await using (shellOwner.ConfigureAwait(false))
                {
                    RouteModel route = CreateValidationRoute();
                    string targetId = "preflight-" + Guid.NewGuid().ToString("N");
                    PathModel persisted = new PathModel { Id = targetId, Name = "Existing saved path" };
                    _ = await route.Save(persisted, TestContext.CancellationToken).ConfigureAwait(false);
                    string fileName = ModelFileResolver<PathModelHeader>.FilePath(persisted) + ContentHandlerBase<PathModelHeader>.SaveStateExtension;
                    TestContext.WriteLine(fileName);
                    byte[] originalBytes = await File.ReadAllBytesAsync(fileName, TestContext.CancellationToken).ConfigureAwait(false);
                    // Consume the save's refresh flag before supplying the pending route-path lookup.
                    _ = await route.GetRoutePaths(TestContext.CancellationToken).ConfigureAwait(false);
                    TaskCompletionSource<ImmutableArray<PathModelHeader>> lookup = new TaskCompletionSource<ImmutableArray<PathModelHeader>>(TaskCreationOptions.RunContinuationsAsynchronously);
                    ToolboxGameHostControl host = null;
                    MainWindow shell = null;
                    SavePreflightSynchronizationContext observer = null;
                    PathEditor editor = null;
                    PathModel source = null;
                    PathModel[] undoHistory = null;
                    PathModel[] redoHistory = null;
                    MenuPathCache.SetPaths(route, lookup.Task);
                    try
                    {
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            editor = CreateEditor();
                            editor.InitializeNewPath();
                            Assert.IsTrue(editor.SetMetadataCommand(targetId, "Start", "End", true).Success);
                            Assert.IsTrue(editor.SetMetadataCommand("Later unsaved name", "Start", "End", true).Success);
                            Assert.IsTrue(editor.Undo());
                            Assert.AreEqual((true, true, true), (editor.HasUnsavedChanges, editor.CanUndo, editor.CanRedo));
                            source = editor.TryCaptureCurrentPathModel();
                            undoHistory = SavePreflightHistory(editor, "undoHistory");
                            redoHistory = SavePreflightHistory(editor, "redoHistory");
                            FolderModel folder = (FolderModel)route.Parent;
                            SetMenuState(scope, folder, [folder], [route], route);
                            SetField(scope.Session, "pathEditor", editor);
                            Assert.IsTrue(new TrainPathSaveRequest(
                                TrainPathSaveRequest.PreparePathDetails(source, source.Id), source.Id, false).IsSaveAs);
                        }).ConfigureAwait(false);
                        if (stopBeforeCapture)
                            await scope.Game.InvokeOnGameThreadAsync(scope.Session.StopAndDrainAsync).WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);

                        using ManualResetEventSlim releaseCapture = new ManualResetEventSlim();
                        TaskCompletionSource captureBlocked = Completion();
                        Task captureBlock = scope.Host.OnOwnerAsync(() =>
                        {
                            captureBlocked.SetResult();
                            releaseCapture.Wait();
                        });
                        try
                        {
                            await captureBlocked.Task.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                            await shellOwner.OnOwnerAsync(() =>
                            {
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
                                SynchronizationContext previous = SynchronizationContext.Current;
                                Assert.IsNotNull(previous);
                                observer = new SavePreflightSynchronizationContext(previous, shellOwner.OwnerThreadId);
                                try
                                {
                                    SynchronizationContext.SetSynchronizationContext(observer);
                                    typeof(MainWindow).GetMethod("MapHost_SaveTrainPathRequested", BindingFlags.Instance | BindingFlags.NonPublic)
                                        .Invoke(shell, new object[] { host, EventArgs.Empty });
                                }
                                finally
                                {
                                    SynchronizationContext.SetSynchronizationContext(previous);
                                }
                            }).ConfigureAwait(false);
                        }
                        finally
                        {
                            // Capture cannot complete before the handler has suspended on the shell STA.
                            releaseCapture.Set();
                            await captureBlock.ConfigureAwait(false);
                        }

                        if (!stopBeforeCapture)
                        {
                            // Let the shell post lookup to the owner before faulting it; faulted cache entries
                            // intentionally reload, whereas a fault on an already awaited lookup reaches preflight.
                            await observer.FirstContinuation.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                            await scope.Host.OnOwnerAsync(() =>
                            {
                                if (lookupFailure == null)
                                    lookup.SetCanceled();
                                else
                                    lookup.SetException(lookupFailure);
                            }).ConfigureAwait(false);
                        }

                        await observer.Completion.WaitAsync(TestContext.CancellationToken).ConfigureAwait(false);
                        Assert.AreEqual((1, 1, 0), (observer.StartedOperations, observer.CompletedOperations, observer.OffOwnerCallbacks));
                        Assert.IsEmpty(observer.PostedExceptions.ToArray());
                        await scope.Host.OnOwnerAsync(() =>
                        {
                            Assert.AreSame(editor, scope.Session.ExistingPathEditor);
                            Assert.AreSame(source, editor.TryCaptureCurrentPathModel());
                            Assert.AreEqual((PathEditor.NewPathId, targetId, true, false),
                                (editor.PathId, source.Name, editor.HasUnsavedChanges, editor.IsSaveInProgress));
                            Assert.AreSequenceEqual(undoHistory, SavePreflightHistory(editor, "undoHistory"));
                            Assert.AreSequenceEqual(redoHistory, SavePreflightHistory(editor, "redoHistory"));
                        }).ConfigureAwait(false);
                        byte[] persistedBytes = await File.ReadAllBytesAsync(fileName, TestContext.CancellationToken).ConfigureAwait(false);
                        Assert.AreSequenceEqual(originalBytes, persistedBytes);
                    }
                    finally
                    {
                        lookup.TrySetCanceled();
                        MenuPathCache.RemovePaths(route);
                        await scope.Game.StopForExitAsync().ConfigureAwait(false);
                        if (host != null)
                        {
                            await shellOwner.OnOwnerAsync(() =>
                            {
                                SetField(host, "ownerResourcesReleased", true);
                                if (shell != null)
                                {
                                    // Both synthetic host signals are complete, so shell cleanup cannot wait on this STA.
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
                }
            }
        }

        private static PathModel[] SavePreflightHistory(PathEditor editor, string fieldName)
            => ((Stack<PathModel>)typeof(PathEditor).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor)).ToArray();

        private sealed class SavePreflightSynchronizationContext : SynchronizationContext
        {
            private readonly SynchronizationContext underlying;
            private readonly int ownerThreadId;
            private readonly TaskCompletionSource completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource firstContinuation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            private int startedOperations;
            private int completedOperations;
            private int offOwnerCallbacks;

            internal Task Completion => completion.Task;
            internal Task FirstContinuation => firstContinuation.Task;
            internal int StartedOperations => Volatile.Read(ref startedOperations);
            internal int CompletedOperations => Volatile.Read(ref completedOperations);
            internal int OffOwnerCallbacks => Volatile.Read(ref offOwnerCallbacks);
            internal ConcurrentQueue<Exception> PostedExceptions { get; } = new ConcurrentQueue<Exception>();

            internal SavePreflightSynchronizationContext(SynchronizationContext underlying, int ownerThreadId)
            {
                this.underlying = underlying;
                this.ownerThreadId = ownerThreadId;
            }

            public override void OperationStarted() => Interlocked.Increment(ref startedOperations);

            public override void OperationCompleted()
            {
                Interlocked.Increment(ref completedOperations);
                // AsyncVoidMethodBuilder posts its exception before notifying completion. Queue behind it.
                underlying.Post(_ => completion.TrySetResult(), null);
            }

            public override void Post(SendOrPostCallback callback, object state)
            {
                underlying.Post(_ =>
                {
                    if (Environment.CurrentManagedThreadId != ownerThreadId)
                        Interlocked.Increment(ref offOwnerCallbacks);
                    SynchronizationContext previous = Current;
                    try
                    {
                        SetSynchronizationContext(this);
                        callback(state);
                    }
                    catch (Exception exception)
                    {
                        // Observe async-void failures without letting the STA's unhandled-exception policy terminate the test host.
                        PostedExceptions.Enqueue(exception);
                    }
                    finally
                    {
                        SetSynchronizationContext(previous);
                        firstContinuation.TrySetResult();
                    }
                }, null);
            }
        }
    }
}
