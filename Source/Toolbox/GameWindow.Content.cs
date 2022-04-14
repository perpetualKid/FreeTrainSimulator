using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Models.Simplified;
using Orts.Graphics.MapView;
using Orts.Toolbox.PopupWindows;

namespace Orts.Toolbox
{
    public class ContentAreaChangedEventArgs: EventArgs
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
        private IEnumerable<Route> routes;
        private IEnumerable<Path> paths;
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
            (windowManager[WindowType.StatusWindow] as StatusTextWindow).RouteName = route.Name;
            windowManager[WindowType.StatusWindow].Open();
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

            await TrackData.LoadTrackData(route.Path, useMetricUnits, token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
                return;

            ToolboxContent content = new ToolboxContent(this);
            await content.Initialize().ConfigureAwait(false);
            content.InitializeItemVisiblity(Settings.ViewSettings);
            content.UpdateWidgetColorSettings(Settings.ColorSettings);
            ContentArea = content.ContentArea;
            paths = await Path.GetPaths(route, true, CancellationToken.None).ConfigureAwait(false);
            mainmenu.PopulatePaths(paths);
            windowManager[WindowType.StatusWindow].Close();
            selectedRoute = route;
           
        }

        internal async Task PreSelectRoute(string[] selection)
        {
            if (selection?.Length > 0)
            {
                Folder folder = mainmenu.SelectContentFolder(selection[0]);
                await FindRoutes(folder).ConfigureAwait(false);

                if (selection.Length > 1 && Settings.RestoreLastView)
                {
                    Route route = routes?.Where(r => r.Name.Equals(selection[1], StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (null != route)
                    {
                        await LoadRoute(route).ConfigureAwait(false);

                    }

                }
            }
        }

        internal void UnloadRoute()
        {
            ContentArea = null;
            selectedRoute = null;
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
