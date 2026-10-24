using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.PathEditing;

namespace FreeTrainSimulator.Toolbox
{
    /// <summary>Owns route, editor and admitted operation lifetime; the game owns platform resources and transport.</summary>
    internal sealed partial class ToolboxSession : IDisposable
    {
        private readonly GameWindow game;
        private readonly RouteLoadCoordinator routeLoads;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly HashSet<Task> jobs = new HashSet<Task>();
        private readonly HashSet<UnsavedPathConfirmationEventArgs> confirmations = new HashSet<UnsavedPathConfirmationEventArgs>();
        private Task stopTask;
        private ContentModel contentModel;
        private FolderModel selectedFolder;
        private RouteModelHeader selectedRoute;
        private ToolboxContent toolboxContent;
        private PathEditor pathEditor;
        private long routeContentVersion;
        private long pathListRefreshVersion;

        internal ToolboxSession(GameWindow game)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            routeLoads = new RouteLoadCoordinator(game.InvokeOnGameThreadAsync);
            PublishMenu();
        }

        /// <summary>Raised on the owner when the session creates or releases its editor.</summary>
        internal event EventHandler<PathEditorAvailabilityChangedEventArgs> OnPathEditorChanged;
        /// <summary>Requests confirmation without transferring ownership of the pending interaction.</summary>
        internal event EventHandler<UnsavedPathConfirmationEventArgs> UnsavedPathConfirmationRequested;
        /// <summary>Requests presentation of the save dialog on behalf of the current session.</summary>
        internal event EventHandler SaveTrainPathRequested;

        internal bool IsStopping { get; private set; }
        internal bool IsDisposed { get; private set; }
        internal FolderModel SelectedFolder => selectedFolder;
        internal RouteModelHeader SelectedRoute => selectedRoute;
        internal PathEditor ExistingPathEditor => pathEditor;
        internal bool HasUnsavedPathChanges => game.HostedServices?.TrainPathToolWindow?.HasUnsavedPathChanges == true || pathEditor?.HasUnsavedChanges == true;

        internal ITrainPathToolingContext TrainPathToolingContext => selectedRoute == null || toolboxContent == null ? null
            : new TrainPathToolingContext(selectedRoute,
                game.ToolboxUserSettings.MeasurementUnit == MeasurementUnit.Route ? selectedRoute.MetricUnits :
                game.ToolboxUserSettings.MeasurementUnit == MeasurementUnit.Metric ||
                (game.ToolboxUserSettings.MeasurementUnit == MeasurementUnit.System && System.Globalization.RegionInfo.CurrentRegion.IsMetric),
                ((ITrackNodeInfoContext)toolboxContent).TrackWorld);

        // Register before invoking the delegate: inline owner calls may synchronously stop the session.
        // Only outer operations are admitted here; their awaited owner dispatches must stay available while stopping.
        internal Task<T> RunAsync<T>(Func<Task<T>> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            TaskCompletionSource completion =  new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (jobs)
            {
                if (IsStopping)
                    return Task.FromException<T>(new InvalidOperationException("The Toolbox session is stopping."));
                jobs.Add(completion.Task);
            }
            Task<T> operation = CaptureInvocationAsync(action);
            CompleteJob(operation, completion);
            return operation;
        }

