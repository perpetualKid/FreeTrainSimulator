using System;
using System.Collections.Frozen;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Loader.Shim;
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
        private ProfileModel contentProfile;
        private FolderModel selectedFolder;
        private RouteModelCore selectedRoute;
        private FrozenSet<RouteModelCore> routeModels;
        private readonly SemaphoreSlim loadRouteSemaphore = new SemaphoreSlim(1);
        private CancellationTokenSource ctsProfileLoading;
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
            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(loadRouteSemaphore, true).ConfigureAwait(false);

            try
            {
                contentProfile = await ProfileModel.None.Get(ctsProfileLoading.Token).ConfigureAwait(false) ??
                    await contentProfile.Empty(ctsProfileLoading.Token).ConfigureAwait(false);

                FrozenSet<FolderModel> contentFolders = await contentProfile.GetFolders(ctsProfileLoading.Token).ConfigureAwait(false);
                mainmenu.PopulateContentFolders(contentProfile.ContentFolders);
            }
            catch (TaskCanceledException)
            {
                mainmenu.PopulateContentFolders(FrozenSet<FolderModel>.Empty);
            }
        }

        internal async Task<FrozenSet<RouteModelCore>> FindRoutes(FolderModel contentFolder)
        {
            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(loadRouteSemaphore, true).ConfigureAwait(false);
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
            loadRouteSemaphore.Release();
            return routeModels;
        }

        internal async Task LoadRoute(RouteModelCore route)
        {
            (windowManager[ToolboxWindowType.StatusWindow] as StatusTextWindow).RouteName = route.Name;
            windowManager[ToolboxWindowType.StatusWindow].Open();
            UnloadRoute();

            ctsRouteLoading = await ctsRouteLoading.ResetCancellationTokenSource(loadRouteSemaphore, true).ConfigureAwait(false);

            bool? useMetricUnits = Settings.UserSettings.MeasurementUnit == MeasurementUnit.Metric || (Settings.UserSettings.MeasurementUnit == MeasurementUnit.System && System.Globalization.RegionInfo.CurrentRegion.IsMetric);
            if (Settings.UserSettings.MeasurementUnit == MeasurementUnit.Route)
                useMetricUnits = null;

            RouteModel routeModel = await route.Extend(ctsProfileLoading.Token).ConfigureAwait(false);

            await TrackData.LoadTrackData(this, routeModel, useMetricUnits, ctsProfileLoading.Token).ConfigureAwait(false);
            if (ctsProfileLoading.Token.IsCancellationRequested)
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

        internal bool LoadPath(PathModelCore path)
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
                FolderModel folder = mainmenu.SelectContentFolder(routeSelection[0]);

                if (routeSelection.Length > 1 && Settings.RestoreLastView)
                {
                    RouteModelCore route = (routeModels ??= await FindRoutes(folder).ConfigureAwait(false))?.Where(r => r.Name.Equals(routeSelection[1], StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (null != route)
                    {
                        await LoadRoute(route).ConfigureAwait(false);
                        mainmenu.PreSelectRoute(route.Name);
                        if (pathSelection.Length > 0)
                        {
                            // only restore first path for now
                            PathModelCore path = (Orts.Formats.Msts.RuntimeData.GameInstance(this) as TrackData).TrainPaths?.Where(p => p.SourceFile().Equals(pathSelection[0], StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                            if (null != path)
                            {
                                if (LoadPath(path))
                                    mainmenu.PreSelectPath(path.SourceFile());
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
    }
}
