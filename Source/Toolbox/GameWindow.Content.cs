using System;
using System.Collections.Frozen;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Independent.Environment;
using FreeTrainSimulator.Models.Loader.Shim;
using FreeTrainSimulator.Models.Simplified;
using FreeTrainSimulator.Toolbox.PopupWindows;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Toolbox
{
    public class ContentAreaChangedEventArgs : EventArgs
    {
        public ContentArea ContentArea { get; }

        public ContentAreaChangedEventArgs(ContentArea contentArea)
        {
            ContentArea = contentArea;
        }
    }

    public partial class GameWindow : Game
    {
        private Folder selectedFolder;
        private RouteModel selectedRoute;
        private FrozenSet<RouteModel> routeModels;
        private readonly SemaphoreSlim loadRoutesSemaphore = new SemaphoreSlim(1);
        private CancellationTokenSource ctsRouteLoading;
        private PathEditor pathEditor;

        internal PathEditor PathEditor
        {
            get
            {
                if (null == pathEditor)
                {
                    pathEditor = new PathEditor(contentArea);
                    pathEditor.OnPathChanged += PathEditor_OnEditorPathChanged;
                }
                return pathEditor;
            }
        }

        private void PathEditor_OnEditorPathChanged(object sender, PathEditorChangedEventArgs e)
        {
            mainmenu.PreSelectPath(e.Path?.FilePath);
        }

        internal async Task LoadFolders()
        {
            try
            {
                IOrderedEnumerable<Folder> folders = (await Folder.GetFolders(Settings.UserSettings.FolderSettings.Folders).ConfigureAwait(true)).OrderBy(f => f.Name);
                mainmenu.PopulateContentFolders(folders);
            }
            catch (TaskCanceledException)
            {
            }
        }

        internal async Task<FrozenSet<RouteModel>> FindRoutes(Folder routeFolder)
        {
            await loadRoutesSemaphore.WaitAsync().ConfigureAwait(false);
            if (routeFolder != selectedFolder)
            {
                routeModels = await RouteLoader.GetRoutes(routeFolder.ContentFolder, ctsRouteLoading?.Token ?? CancellationToken.None).ConfigureAwait(false);
                selectedFolder = routeFolder;
            }
            loadRoutesSemaphore.Release();
            return routeModels;
        }

        internal async Task LoadRoute(RouteModel route)
        {
            (windowManager[ToolboxWindowType.StatusWindow] as StatusTextWindow).RouteName = route.Name;
            windowManager[ToolboxWindowType.StatusWindow].Open();
            UnloadRoute();

            ctsRouteLoading = await ResetCancellationTokenSource(loadRoutesSemaphore, ctsRouteLoading);

            CancellationToken token = ctsRouteLoading.Token;

            bool? useMetricUnits = Settings.UserSettings.MeasurementUnit == MeasurementUnit.Metric || (Settings.UserSettings.MeasurementUnit == MeasurementUnit.System && System.Globalization.RegionInfo.CurrentRegion.IsMetric);
            if (Settings.UserSettings.MeasurementUnit == MeasurementUnit.Route)
                useMetricUnits = null;

            await TrackData.LoadTrackData(this, selectedFolder.ContentFolder.Route(route.RouteId), useMetricUnits, token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
                return;

            ToolboxContent content = new ToolboxContent(this);
            await content.Initialize().ConfigureAwait(false);
            content.InitializeItemVisiblity(Settings.ViewSettings);
            content.UpdateWidgetColorSettings(Settings.ColorSettings);
            content.ContentArea.FontOutlineOptions = Settings.OutlineFont ? OutlineRenderOptions.Default : null;
            ContentArea = content.ContentArea;
            mainmenu.PopulatePaths((Orts.Formats.Msts.RuntimeData.GameInstance(this) as TrackData).TrainPaths);
            windowManager[ToolboxWindowType.StatusWindow].Close();
            selectedRoute = route;
        }

        internal bool LoadPath(Path path)
        {
            return PathEditor.InitializePath(path);
        }

        internal void EditPath()
        {
            PathEditor.InitializeNewPath();
        }

        internal async Task PreSelectRoute(string[] routeSelection, string[] pathSelection)
        {
            if (routeSelection?.Length > 0)
            {
                Folder folder = mainmenu.SelectContentFolder(routeSelection[0]);

                if (routeSelection.Length > 1 && Settings.RestoreLastView)
                {
                    RouteModel route = (routeModels ??= await FindRoutes(folder).ConfigureAwait(false))?.Where(r => r.Name.Equals(routeSelection[1], StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (null != route)
                    {
                        await LoadRoute(route).ConfigureAwait(false);
                        mainmenu.PreSelectRoute(route.Name);
                        if (pathSelection.Length > 0)
                        {
                            // only restore first path for now
                            Path path = (Orts.Formats.Msts.RuntimeData.GameInstance(this) as TrackData).TrainPaths?.Where(p => p.FilePath.Equals(pathSelection[0], StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                            if (null != path)
                            {
                                if (LoadPath(path))
                                    mainmenu.PreSelectPath(path.FilePath);
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
            pathEditor?.Dispose();
            pathEditor = null;
        }

        internal void UnloadPath()
        {
            PathEditor.InitializePath(null);
        }

        private static async ValueTask<CancellationTokenSource> ResetCancellationTokenSource(SemaphoreSlim semaphore, CancellationTokenSource cts)
        {
            try
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
                if (cts != null && !cts.IsCancellationRequested)
                    await cts.CancelAsync().ConfigureAwait(false);
                cts?.Dispose();
                // Create a new cancellation token source so that can cancel all the tokens again 
                return new CancellationTokenSource();
            }
            finally
            {
                _ = semaphore.Release();
            }
        }
    }
}
