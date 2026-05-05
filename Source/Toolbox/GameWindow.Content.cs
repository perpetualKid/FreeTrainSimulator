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
                    OnPathEditorChanged?.Invoke(this, new PathEditorAvailabilityChangedEventArgs(pathEditor));
                }
                return pathEditor;
            }
        }

        private void PathEditor_OnEditorPathChanged(object sender, PathEditorChangedEventArgs e)
        {
            mainmenu.PreSelectPath(e.Path?.PathModel);
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
                mainmenu.PopulateContentFolders(contentModel.ContentFolders);
            }
            catch (TaskCanceledException)
            {
                mainmenu.PopulateContentFolders(ImmutableArray<FolderModel>.Empty);
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
                    }
                    catch (TaskCanceledException) { }
                    selectedFolder = contentFolder;
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

            IMapContentFactory contentFactory = new XnaMapContentFactory();
            toolboxContent = contentFactory.CreateToolboxContent(
                this,
                Components.OfType<MouseInputGameComponent>().FirstOrDefault(),
                new XnaMapInsetHost(Components.OfType<InsetComponent>().FirstOrDefault()),
                new XnaMapTextureHelperHost(Components.OfType<TextureContentComponent>()));

            await toolboxContent.Initialize().ConfigureAwait(true);
            toolboxContent.InitializeItemVisiblity(ToolboxSettings.ViewSettings);
            toolboxContent.UpdateWidgetColorSettings(ToolboxSettings.ColorSettings, ToolboxSettings.FontOutline, ToolboxSettings.LimitTrackWidth);
            ContentArea = ((IXnaMapShellHost)toolboxContent.ShellHost).Component as ContentArea;
            mainmenu.PopulatePaths(await pathTask.ConfigureAwait(true));
            _ = windowManager[ToolboxWindowType.StatusWindow].Close();
            selectedRoute = route;
        }

        internal bool LoadPath(PathModelHeader path)
        {
            return PathEditor.InitializePath(path);
        }

        internal void EditPath()
        {
            PathEditor.InitializeNewPath();            
        }

        internal void SavePath()
        {
            windowForm.ActiveControl = null;
            _ = windowManager[ToolboxWindowType.TrainPathSaveWindow].Open();
        }

        internal async Task PreSelectRoute(string folderName, string routeId, string pathId)
        {
            if (!string.IsNullOrEmpty(folderName))
            {
                FolderModel folder = mainmenu.SelectContentFolder(folderName);

                if (!string.IsNullOrEmpty(routeId) && ToolboxSettings.RestoreLastView)
                {
                    RouteModelHeader route = (routeModels.IsDefaultOrEmpty ? routeModels = await FindRoutes(folder).ConfigureAwait(true) : routeModels).GetById(routeId);
                    if (null != route)
                    {
                        await LoadRoute(route).ConfigureAwait(true);
                        mainmenu.PreSelectRoute(route.Name);
                        if (!string.IsNullOrEmpty(pathId))
                        {
                            // only restore first path for now
                            PathModelHeader path = (await route.GetRoutePaths(CancellationToken.None).ConfigureAwait(true)).GetById(pathId);
                            if (null != path)
                            {
                                if (LoadPath(path))
                                    mainmenu.PreSelectPath(path);
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
            mainmenu.ClearPathMenu();
            if (pathEditor != null)
            {
                pathEditor.Dispose();
                pathEditor = null;
                OnPathEditorChanged?.Invoke(this, new PathEditorAvailabilityChangedEventArgs(null));
            }
        }

        internal void UnloadPath()
        {
            _ = PathEditor.InitializePath(null);
        }
    }
}
