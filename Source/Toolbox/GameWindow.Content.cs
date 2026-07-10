using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Toolbox.PopupWindows;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Toolbox
{
    public class ContentAreaChangedEventArgs : EventArgs
    {
        public ContentArea ContentArea { get; }

        public IMapRenderer Renderer => ContentArea;

        public IMapViewport Viewport => ContentArea;

        public IMapHostControl HostControl => ContentArea;

        public IMapLocationContext LocationContext => ContentArea as IMapLocationContext;

        public IMapDisplaySettingsContext DisplaySettingsContext => ContentArea as IMapDisplaySettingsContext;

        public ITrackNodeInfoContext TrackNodeInfoContext => ContentArea?.Content as ITrackNodeInfoContext;

        public ITrackItemInfoContext TrackItemInfoContext => ContentArea?.Content as ITrackItemInfoContext;

        public INameValueInformationProvider RouteInformationProvider => ContentArea?.Content;

        public ContentAreaChangedEventArgs(ContentArea contentArea)
        {
            ContentArea = contentArea;
        }
    }

    public partial class GameWindow : Game
    {
        private ContentModel contentModel;
        private FolderModel selectedFolder;
        private RouteModelHeader selectedRoute;
        private ImmutableArray<RouteModelHeader> routeModels;
        private readonly SemaphoreSlim loadRouteSemaphore = new SemaphoreSlim(1);
        private CancellationTokenSource ctsProfileLoading;
        private CancellationTokenSource ctsRouteLoading;
        private PathEditor pathEditor;
        private ToolboxContent toolboxContent;

        internal event EventHandler<PathEditorAvailabilityChangedEventArgs> OnPathEditorChanged;

        internal PathEditor PathEditor
        {
            get
            {
                if (null == pathEditor && toolboxContent != null)
                {
                    pathEditor = new PathEditor(toolboxContent, userCommandController);
                    pathEditor.OnPathChanged += PathEditor_OnEditorPathChanged;
                    pathEditor.OnPathUpdated += PathEditor_OnEditorPathUpdated;
                    OnPathEditorChanged?.Invoke(this, new PathEditorAvailabilityChangedEventArgs(pathEditor));
                }
                return pathEditor;
            }
        }

        protected override void Dispose(bool disposing)
        {
            loadRouteSemaphore?.Dispose();
            ctsProfileLoading?.Dispose();
            ctsRouteLoading?.Dispose();
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
            menu.PreSelectPath(e.Path?.PathModel);
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
                menu.PopulateContentFolders(contentModel.ContentFolders);
            }
            catch (TaskCanceledException)
            {
                menu.PopulateContentFolders(ImmutableArray<FolderModel>.Empty);
            }
            return true;
        }

        internal async Task<ImmutableArray<RouteModelHeader>> FindRoutes(FolderModel contentFolder)
        {
            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(loadRouteSemaphore, true).ConfigureAwait(false);
            try
            {
                await loadRouteSemaphore.WaitAsync().ConfigureAwait(false);
                if (contentFolder != selectedFolder)
                {
                    try
                    {
                        routeModels = await contentFolder.GetRoutes(ctsProfileLoading.Token).ConfigureAwait(false);
                        // Only commit the folder as selected once its routes have actually loaded. Committing
                        // after a cancelled fetch would leave selectedFolder pointing at a folder whose routes
                        // were never populated, so re-selecting it later would be skipped by this guard and the
                        // folder change would silently do nothing.
                        selectedFolder = contentFolder;
                    }
                    catch (TaskCanceledException) { }
                }
            }
            finally
            {
                _ = loadRouteSemaphore.Release();
            }
            return routeModels;
        }

        internal async Task LoadRoute(RouteModelHeader route)
        {
            (windowManager[ToolboxWindowType.StatusWindow] as StatusTextWindow).RouteName = route.Name;
            _ = windowManager[ToolboxWindowType.StatusWindow].Open();
            UnloadRoute();

            ctsRouteLoading = await ctsRouteLoading.ResetCancellationTokenSource(loadRouteSemaphore, true).ConfigureAwait(true);

            RouteModel routeModel = await route.GetExtended(ctsProfileLoading.Token).ConfigureAwait(true);
            Task<ImmutableArray<PathModelHeader>> pathTask = routeModel.GetRoutePaths(ctsProfileLoading.Token);

            bool useMetricUnits = ToolboxUserSettings.MeasurementUnit == MeasurementUnit.Metric || (ToolboxUserSettings.MeasurementUnit == MeasurementUnit.System && System.Globalization.RegionInfo.CurrentRegion.IsMetric);
            if (ToolboxUserSettings.MeasurementUnit == MeasurementUnit.Route)
                useMetricUnits = routeModel.MetricUnits;

            await RuntimeDataResolver.Initialize(routeModel, useMetricUnits).ConfigureAwait(true);
            if (ctsProfileLoading.Token.IsCancellationRequested)
                return;

            XnaMapContentFactory contentFactory = new XnaMapContentFactory();
            toolboxContent = contentFactory.CreateToolboxContent(
                this,
                Components.OfType<MouseInputGameComponent>().FirstOrDefault(),
                new XnaMapInsetHost(Components.OfType<InsetComponent>().FirstOrDefault()),
                new XnaMapTextureHelperHost(Components.OfType<TextureContentComponent>()));

            await toolboxContent.Initialize().ConfigureAwait(true);
            toolboxContent.InitializeItemVisiblity(ToolboxSettings.ViewSettings);
            toolboxContent.UpdateWidgetColorSettings(ToolboxSettings.ColorSettings, ToolboxSettings.FontOutline, ToolboxSettings.LimitTrackWidth);
            ContentArea = ((IXnaMapShellHost)toolboxContent.ShellHost).Component as ContentArea;
            selectedRoute = route;
            ImmutableArray<PathModelHeader> paths = await pathTask.ConfigureAwait(true);

            // Lazily validate paths that have never been validated (Valid is null), persisting the resulting
            // flag so subsequent loads only read the lightweight header. Legacy/imported paths get flagged here
            // without their node data being altered. Re-fetch afterwards so the menu/tool window show the marker.
            await PathEditor.ValidateRoutePaths(routeModel, RuntimeDataResolver.Instance.TrackWorld, false, ctsProfileLoading.Token).ConfigureAwait(true);
            if (ctsProfileLoading.Token.IsCancellationRequested)
                return;
            paths = await routeModel.GetRoutePaths(ctsProfileLoading.Token).ConfigureAwait(true);

            menu.PopulatePaths(paths);
            hostedTrainPathToolWindow?.UpdatePaths(paths);
            _ = windowManager[ToolboxWindowType.StatusWindow].Close();
        }

        internal bool LoadPath(PathModelHeader path)
        {
            PathEditor editor = PathEditor;
            return editor?.InitializePath(path) == true;
        }

        internal void EditPath()
        {
            PathEditor editor = PathEditor;
            if (editor == null)
                return;

            editor.InitializeNewPath();
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

        internal async Task PreSelectRoute(string folderName, string routeId, string pathId)
        {
            if (!string.IsNullOrEmpty(folderName))
            {
                FolderModel folder = menu.SelectContentFolder(folderName);

                if (!string.IsNullOrEmpty(routeId) && ToolboxSettings.RestoreLastView)
                {
                    RouteModelHeader route = (routeModels.IsDefaultOrEmpty ? routeModels = await FindRoutes(folder).ConfigureAwait(true) : routeModels).GetById(routeId);
                    if (null != route)
                    {
                        await LoadRoute(route).ConfigureAwait(true);
                        menu.PreSelectRoute(route.Name);
                        if (!string.IsNullOrEmpty(pathId))
                        {
                            // only restore first path for now
                            PathModelHeader path = (await route.GetRoutePaths(CancellationToken.None).ConfigureAwait(true)).GetById(pathId);
                            if (null != path)
                            {
                                if (LoadPath(path))
                                    menu.PreSelectPath(path);
                            }
                        }
                    }
                }
            }
        }

        internal void UnloadRoute()
        {
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
        }

        internal void UnloadPath()
        {
            _ = pathEditor?.InitializePath(null);
        }
    }
}