        private async Task<T> CaptureInvocationAsync<T>(Func<Task<T>> action)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (MapContentRollbackException error)
            {
                // No candidate exists for discard to retain. Publish failed-release evidence on
                // the game owner before this admitted operation can complete and leave the job set.
                await game.InvokeOnGameThreadAsync(() =>
                {
                    game.RetainMapRollbackFailure(error);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
                throw;
            }
        }

        internal Task RunAsync(Func<Task> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return RunAsync(async () =>
            {
                await action().ConfigureAwait(false);
                return true;
            });
        }

        private void CompleteJob(Task operation, TaskCompletionSource completion)
        {
            GameWindow.ObserveGameThreadOperation(operation);
            _ = operation.ContinueWith(completed =>
            {
                lock (jobs)
                {
                    // Cancellation ends a waiter, not the shared native/cache operation it borrowed.
                    if (completed.IsFaulted)
                        completion.SetException(completed.Exception.InnerExceptions);
                    else
                        completion.SetResult();
                    jobs.Remove(completion.Task);
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            // The operation above is the reported failure; also observe the drain's copy if no stop awaits it.
            _ = completion.Task.ContinueWith(completed => _ = completed.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        /// <summary>Closes admission on the owner, cancels waiters/interactions and awaits actual jobs and discards.</summary>
        internal Task StopAndDrainAsync()
        {
            TaskCompletionSource cancellationCompletion;
            lock (jobs)
            {
                if (stopTask != null)
                    return stopTask;
                IsStopping = true;
                cancellationCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                jobs.Add(cancellationCompletion.Task);
                stopTask = Task.WhenAll(jobs.ToArray());
            }
            Task cancellation = null;
            try
            {
                routeLoads.Stop();
                ++routeContentVersion;
                pendingFolder = null;
                pendingRoute = null;
                loadingRoute = null;
                PublishMenu();
                cancellation = lifetime.CancelAsync();
                foreach (UnsavedPathConfirmationEventArgs confirmation in confirmations.ToArray())
                    confirmation.Completion.TrySetResult(false);
                pathEditor?.Stop();
            }
            catch (Exception failure)
            {
                cancellation = Task.WhenAll(cancellation ?? lifetime.CancelAsync(), Task.FromException(failure));
                throw;
            }
            finally
            {
                // Publish the drain before cancellation can re-enter stop, but finish it only after callbacks and owner setup.
                CompleteJob(cancellation ?? lifetime.CancelAsync(), cancellationCompletion);
            }
            return stopTask;
        }

        /// <summary>Closes admission and releases resources on the game owner. The caller must drain operations before disposal.</summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;
            game.VerifySessionOwner();
            if (!StopAndDrainAsync().IsCompleted)
                throw new InvalidOperationException("Drain the Toolbox session before disposing its resources.");

            ClearRoute();
            lifetime.Dispose();
            IsDisposed = true;
        }

        private PathEditor GetPathEditor()
        {
            if (pathEditor == null && toolboxContent != null && !IsStopping)
            {
                pathEditor = game.CreatePathEditor(toolboxContent);
                pathEditor.OnPathChanged += PathEditor_OnEditorPathChanged;
                pathEditor.OnPathUpdated += PathEditor_OnEditorPathUpdated;
                OnPathEditorChanged?.Invoke(this, new PathEditorAvailabilityChangedEventArgs(pathEditor));
            }
            return pathEditor;
        }

        private void PathEditor_OnEditorPathChanged(object sender, PathEditorChangedEventArgs e)
        {
            ToolboxMenuEntry updated = PathEntry(e.Path?.PathModel ?? pathEditor?.TryCaptureCurrentPathModel());
            if (!Equals(menuPath?.Key, updated?.Key) || menuPath?.Name != updated?.Name || menuPath?.Description != updated?.Description)
            {
                menuPath = updated;
                PublishMenu();
            }
        }

        private void PathEditor_OnEditorPathUpdated(object sender, PathEditorChangedEventArgs e)
            => PathEditor_OnEditorPathChanged(sender, e);

        internal Task StartAsync() => RunAsync(async () =>
        {
            long selection = routeLoads.BeginSelection();
            if (!await LoadFoldersAsync().ConfigureAwait(false))
            {
                await game.InvokeOnGameThreadAsync(() =>
                {
                    if (!IsStopping)
                        GameWindow.ShowContentAnalysisRequired();
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
                return;
            }
            await game.InvokeOnGameThreadAsync(async () =>
            {
                if (!game.ToolboxSettings.RestoreLastView || !routeLoads.IsLatestSelection(selection))
                    return;
                try
                {
                    await PreSelectRouteAsync(game.ToolboxSettings.Folder, game.ToolboxSettings.RouteId, game.ToolboxSettings.PathId, selection).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Trace.TraceError($"Error restoring last view: {ex}");
                    await game.InvokeOnGameThreadAsync(() =>
                    {
                        if (routeLoads.IsLatestSelection(selection))
                        {
                            game.SetRouteLoadingStatus(null);
                            game.ToolboxSettings.RestoreLastView = false;
                        }
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                    throw;
                }
            }).ConfigureAwait(false);
        });

        private async Task<bool> LoadFoldersAsync()
        {
            // Folder discovery and settings persistence no longer share/reset a cancellation source.
            ContentModel discovered = await contentModel.Get(CancellationToken.None).WaitAsync(lifetime.Token).ConfigureAwait(false);
            if (discovered.RefreshRequired())
                return false;
            await game.InvokeOnGameThreadAsync(() =>
            {
                if (!IsStopping)
                {
                    contentModel = discovered;
                    PublishMenu();
                }
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            return true;
        }

        internal Task SelectFolderAsync(ToolboxMenuCommand command) => RunAsync(async () =>
        {
            FolderModel folder = ResolveFolder(command);
            if (folder == null)
                return;
            long selection = routeLoads.BeginSelection();
            pendingFolder = FolderKey(folder);
            pendingRoute = null;
            PublishMenu();
            bool confirmed = await ConfirmDiscardUnsavedPathsCoreAsync().ConfigureAwait(false);
            long discoveryContext = 0;
            bool accepted = await game.InvokeOnGameThreadAsync(() =>
            {
                if (!routeLoads.IsLatestSelection(selection))
                    return Task.FromResult(false);
                if (!confirmed)
                {
                    pendingFolder = null;
                    PublishMenu();
                    return Task.FromResult(false);
                }
                selectedFolder = folder;
                discoveryContext = ++folderContext;
                pendingFolder = null;
                menuRoutes = ImmutableArray<RouteModelHeader>.Empty;
                routeLoads.CancelCurrent();
                ClearRoute();
                discoveringRoutes = true;
                menuError = null;
                PublishMenu();
                return Task.FromResult(true);
            }).ConfigureAwait(false);
            if (!accepted)
                return;
            ImmutableArray<RouteModelHeader> routes = await FindRoutesAsync(folder, discoveryContext).ConfigureAwait(false);
            await game.InvokeOnGameThreadAsync(async () =>
            {
                if (IsStopping || folderContext != discoveryContext)
                    return;
                menuRoutes = routes;
                discoveringRoutes = false;
                if (routes.Length == 1 && routeLoads.IsLatestSelection(selection))
                    await LoadRouteAsync(routes[0], selection).ConfigureAwait(false);
                else
                    PublishMenu();
            }).ConfigureAwait(false);
        });

        private async Task<ImmutableArray<RouteModelHeader>> FindRoutesAsync(FolderModel folder, long discoveryContext)
        {
            // Only discovery owns this folder-context failure transition. Route failures are request-owned.
            try
            {
                return await folder.GetRoutes(CancellationToken.None).WaitAsync(lifetime.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await game.InvokeOnGameThreadAsync(() =>
                {
                    if (!IsStopping && folderContext == discoveryContext)
                    {
                        discoveringRoutes = false;
                        menuError = ex.Message;
                        PublishMenu();
                    }
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
                throw;
            }
        }

        internal Task ToggleRouteAsync(ToolboxMenuCommand command) => RunAsync(async () =>
        {
            RouteModelHeader route = ResolveRoute(command);
            if (route == null)
                return;
            long selection = routeLoads.BeginSelection();
            pendingFolder = null;
            pendingRoute = RouteKey(route);
            PublishMenu();
            bool confirmed = await ConfirmDiscardUnsavedPathsCoreAsync().ConfigureAwait(false);
            await game.InvokeOnGameThreadAsync(async () =>
            {
                if (!routeLoads.IsLatestSelection(selection))
                    return;
                if (!confirmed)
                {
                    pendingRoute = null;
                    PublishMenu();
                    return;
                }
                pendingRoute = null;
                if (ReferenceEquals(selectedRoute, route))
                {
                    routeLoads.CancelCurrent();
                    ClearRoute();
                    menuError = null;
                    PublishMenu();
                }
                else
                    await LoadRouteAsync(route, selection).ConfigureAwait(false);
            }).ConfigureAwait(false);
        });

        private RouteLoadCoordinator.Request BeginRouteLoad(RouteModelHeader route, long selection)
        {
            RouteLoadCoordinator.Request request = routeLoads.BeginRequest(selection);
            if (request == null)
                return null;
            ClearRoute();
            loadingRoute = RouteKey(route);
            discoveringRoutes = false;
            menuError = null;
            PublishMenu();
            game.SetRouteLoadingStatus(route);
            return request;
        }

        private async Task<bool> LoadRouteAsync(RouteModelHeader route, long selection)
        {
            using (RouteLoadCoordinator.Request request = BeginRouteLoad(route, selection))
                return request != null && await LoadRouteAsync(route, request).ConfigureAwait(false);
        }

        private async Task<bool> LoadRouteAsync(RouteModelHeader route, RouteLoadCoordinator.Request request)
        {
            CancellationToken token = request.Token;
            ToolboxContent candidate = null;
            try
            {
                RouteModel routeModel = await route.GetExtended(CancellationToken.None).AsTask().WaitAsync(token).ConfigureAwait(false);
                ImmutableArray<PathModelHeader> paths = await routeModel.GetRoutePaths(CancellationToken.None).WaitAsync(token).ConfigureAwait(false);
                bool useMetricUnits = game.ToolboxUserSettings.MeasurementUnit == MeasurementUnit.Metric ||
                    (game.ToolboxUserSettings.MeasurementUnit == MeasurementUnit.System && System.Globalization.RegionInfo.CurrentRegion.IsMetric);
                if (game.ToolboxUserSettings.MeasurementUnit == MeasurementUnit.Route)
                    useMetricUnits = routeModel.MetricUnits;
                return await routeLoads.LoadAsync(request, routeModel, useMetricUnits, async (prepared, completeAsync) =>
                {
                    candidate = game.CreateRouteContent(prepared);
                    await candidate.InitializeAsync(completeAsync).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    await PathEditor.ValidateRoutePaths(routeModel, prepared.TrackWorld, false, CancellationToken.None).ConfigureAwait(false);
                    paths = await routeModel.GetRoutePaths(CancellationToken.None).WaitAsync(token).ConfigureAwait(false);
                }, () =>
                {
                    candidate.InitializeItemVisiblity(game.ToolboxSettings.ViewSettings);
                    candidate.UpdateWidgetColorSettings(game.ToolboxSettings.ColorSettings, game.ToolboxSettings.FontOutline, game.ToolboxSettings.LimitTrackWidth);
                    toolboxContent = candidate;
                    selectedRoute = route;
                    game.AttachRouteContent(candidate);
                    menuPaths = paths;
                    loadingRoute = null;
                    PublishMenu();
                    game.HostedServices?.TrainPathToolWindow?.UpdatePaths(paths);
                }, () =>
                {
                    if (candidate != null && ReferenceEquals(toolboxContent, candidate))
                        ClearRoute();
                    else
                        game.DiscardRouteContent(candidate);
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                _ = await routeLoads.CommitAsync(request, () =>
                {
                    // This request may still be current while newer intent awaits confirmation.
                    // Never overwrite that intent, or restore the folder preceding acceptance.
                    ClearRoute();
                    menuError = ex.Message;
                    PublishMenu();
                }).ConfigureAwait(false);
                throw;
            }
            finally
            {
                await routeLoads.CommitAsync(request, () => game.SetRouteLoadingStatus(null)).ConfigureAwait(false);
            }
        }

        internal Task<bool> ConfirmDiscardUnsavedPathsAsync() => RunAsync(ConfirmDiscardUnsavedPathsCoreAsync);

        internal Task<bool> ValidateAllPathsAsync(Action<ImmutableArray<PathModelHeader>> refresh) => RunAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(refresh);
            RouteModelHeader route = selectedRoute;
            ToolboxContent content = toolboxContent;
            long version = routeContentVersion;
            ITrainPathToolingContext context = TrainPathToolingContext;
            if (context == null)
                return false;

            ImmutableArray<PathModelHeader> paths = await context.ValidateAllPaths().ConfigureAwait(false);
            await game.InvokeOnGameThreadAsync(() =>
            {
                if (!IsStopping && version == routeContentVersion && ReferenceEquals(route, selectedRoute) && ReferenceEquals(content, toolboxContent))
                    refresh(paths);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            return true;
        });

        private async Task<bool> ConfirmDiscardUnsavedPathsCoreAsync()
        {
            if (!HasUnsavedPathChanges)
                return true;
            UnsavedPathConfirmationEventArgs confirmation = new UnsavedPathConfirmationEventArgs();
            confirmations.Add(confirmation);
            try
            {
                if (UnsavedPathConfirmationRequested == null)
                    return false;
                UnsavedPathConfirmationRequested.Invoke(this, confirmation);
                return await confirmation.Completion.Task.ConfigureAwait(true);
            }
            finally
            {
                confirmations.Remove(confirmation);
            }
        }

        internal Task TogglePathAsync(ToolboxMenuCommand command) => RunAsync(() =>
        {
            PathModelHeader path = ResolvePath(command);
            return path == null ? Task.CompletedTask
                : LoadPathCoreAsync(string.Equals(pathEditor?.PathId, path.Id, StringComparison.OrdinalIgnoreCase) ? null : path, lifetime.Token);
        });

        internal Task<bool> LoadPathAsync(PathModelHeader path) => RunAsync(() => LoadPathCoreAsync(path, lifetime.Token));

        private Task<bool> LoadPathCoreAsync(PathModelHeader path, CancellationToken cancellationToken)
        {
            PathEditor editor = GetPathEditor();
            return editor?.InitializePathAsync(path, cancellationToken) ?? Task.FromResult(false);
        }

        internal Task EditPathAsync() => RunAsync(() =>
        {
            PathEditor editor = GetPathEditor();
            if (editor != null)
            {
                editor.InitializeNewPath();
                game.ActivatePathEditingInput();
            }
            return Task.CompletedTask;
        });

        internal Task RequestSavePathAsync() => RunAsync(() =>
        {
            SaveTrainPathRequested?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        });

        private async Task PreSelectRouteAsync(string folderId, string routeId, string pathId, long selection)
        {
            if (!routeLoads.IsLatestSelection(selection) || string.IsNullOrEmpty(folderId))
                return;
            selectedFolder = contentModel.ContentFolders.FirstOrDefault(folder => string.Equals(folder.Id, folderId, StringComparison.OrdinalIgnoreCase));
            if (selectedFolder == null)
                return;
            long discoveryContext = ++folderContext;
            menuRoutes = ImmutableArray<RouteModelHeader>.Empty;
            discoveringRoutes = true;
            menuError = null;
            PublishMenu();
            ImmutableArray<RouteModelHeader> routes = await FindRoutesAsync(selectedFolder, discoveryContext).ConfigureAwait(false);
            await game.InvokeOnGameThreadAsync(async () =>
            {
                if (!routeLoads.IsLatestSelection(selection))
                    return;
                menuRoutes = routes;
                discoveringRoutes = false;
                PublishMenu();
                RouteModelHeader route = routes.GetById(routeId);
                if (route == null || !game.ToolboxSettings.RestoreLastView)
                    return;
                using (RouteLoadCoordinator.Request request = BeginRouteLoad(route, selection))
                {
                    if (request == null || !await LoadRouteAsync(route, request).ConfigureAwait(false))
                        return;
                    try
                    {
                        PathModelHeader path = string.IsNullOrEmpty(pathId) ? null
                            : (await route.GetRoutePaths(CancellationToken.None).WaitAsync(request.Token).ConfigureAwait(false)).GetById(pathId);
                        bool restoreView = false;
                        PathEditor restoredEditor = null;
                        Task<bool> loadPath = Task.FromResult(true);
                        await routeLoads.CommitAsync(request, () =>
                        {
                            restoreView = pathEditor == null && routeLoads.IsLatestSelection(selection);
                            if (path != null && restoreView)
                            {
                                loadPath = LoadPathCoreAsync(path, request.Token);
                                restoredEditor = pathEditor;
                            }
                        }).ConfigureAwait(false);
                        restoreView = await loadPath.ConfigureAwait(false) && restoreView;
                        await routeLoads.CommitAsync(request, () =>
                        {
                            bool restoredSessionIsCurrent = ReferenceEquals(pathEditor, restoredEditor)
                                && (path == null || !pathEditor.HasUnsavedChanges && string.Equals(pathEditor.PathId, path.Id, StringComparison.OrdinalIgnoreCase));
                            if (restoreView && restoredSessionIsCurrent && routeLoads.IsLatestSelection(selection))
                                game.RestoreRouteViewport();
                        }).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (request.Token.IsCancellationRequested)
                    {
                        // A newer route owns startup followups now.
                    }
                }
            }).ConfigureAwait(false);
        }

        private void ClearRoute()
        {
            ++routeContentVersion;
            saveDialogEditor = null;
            saveDialogSource = null;
            if (pathEditor != null)
            {
                pathEditor.OnPathChanged -= PathEditor_OnEditorPathChanged;
                pathEditor.OnPathUpdated -= PathEditor_OnEditorPathUpdated;
                pathEditor.Dispose();
                pathEditor = null;
                OnPathEditorChanged?.Invoke(this, new PathEditorAvailabilityChangedEventArgs(null));
            }
            // The editor borrows the committed map; release it before disposing that map.
            game.AttachRouteContent(null);
            selectedRoute = null;
            menuPaths = ImmutableArray<PathModelHeader>.Empty;
            menuPath = null;
            loadingRoute = null;
            game.HostedServices?.TrainPathToolWindow?.InvalidatePaths();
            toolboxContent = null;
            game.SetRouteLoadingStatus(null);
        }
    }
}
