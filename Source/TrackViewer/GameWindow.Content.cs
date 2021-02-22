using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Models.Simplified;
using Orts.View.Track;

namespace Orts.TrackViewer
{
    public partial class GameWindow : Game
    {
        private Folder selectedFolder;
        private Route selectedRoute;
        private IEnumerable<Route> routes;
        private readonly SemaphoreSlim loadRoutesSemaphore = new SemaphoreSlim(1);

        internal async Task LoadFolders()
        {
            try
            {
                IOrderedEnumerable<Folder> folders = (await Folder.GetFolders(Settings.FolderSettings.Folders).ConfigureAwait(true)).OrderBy(f => f.Name);
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
            StatusMessage = route.Name;
            ContentArea = null;

            TrackData trackData = new TrackData(route.Path);

            bool? useMetricUnits = (Settings.MeasurementUnit == MeasurementUnit.Metric || Settings.MeasurementUnit == MeasurementUnit.System && System.Globalization.RegionInfo.CurrentRegion.IsMetric);
            if (Settings.MeasurementUnit == MeasurementUnit.Route)
                useMetricUnits = null;

            await trackData.LoadTrackData(useMetricUnits).ConfigureAwait(false);

            TrackContent content = new TrackContent(trackData.TrackDB, trackData.TrackSections, trackData.SignalConfig, trackData.UseMetricUnits);
            await content.Initialize().ConfigureAwait(false);
            ContentArea = new ContentArea(this, content);
            StatusMessage = null;
            selectedRoute = route;
        }

        internal async Task PreSelectRoute(string[] selection)
        {
            if (selection?.Length > 0)
            {
                Folder folder = mainmenu.SelectContentFolder(selection[0]);
                await FindRoutes(folder).ConfigureAwait(false);

                if (selection.Length > 1 && Settings.TrackViewer.LoadRouteOnStart)
                {
                    Route route = routes?.Where(r => r.Name.Equals(selection[1], StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (null != route)
                        await LoadRoute(route).ConfigureAwait(false);
                }
            }
        }

    }
}
