using System;
using System.Collections.Frozen;
using System.Linq;
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

        private async Task ProfileChanged()
        {
            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            SelectedProfile = await SelectedProfile.Get(ctsProfileLoading.Token).ConfigureAwait(false);
            currentSelections = await SelectedProfile.SelectionsModel(ctsProfileLoading.Token).ConfigureAwait(false);

            //Initial setup if necessary
            if (SelectedProfile.SetupRequired() || SelectedProfile.ContentFolders.Count == 0)
            {
                await (ShowOptionsForm(true)).ConfigureAwait(false);
            }
            else
            {
                FrozenSet<FolderModel> contentFolders = await SelectedProfile.GetFolders(ctsProfileLoading.Token).ConfigureAwait(false);
                SetupFoldersDropdown(contentFolders);
                await FolderChanged(contentFolders.GetByNameOrFirstByName(currentSelections?.FolderName)).ConfigureAwait(false);
            }
            SetupActivityFromSelection(currentSelections);
        }

        private async Task FolderChanged(FolderModel contentFolder)
        {
            if (SelectedFolder == contentFolder)
                return;

            FrozenSet<RouteModelCore> routeModels = null;
            FrozenSet<WagonSetModel> consistModels = null;

            contentFolder = await contentFolder.Get(CancellationToken.None).ConfigureAwait(false);

            contentFolder = comboBoxFolder.SetComboBoxItem((FolderModel folderItem) => string.Equals(folderItem.Name, contentFolder?.Name, StringComparison.OrdinalIgnoreCase));
            currentSelections = currentSelections with { FolderName = contentFolder?.Name };
            SelectedFolder = contentFolder;

            ctsRouteLoading = await ctsRouteLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            if (contentFolder != null)
            {
                try
                {
                    routeModels = await contentFolder.GetRoutes(ctsRouteLoading.Token).ConfigureAwait(false);
                    consistModels = await contentFolder.GetWagonSets(ctsRouteLoading.Token).ConfigureAwait (false);
                }
                catch (TaskCanceledException) { return; }
            }

            SetupRoutesDropdown(routeModels);
            SetupConsistsDropdown(consistModels);
            RouteModelCore routeModel = routeModels.GetByName(currentSelections?.RouteName);
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
                    pathModels = await routeModel.GetPaths(ctsPathLoading.Token).ConfigureAwait(false);
                    activityModels = await routeModel.GetActivities(ctsPathLoading.Token).ConfigureAwait(false);
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
                WagonSetName = activity.ActivityType == ActivityType.Activity ? activity.ConsistId : (comboBoxConsist.SelectedValue as WagonSetModel)?.Id,
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
