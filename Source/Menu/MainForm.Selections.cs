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
            ctsModelLoading = await ctsModelLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            SelectedProfile = await SelectedProfile.Get(ctsModelLoading.Token).ConfigureAwait(false);
            currentSelections = await SelectedProfile.SelectionsModel(ctsModelLoading.Token).ConfigureAwait(false);

            //Initial setup if necessary
            if (SelectedProfile.ContentFolders.Count == 0)
            {
                await (ShowOptionsForm(true)).ConfigureAwait(false);
            }
            else
            {
                FrozenSet<FolderModel> contentFolders = await SelectedProfile.GetFolders(ctsModelLoading.Token).ConfigureAwait(false);
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
            FrozenSet<WagonReferenceModel> locomotives = null;

            contentFolder = await contentFolder.Get(CancellationToken.None).ConfigureAwait(false);

            contentFolder = comboBoxFolder.SetComboBoxItem((FolderModel folderItem) => string.Equals(folderItem.Name, contentFolder?.Name, StringComparison.OrdinalIgnoreCase));
            currentSelections = currentSelections with { FolderName = contentFolder?.Name };
            SelectedFolder = contentFolder;

            ctsModelLoading = await ctsModelLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            if (contentFolder != null)
            {
                try
                {
                    routeModels = await contentFolder.GetRoutes(ctsModelLoading.Token).ConfigureAwait(false);
                    consistModels = await contentFolder.GetWagonSets(ctsModelLoading.Token).ConfigureAwait(false);
                    locomotives = await contentFolder.GetLocomotives(ctsModelLoading.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException) { return; }
            }

            SetupRoutesDropdown(routeModels);
            SetupLocomotivesDropdown(consistModels);
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
                ctsModelLoading = await ctsModelLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);
                try
                {
                    pathModels = await routeModel.GetPaths(ctsModelLoading.Token).ConfigureAwait(false);
                    activityModels = await routeModel.GetActivities(ctsModelLoading.Token).ConfigureAwait(false);
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

            activity = comboBoxActivity.SetComboBoxItem((ActivityModelCore activityItem) => string.Equals(activityItem.Id, activity?.Id, StringComparison.OrdinalIgnoreCase));

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

        private void LocomotiveChanged(WagonSetModel wagonSetModel)
        {
            if (wagonSetModel == SelectedConsist)
                return;

            wagonSetModel = comboBoxConsist.SetComboBoxItem((WagonSetModel wagonSetItem) => string.Equals(wagonSetItem.Id, wagonSetModel?.Id, StringComparison.OrdinalIgnoreCase));
            currentSelections = currentSelections with
            {
                WagonSetName = wagonSetModel?.Id,
            };
            SelectedConsist = wagonSetModel;
            SetupConsistsDropdown();
            _ = comboBoxConsist.SetComboBoxItem((ComboBoxItem<WagonSetModel> cbi) => string.Equals(cbi.Value.Id, currentSelections.WagonSetName, StringComparison.OrdinalIgnoreCase));
        }

        private void PathChanged(PathModelCore pathModel)
        {
            if (pathModel == SelectedPath)
                return;

            pathModel = comboBoxStartAt.SetComboBoxItem((IGrouping<string, PathModelCore> grouping) => grouping.Any(p => p.Id == pathModel?.Id)).FirstOrDefault(p => p.Id == pathModel?.Id);

            currentSelections = currentSelections with { PathName = pathModel?.Id };
            SelectedPath = pathModel;

            SetupPathEndDropdown();
            _ = comboBoxHeadTo.SetComboBoxItem((ComboBoxItem<PathModelCore> cbi) => string.Equals(currentSelections.PathName, cbi.Value.Id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
