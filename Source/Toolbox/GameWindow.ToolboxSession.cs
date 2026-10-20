using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.PopupWindows;

namespace FreeTrainSimulator.Toolbox
{
    public partial class GameWindow
    {
        /// <summary>Game-owned route, editor and admitted operation lifetime. Transport remains owned by the game.</summary>
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
            }

            internal bool IsStopping { get; private set; }
            internal bool IsDisposed { get; private set; }
            internal FolderModel SelectedFolder => selectedFolder;
            internal RouteModelHeader SelectedRoute => selectedRoute;
            internal PathEditor ExistingPathEditor => pathEditor;
            internal bool HasUnsavedPathChanges => game.hostedTrainPathToolWindow?.HasUnsavedPathChanges == true || pathEditor?.HasUnsavedChanges == true;

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
                ObserveGameThreadOperation(operation);
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
                lock (jobs)
                {
                    if (stopTask != null)
                        return stopTask;
                    IsStopping = true;
                    stopTask = Task.WhenAll(jobs.ToArray());
                }
                routeLoads.Stop();
                ++routeContentVersion;
                lifetime.CancelAsync().ConfigureAwait(false);
                foreach (UnsavedPathConfirmationEventArgs confirmation in confirmations.ToArray())
                    confirmation.Completion.TrySetResult(false);
                pathEditor?.Stop();
                return stopTask;
            }

            /// <summary>Closes admission and releases resources on the game owner. The caller must drain operations before disposal.</summary>
            public void Dispose()
            {
                if (IsDisposed)
                    return;
                if (!game.ownerControl.IsHandleCreated || game.ownerControl.IsDisposed || game.ownerControl.InvokeRequired)
                    throw new InvalidOperationException("The Toolbox session must be disposed on its game owner.");
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
                    pathEditor = new PathEditor(toolboxContent, game.userCommandController, action => game.InvokeOnGameThreadAsync(() =>
                    {
                        action();
                        return Task.CompletedTask;
                    }),
                        commit => game.InvokeOnGameThreadAsync(() => Task.FromResult(!IsStopping && commit())), RunAsync);
                    pathEditor.OnPathChanged += PathEditor_OnEditorPathChanged;
                    pathEditor.OnPathUpdated += PathEditor_OnEditorPathUpdated;
                    game.OnPathEditorChanged?.Invoke(game, new PathEditorAvailabilityChangedEventArgs(pathEditor));
                }
                return pathEditor;
            }

            private void PathEditor_OnEditorPathChanged(object sender, PathEditorChangedEventArgs e)
            {
                game.hostedTrainPathToolWindow?.MarkDirty();
                game.menu.PreSelectPath(e.Path?.PathModel ?? pathEditor?.TryCaptureCurrentPathModel());
            }

            private void PathEditor_OnEditorPathUpdated(object sender, PathEditorChangedEventArgs e)
                => game.hostedTrainPathToolWindow?.MarkDirty();

            internal Task StartAsync() => RunAsync(async () =>
            {
                long selection = routeLoads.BeginSelection();
                if (!await LoadFoldersAsync().ConfigureAwait(false))
                {
                    await game.InvokeOnGameThreadAsync(() =>
                    {
                        if (!IsStopping)
                            MessageBox.Show($"In an effort to optimize content, {RuntimeInfo.ProductName} will need to analyze existing content files and folders." + Environment.NewLine + Environment.NewLine +
                                $"Please close {RuntimeInfo.ApplicationName}, and use the Menu-application to review current content folder settings for further analysis.", "Please read!");
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
                                game.windowManager[ToolboxWindowType.StatusWindow].Close();
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
                        game.menu.PopulateContentFolders(contentModel.ContentFolders);
                    }
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
                return true;
            }

            internal Task SelectFolderAsync(FolderModel folder) => RunAsync(async () =>
            {
                ArgumentNullException.ThrowIfNull(folder);
                long selection = routeLoads.BeginSelection();
                bool confirmed = await ConfirmDiscardUnsavedPathsCoreAsync().ConfigureAwait(false);
                bool accepted = await game.InvokeOnGameThreadAsync(() =>
                {
                    if (!routeLoads.IsLatestSelection(selection))
                        return Task.FromResult(false);
                    if (!confirmed)
                    {
                        game.hostedMenu.ProjectSelectedFolder(selectedFolder);
                        return Task.FromResult(false);
                    }
                    selectedFolder = folder;
                    game.hostedMenu.ProjectSelectedFolder(folder);
                    game.menu.PopulateRoutes(ImmutableArray<RouteModelHeader>.Empty);
                    routeLoads.CancelCurrent();
                    ClearRoute();
                    return Task.FromResult(true);
                }).ConfigureAwait(false);
                if (!accepted)
                    return;
                ImmutableArray<RouteModelHeader> routes = await FindRoutesAsync(folder).ConfigureAwait(false);
                await game.InvokeOnGameThreadAsync(async () =>
                {
                    if (!routeLoads.IsLatestSelection(selection))
                        return;
                    game.menu.PopulateRoutes(routes);
                    if (routes.Length == 1)
                        await LoadRouteAsync(routes[0], selection).ConfigureAwait(false);
                }).ConfigureAwait(false);
            });

            private Task<ImmutableArray<RouteModelHeader>> FindRoutesAsync(FolderModel folder)
                => folder.GetRoutes(CancellationToken.None).WaitAsync(lifetime.Token);

            internal Task ToggleRouteAsync(RouteModelHeader route) => RunAsync(async () =>
            {
                ArgumentNullException.ThrowIfNull(route);
                long selection = routeLoads.BeginSelection();
                bool confirmed = await ConfirmDiscardUnsavedPathsCoreAsync().ConfigureAwait(false);
                await game.InvokeOnGameThreadAsync(async () =>
                {
                    if (!routeLoads.IsLatestSelection(selection))
                        return;
                    if (!confirmed)
                    {
                        game.menu.PreSelectRoute(selectedRoute?.Name);
                        return;
                    }
                    if (ReferenceEquals(selectedRoute, route))
                    {
                        routeLoads.CancelCurrent();
                        ClearRoute();
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
                (game.windowManager[ToolboxWindowType.StatusWindow] as StatusTextWindow).RouteName = route.Name;
                _ = game.windowManager[ToolboxWindowType.StatusWindow].Open();
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
                        candidate = new XnaMapContentFactory().CreateToolboxContent(game,
                            game.Components.OfType<MouseInputGameComponent>().FirstOrDefault(),
                            new XnaMapInsetHost(game.Components.OfType<InsetComponent>().FirstOrDefault()),
                            new XnaMapTextureHelperHost(game.Components.OfType<TextureContentComponent>()));
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
                        game.ContentArea = ((IXnaMapShellHost)candidate.ShellHost).Component as ContentArea;
                        game.menu.PreSelectRoute(route.Name);
                        game.menu.PopulatePaths(paths);
                        game.hostedTrainPathToolWindow?.UpdatePaths(paths);
                    }, () =>
                    {
                        if (candidate != null && ReferenceEquals(toolboxContent, candidate))
                            ClearRoute();
                        else
                            ((candidate?.ShellHost as IXnaMapShellHost)?.Component as ContentArea)?.Dispose();
                    }).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return false;
                }
                finally
                {
                    await routeLoads.CommitAsync(request, () => game.windowManager[ToolboxWindowType.StatusWindow].Close()).ConfigureAwait(false);
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
                    if (game.UnsavedPathConfirmationRequested == null)
                        return false;
                    game.UnsavedPathConfirmationRequested.Invoke(game, confirmation);
                    return await confirmation.Completion.Task.ConfigureAwait(true);
                }
                finally
                {
                    confirmations.Remove(confirmation);
                }
            }

            internal Task TogglePathAsync(PathModelHeader path) => RunAsync(() =>
            {
                ArgumentNullException.ThrowIfNull(path);
                return LoadPathCoreAsync(string.Equals(pathEditor?.PathId, path.Id, StringComparison.OrdinalIgnoreCase) ? null : path, lifetime.Token);
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
                game.SaveTrainPathRequested?.Invoke(game, EventArgs.Empty);
                return Task.CompletedTask;
            });

            private async Task PreSelectRouteAsync(string folderName, string routeId, string pathId, long selection)
            {
                if (!routeLoads.IsLatestSelection(selection) || string.IsNullOrEmpty(folderName))
                    return;
                selectedFolder = contentModel.ContentFolders.FirstOrDefault(folder => string.Equals(folder.Name, folderName, StringComparison.OrdinalIgnoreCase));
                if (selectedFolder == null)
                    return;
                game.hostedMenu.ProjectSelectedFolder(selectedFolder);
                ImmutableArray<RouteModelHeader> routes = await FindRoutesAsync(selectedFolder).ConfigureAwait(false);
                await game.InvokeOnGameThreadAsync(async () =>
                {
                    if (!routeLoads.IsLatestSelection(selection))
                        return;
                    game.menu.PopulateRoutes(routes);
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
                                    game.ContentArea?.PresetPosition(game.ToolboxSettings.ContentPosition, game.ToolboxSettings.ContentScale);
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
                    game.OnPathEditorChanged?.Invoke(game, new PathEditorAvailabilityChangedEventArgs(null));
                }
                // The editor borrows the committed map; release it before disposing that map.
                if (game.ContentArea != null)
                    game.ContentArea = null;
                selectedRoute = null;
                game.menu?.ClearPathMenu();
                game.menu?.PreSelectRoute(null);
                game.hostedTrainPathToolWindow?.InvalidatePaths();
                toolboxContent = null;
                if (game.windowManager != null)
                    _ = game.windowManager[ToolboxWindowType.StatusWindow].Close();
            }
        }
    }
}
