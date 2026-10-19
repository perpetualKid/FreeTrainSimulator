using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.PopupWindows;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Toolbox
{

    public partial class GameWindow : Game
    {
        private ContentModel contentModel;
        private FolderModel selectedFolder;
        private RouteModelHeader selectedRoute;
        private readonly SemaphoreSlim loadRouteSemaphore = new SemaphoreSlim(1);
        private CancellationTokenSource ctsProfileLoading;
        private readonly RouteLoadCoordinator routeLoads;
        private PathEditor pathEditor;
        private ToolboxContent toolboxContent;

        internal event EventHandler<PathEditorAvailabilityChangedEventArgs> OnPathEditorChanged;

        internal PathEditor PathEditor
        {
            get
            {
                if (null == pathEditor && toolboxContent != null)
                {
                    pathEditor = new PathEditor(toolboxContent, userCommandController, InvokeOnGameThread, commit => InvokeOnGameThreadAsync(() => Task.FromResult(commit())));
                    pathEditor.OnPathChanged += PathEditor_OnEditorPathChanged;
                    pathEditor.OnPathUpdated += PathEditor_OnEditorPathUpdated;
                    OnPathEditorChanged?.Invoke(this, new PathEditorAvailabilityChangedEventArgs(pathEditor));
                }
                return pathEditor;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                routeLoads?.Stop();
            _ = Interlocked.Increment(ref routeContentVersion);
            hostedPointerGesture.Reset();
            if (windowForm != null)
            {
                windowForm.MouseDown -= WindowForm_MouseDown;
                windowForm.MouseUp -= WindowForm_MouseUp;
                windowForm.MouseCaptureChanged -= WindowForm_MouseCaptureChanged;
            }
            loadRouteSemaphore?.Dispose();
            ctsProfileLoading?.Dispose();
            pathEditor?.Dispose();
            windowManager?.Dispose();
            spriteBatch?.Dispose();
            graphicsDeviceManager?.Dispose();
            windowForm?.Dispose();
            base.Dispose(disposing);
        }

        // Returns the existing path editor without creating it (the PathEditor getter lazily creates and
        // raises events). Used by the hosted train-path tool window so reading the snapshot never forces
        // editor creation. Null until a path edit session exists.
        internal PathEditor HostedPathEditor => pathEditor;

        // Builds a tooling context for the hosted train-path tool window from the currently selected route
        // and the active measurement-unit preference (mirrors the legacy popup registration). Null when no
        // route is loaded.
        internal ITrainPathToolingContext HostedTrainPathToolingContext =>
            selectedRoute == null ? null : new TrainPathToolingContext(selectedRoute,
                ToolboxUserSettings.MeasurementUnit == MeasurementUnit.Route ? selectedRoute.MetricUnits :
                ToolboxUserSettings.MeasurementUnit == MeasurementUnit.Metric || (ToolboxUserSettings.MeasurementUnit == MeasurementUnit.System && System.Globalization.RegionInfo.CurrentRegion.IsMetric));

        private void PathEditor_OnEditorPathChanged(object sender, PathEditorChangedEventArgs e)
        {
            hostedTrainPathToolWindow?.MarkDirty();
            menu.PreSelectPath(e.Path?.PathModel ?? pathEditor?.TryCaptureCurrentPathModel());
        }

        private void PathEditor_OnEditorPathUpdated(object sender, PathEditorChangedEventArgs e)
        {
            hostedTrainPathToolWindow?.MarkDirty();
        }

        internal async Task<bool> LoadFolders()
        {
            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(loadRouteSemaphore, true).ConfigureAwait(false);

            try
            {
                contentModel = await contentModel.Get(ctsProfileLoading.Token).ConfigureAwait(false);
                if (contentModel.RefreshRequired())
                {
                    return false;
                }
                await InvokeOnGameThreadAsync(() =>
                {
                    menu.PopulateContentFolders(contentModel.ContentFolders);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                await InvokeOnGameThreadAsync(() =>
                {
                    menu.PopulateContentFolders(ImmutableArray<FolderModel>.Empty);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
            return true;
        }

        internal long BeginRouteSelection() => routeLoads.BeginSelection();

        internal bool IsLatestRouteSelection(long selection) => routeLoads.IsLatestSelection(selection);

        internal async Task<ImmutableArray<RouteModelHeader>> FindRoutesAsync(FolderModel contentFolder, long selection)
        {
            ArgumentNullException.ThrowIfNull(contentFolder);
            ImmutableArray<RouteModelHeader> routes = await contentFolder.GetRoutes(CancellationToken.None).ConfigureAwait(false);
            await InvokeOnGameThreadAsync(() =>
            {
                if (routeLoads.IsLatestSelection(selection))
                {
                    selectedFolder = contentFolder;
                }
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            return routes;
        }

        // Called on the game thread before the first await, including startup and single-route discovery.
        private RouteLoadCoordinator.Request BeginRouteLoad(RouteModelHeader route, long selection)
        {
            RouteLoadCoordinator.Request request = routeLoads.BeginRequest(selection);
            if (request == null)
                return null;

            ClearRoute();
            (windowManager[ToolboxWindowType.StatusWindow] as StatusTextWindow).RouteName = route.Name;
            _ = windowManager[ToolboxWindowType.StatusWindow].Open();
            return request;
        }

        internal async Task<bool> LoadRouteAsync(RouteModelHeader route, long selection)
        {
            ArgumentNullException.ThrowIfNull(route);
            using (RouteLoadCoordinator.Request request = BeginRouteLoad(route, selection))
            {
                return request != null && await LoadRouteAsync(route, request).ConfigureAwait(false);
            }
        }

        private async Task<bool> LoadRouteAsync(RouteModelHeader route, RouteLoadCoordinator.Request request)
        {
            CancellationToken token = request.Token;
            ToolboxContent candidate = null;
            try
            {
                // Shared model caches must not retain a task canceled by an individual route request.
                RouteModel routeModel = await route.GetExtended(CancellationToken.None).AsTask().WaitAsync(token).ConfigureAwait(false);
                ImmutableArray<PathModelHeader> paths = await routeModel.GetRoutePaths(CancellationToken.None).WaitAsync(token).ConfigureAwait(false);
                bool useMetricUnits = ToolboxUserSettings.MeasurementUnit == MeasurementUnit.Metric || (ToolboxUserSettings.MeasurementUnit == MeasurementUnit.System && System.Globalization.RegionInfo.CurrentRegion.IsMetric);
                if (ToolboxUserSettings.MeasurementUnit == MeasurementUnit.Route)
                    useMetricUnits = routeModel.MetricUnits;

                return await routeLoads.LoadAsync(request, routeModel, useMetricUnits, async (prepared, completeAsync) =>
                {
                    candidate = new XnaMapContentFactory().CreateToolboxContent(
                        this,
                        Components.OfType<MouseInputGameComponent>().FirstOrDefault(),
                        new XnaMapInsetHost(Components.OfType<InsetComponent>().FirstOrDefault()),
                        new XnaMapTextureHelperHost(Components.OfType<TextureContentComponent>()));
                    await candidate.InitializeAsync(completeAsync).ConfigureAwait(false);
                    // Validation seeds shared model caches; drain it before publishing the route's paths.
                    token.ThrowIfCancellationRequested();
                    await PathEditor.ValidateRoutePaths(routeModel, prepared.TrackWorld, false, CancellationToken.None).ConfigureAwait(false);
                    paths = await routeModel.GetRoutePaths(CancellationToken.None).WaitAsync(token).ConfigureAwait(false);
                }, () =>
                {
                    candidate.InitializeItemVisiblity(ToolboxSettings.ViewSettings);
                    candidate.UpdateWidgetColorSettings(ToolboxSettings.ColorSettings, ToolboxSettings.FontOutline, ToolboxSettings.LimitTrackWidth);
                    toolboxContent = candidate;
                    selectedRoute = route;
                    ContentArea = ((IXnaMapShellHost)candidate.ShellHost).Component as ContentArea;
                    menu.PreSelectRoute(route.Name);
                    menu.PopulatePaths(paths);
                    hostedTrainPathToolWindow?.UpdatePaths(paths);
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
                await routeLoads.CommitAsync(request, () => windowManager[ToolboxWindowType.StatusWindow].Close()).ConfigureAwait(false);
            }
        }

        internal async Task<bool> LoadPathAsync(PathModelHeader path)
        {
            PathEditor editor = PathEditor;
            return editor != null && await editor.InitializePathAsync(path, ctsProfileLoading?.Token ?? CancellationToken.None).ConfigureAwait(true);
        }

        internal bool HasUnsavedPathChanges => hostedTrainPathToolWindow?.HasUnsavedPathChanges == true || pathEditor?.HasUnsavedChanges == true;

        internal event EventHandler<UnsavedPathConfirmationEventArgs> UnsavedPathConfirmationRequested;

        internal Task<bool> ConfirmDiscardUnsavedPathsAsync()
            => UnsavedPathConfirmationEventArgs.RequestAsync(
                HasUnsavedPathChanges, this, UnsavedPathConfirmationRequested);

        internal void EditPath()
        {
            PathEditor editor = PathEditor;
            if (editor == null)
                return;

            editor.InitializeNewPath();
            ActivatePathEditingInput();
        }

        internal void ActivatePathEditingInput()
        {
            SetHostedInputCaptured(false);
            FocusHostedWindow();
        }

        // Raised on the game thread in hosted mode when the user requests a path save, so the WPF shell can
        // show its own modal save dialog instead of the legacy MonoGame popup.
        internal event EventHandler SaveTrainPathRequested;

        internal void SavePath()
        {
            SaveTrainPathRequested?.Invoke(this, EventArgs.Empty);
        }

        private async Task PreSelectRouteAsync(string folderName, string routeId, string pathId, long selection)
        {
            if (!routeLoads.IsLatestSelection(selection) || string.IsNullOrEmpty(folderName))
                return;

            FolderModel folder = await InvokeOnGameThreadAsync(() => Task.FromResult(
                routeLoads.IsLatestSelection(selection) ? menu.SelectContentFolder(folderName) : null)).ConfigureAwait(false);
            if (folder == null)
                return;

            ImmutableArray<RouteModelHeader> routes = await FindRoutesAsync(folder, selection).ConfigureAwait(false);
            await InvokeOnGameThreadAsync(async () =>
            {
            if (!routeLoads.IsLatestSelection(selection))
                return;

            menu.PopulateRoutes(routes);
            RouteModelHeader route = routes.GetById(routeId);
            if (route == null || !ToolboxSettings.RestoreLastView)
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
                        _ = await routeLoads.CommitAsync(request, () =>
                        {
                            restoreView = pathEditor == null && routeLoads.IsLatestSelection(selection);
                            if (path != null && restoreView)
                            {
                                loadPath = LoadPathAsync(path);
                                restoredEditor = pathEditor;
                            }
                        }).ConfigureAwait(false);
                        restoreView = await loadPath.ConfigureAwait(false) && restoreView;
                        await routeLoads.CommitAsync(request, () =>
                        {
                            bool restoredSessionIsCurrent = ReferenceEquals(pathEditor, restoredEditor)
                                && (path == null || !pathEditor.HasUnsavedChanges
                                    && string.Equals(pathEditor.PathId, path.Id, StringComparison.OrdinalIgnoreCase));
                            if (restoreView && restoredSessionIsCurrent && routeLoads.IsLatestSelection(selection))
                                ContentArea?.PresetPosition(ToolboxSettings.ContentPosition, ToolboxSettings.ContentScale);
                        }).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (request.Token.IsCancellationRequested)
                    {
                        // A newer route owns startup followups now.
                    }
                }
            }).ConfigureAwait(false); 
        }

        internal void UnloadRoute()
        {
            routeLoads.Invalidate();
            ClearRoute();
        }

        internal void UnloadRoute(long selection)
        {
            if (!routeLoads.IsLatestSelection(selection))
                return;

            routeLoads.CancelCurrent();
            ClearRoute();
        }

        private void ClearRoute()
        {
            _ = Interlocked.Increment(ref routeContentVersion);
            ContentArea = null;
            selectedRoute = null;
            menu.ClearPathMenu();
            menu.PreSelectRoute(null);
            hostedTrainPathToolWindow?.InvalidatePaths();
            if (pathEditor != null)
            {
                pathEditor.OnPathChanged -= PathEditor_OnEditorPathChanged;
                pathEditor.OnPathUpdated -= PathEditor_OnEditorPathUpdated;
                pathEditor.Dispose();
                pathEditor = null;
                OnPathEditorChanged?.Invoke(this, new PathEditorAvailabilityChangedEventArgs(null));
            }
            toolboxContent = null;
            _ = windowManager[ToolboxWindowType.StatusWindow].Close();
        }

        internal Task UnloadPathAsync()
        {
            return pathEditor?.InitializePathAsync(null, CancellationToken.None) ?? Task.CompletedTask;
        }
    }
}
