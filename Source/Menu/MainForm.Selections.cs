using System;
using System.Collections.Frozen;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Independent.Settings;
using FreeTrainSimulator.Models.Loader.Shim;

namespace Orts.Menu
{
    public partial class MainForm
    {
        private ProfileSelectionsModel currentSelections;

        private async ValueTask ProfileChanged()
        {
            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            SelectedProfile = await SelectedProfile.Get(ctsProfileLoading.Token).ConfigureAwait(false);
            try
            {
//                if (SelectedProfile.SetupRequired())
                {
                    SelectedProfile = await SelectedProfile.Convert(settings.FolderSettings.Folders.Select(item => (item.Key, item.Value)), ctsProfileLoading.Token).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException) { return; }

            currentSelections = await SelectedProfile.SelectionsModel(ctsProfileLoading.Token).ConfigureAwait(false);

            //Initial setup if necessary
            if ((SelectedProfile.ContentFolders.Count == 0))
            {
                await (ShowOptionsForm(true)).ConfigureAwait(false);
            }
            else
            {
                await (FoldersChanged(SelectedProfile.ContentFolders)).ConfigureAwait(false);
            }
            SetupActivityFromSelection(currentSelections);
        }

        private async ValueTask FoldersChanged(FrozenSet<FolderModel> contentFolders)
        {
            SetupFoldersDropdown(contentFolders);
            FolderModel folderModel = contentFolders.Where(f => f.Name == currentSelections?.FolderName)?.FirstOrDefault() ?? contentFolders.OrderBy(f => f.Name).FirstOrDefault();
            await FolderChanged(folderModel).ConfigureAwait(false);
        }

        private async ValueTask FolderChanged(FolderModel contentFolder)
        {
            if (SelectedFolder == contentFolder)
                return;

            FrozenSet<RouteModelCore> routeModels = null;

            contentFolder = await contentFolder.Get(CancellationToken.None).ConfigureAwait(false);

            contentFolder = comboBoxFolder.SetComboBoxItem((FolderModel folderItem) => string.Equals(folderItem.Name, contentFolder?.Name, StringComparison.OrdinalIgnoreCase));
            currentSelections = currentSelections with { FolderName = contentFolder?.Name };
            SelectedFolder = contentFolder;

            ctsRouteLoading = await ctsRouteLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            if (contentFolder != null)
            {
                try
                {
                    routeModels = await contentFolder.Routes(ctsRouteLoading.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException) { return; }
            }
            //TODO load Trains
            await RoutesChanged(routeModels).ConfigureAwait(false);
        }

        private async ValueTask RoutesChanged(FrozenSet<RouteModelCore> routeModels)
        {
            SetupRoutesDropdown(routeModels);
            RouteModelCore routeModel = routeModels.Where(r => r.Name == currentSelections?.RouteName).FirstOrDefault();
            await RouteChanged(routeModel).ConfigureAwait(false);
        }

        private async ValueTask RouteChanged(RouteModelCore routeModel)
        {
            if (SelectedRoute == routeModel)
                return;

            routeModel = comboBoxRoute.SetComboBoxItem((RouteModelCore routeModelItem) => string.Equals(routeModelItem.Name, routeModel?.Name, StringComparison.OrdinalIgnoreCase));

            currentSelections = currentSelections with { RouteName = routeModel?.Name };
            SelectedRoute = routeModel;

            FrozenSet<PathModelCore> pathModels = null;
            FrozenSet<ActivityModelCore> activityModels = null;

            if (routeModel != null)
            {
                ctsPathLoading = await ctsPathLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);
                try
                {
                    pathModels = await routeModel.Paths(ctsPathLoading.Token);
                    activityModels = await routeModel.Activities(ctsPathLoading.Token);
                }
                catch (TaskCanceledException) { }
            }

            SetupActivitiesDropdown(activityModels ?? FrozenSet<ActivityModelCore>.Empty);
            SetupPathStartDropdown(pathModels ?? FrozenSet<PathModelCore>.Empty);
            SetupPathEndDropdown();

            //TODO load Timetablesets
            SelectedRoute = routeModel;
        }

        private void ActivityChanged(ActivityModelCore activity)
        {
            if (SelectedActivity == activity)
                return;

            activity = comboBoxActivity.SetComboBoxItem((ActivityModelCore activityItem) => string.Equals(activityItem.Name, activity?.Name, StringComparison.OrdinalIgnoreCase));

            currentSelections = currentSelections with
            {
                ActivityName = activity?.Name,
                ActivityType = activity.ActivityType,
                StartTime = activity.ActivityType == ActivityType.Activity ? activity.StartTime : comboBoxStartTime.Tag != null ? (TimeOnly)comboBoxStartTime.Tag : activity.StartTime,
                Season = activity.ActivityType == ActivityType.Activity ? activity.Season : (SeasonType)comboBoxStartSeason.SelectedValue,
                Weather = activity.ActivityType == ActivityType.Activity ? activity.Weather : (WeatherType)comboBoxStartWeather.SelectedValue,
                PathName = activity.ActivityType == ActivityType.Activity ? activity.PathId : (comboBoxHeadTo.SelectedValue as PathModelCore)?.Id,
            };
            SelectedActivity = activity;

            SetupActivityFromSelection(currentSelections);
        }

        private void PathChanged(PathModelCore pathModel)
        {
            if (pathModel == SelectedPath)
                return;

            pathModel = comboBoxStartAt.SetComboBoxItem((IGrouping<string, PathModelCore> grouping) => grouping.Where(p => p.Name == pathModel?.Name).Any()).Where(p => p.Name == pathModel?.Name).FirstOrDefault();

            currentSelections = currentSelections with { PathName = pathModel?.Name };
            SelectedPath = pathModel;

            SetupPathEndDropdown();
            _ = comboBoxHeadTo.SetComboBoxItem((ComboBoxItem<PathModelCore> cbi) => string.Equals(currentSelections.PathName, cbi.Value.Name, StringComparison.OrdinalIgnoreCase));
            UpdateEnabled();
            return;
        }
    }
}
