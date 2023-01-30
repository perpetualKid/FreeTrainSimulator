﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Models.Simplified;
using Orts.Graphics.MapView;
using Orts.Toolbox.PopupWindows;
using Orts.Formats.Msts.Files;
using Orts.Graphics.Xna;

namespace Orts.Toolbox
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
        private Route selectedRoute;
        private Path selectedPath; // going forward, there may be multiple paths selected at once
        private IEnumerable<Route> routes;
        private readonly SemaphoreSlim loadRoutesSemaphore = new SemaphoreSlim(1);
        private CancellationTokenSource ctsRouteLoading;

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

        internal async Task<IEnumerable<Route>> FindRoutes(Folder routeFolder)
        {
            await loadRoutesSemaphore.WaitAsync().ConfigureAwait(false);
            if (routeFolder != selectedFolder)
            {
                routes = null;
                routes = (await Task.Run(() => Route.GetRoutes(routeFolder, CancellationToken.None)).ConfigureAwait(false)).OrderBy(r => r.ToString());
                selectedFolder = routeFolder;
            }
            loadRoutesSemaphore.Release();
            return routes;
        }

        internal async Task LoadRoute(Route route)
        {
            (windowManager[ToolboxWindowType.StatusWindow] as StatusTextWindow).RouteName = route.Name;
            windowManager[ToolboxWindowType.StatusWindow].Open();
            UnloadRoute();

            lock (routes)
            {
                if (ctsRouteLoading != null && !ctsRouteLoading.IsCancellationRequested)
                    ctsRouteLoading.Cancel();
                ctsRouteLoading = ResetCancellationTokenSource(ctsRouteLoading);
            }

            CancellationToken token = ctsRouteLoading.Token;

            bool? useMetricUnits = (Settings.UserSettings.MeasurementUnit == MeasurementUnit.Metric || Settings.UserSettings.MeasurementUnit == MeasurementUnit.System && System.Globalization.RegionInfo.CurrentRegion.IsMetric);
            if (Settings.UserSettings.MeasurementUnit == MeasurementUnit.Route)
                useMetricUnits = null;
            await TrackData.LoadTrackData(this, route, useMetricUnits, token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
                return;

            ToolboxContent content = new ToolboxContent(this);
            await content.Initialize().ConfigureAwait(false);
            content.InitializeItemVisiblity(Settings.ViewSettings);
            content.UpdateWidgetColorSettings(Settings.ColorSettings);
            content.ContentArea.FontOutlineOptions = Settings.OutlineFont ? OutlineRenderOptions.Default : null;
            ContentArea = content.ContentArea;
            mainmenu.PopulatePaths((TrackData.GameInstance(this) as TrackData).TrainPaths);
            windowManager[ToolboxWindowType.StatusWindow].Close();
            selectedRoute = route;
        }

        internal bool LoadPath(Path path)
        {
            try
            {
                PathFile patFile = new PathFile(path.FilePath);
                selectedPath = path;
                ((ToolboxContent)contentArea?.Content).InitializePath(patFile, path.FilePath);
                return true;
            }
            catch (Exception ex) when (ex is Exception)
            {
                return false;
            }
        }

        internal async Task PreSelectRoute(string[] routeSelection, string[] pathSelection)
        {
            if (routeSelection?.Length > 0)
            {
                Folder folder = mainmenu.SelectContentFolder(routeSelection[0]);
                await FindRoutes(folder).ConfigureAwait(false);

                if (routeSelection.Length > 1 && Settings.RestoreLastView)
                {
                    Route route = routes?.Where(r => r.Name.Equals(routeSelection[1], StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (null != route)
                    {
                        await LoadRoute(route).ConfigureAwait(false);
                        mainmenu.PreSelectRoute(route.Name);
                        if (pathSelection.Length > 0)
                        {
                            // only restore first path for now
                            Path path = (TrackData.GameInstance(this) as TrackData).TrainPaths?.Where(p => p.FilePath.Equals(pathSelection[0], StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
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
            selectedPath = null;
            mainmenu.ClearPathMenu();
        }

        internal void UnloadPath()
        {
            selectedPath = null;
            ((ToolboxContent)contentArea?.Content).InitializePath(null, null);
        }

        private static CancellationTokenSource ResetCancellationTokenSource(CancellationTokenSource cts)
        {
            if (cts != null)
            {
                cts.Dispose();
            }
            // Create a new cancellation token source so that can cancel all the tokens again 
            return new CancellationTokenSource();
        }

    }
}
