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
        private static WagonReferenceModel anyConsist;

        private async Task ProfileChanged(ProfileModel profileModel)
        {
            if (profileModel != null && SelectedProfile == profileModel)
                return;

            ctsModelLoading = await ctsModelLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            SelectedProfile = await (profileModel ?? ProfileModel.None).Get(ctsModelLoading.Token).ConfigureAwait(false);
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
            anyConsist = consistModels.Any();

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
            FrozenSet<string> timetableWeatherFile = null;

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

        private void ActivityChanged(ActivityModelCore activityModel)
        {
            if (SelectedActivity == activityModel)
                return;

            activityModel = comboBoxActivity.SetComboBoxItem((ActivityModelCore activityItem) => string.Equals(activityItem.Id, activityModel?.Id, StringComparison.OrdinalIgnoreCase));

            currentSelections = currentSelections with
            {
                ActivityName = activityModel?.Name,
                ActivityType = activityModel.ActivityType,
                StartTime = activityModel.ActivityType == ActivityType.Activity ? activityModel.StartTime : comboBoxStartTime.Tag != null ? (TimeOnly)comboBoxStartTime.Tag : activityModel.StartTime,
                Season = activityModel.ActivityType == ActivityType.Activity ? activityModel.Season : (SeasonType)comboBoxStartSeason.SelectedValue,
                Weather = activityModel.ActivityType == ActivityType.Activity ? activityModel.Weather : (WeatherType)comboBoxStartWeather.SelectedValue,
                PathName = activityModel.ActivityType == ActivityType.Activity ? activityModel.PathId : (comboBoxHeadTo.SelectedValue as PathModelCore)?.Id,
                WagonSetName = activityModel.ActivityType == ActivityType.Activity ? activityModel.ConsistId : (comboBoxConsist.SelectedValue as WagonSetModel)?.Id,
            };
            SelectedActivity = activityModel;

            SetupActivityFromSelection(currentSelections);
        }

        private void LocomotiveChanged(WagonSetModel wagonSetModel, bool any)
        {
            if (!any && wagonSetModel == SelectedConsist)
                return;

            if (any)
            {
            }
            else
            {
                _ = comboBoxLocomotive.SetComboBoxItem((IGrouping<string, WagonSetModel> grouping) => grouping.Key != anyConsist.Name && grouping.Where(w => string.Equals(w.Id, wagonSetModel.Id, StringComparison.OrdinalIgnoreCase)).Any());
                currentSelections = currentSelections with
                {
                    WagonSetName = wagonSetModel?.Id,
                };
                SelectedConsist = wagonSetModel;
            }
            SetupConsistsDropdown();
            _ = comboBoxConsist.SetComboBoxItem((WagonSetModel wagonSetItem) => string.Equals(wagonSetItem.Id, currentSelections.WagonSetName, StringComparison.OrdinalIgnoreCase));
        }

        private void ConsistChanged(WagonSetModel wagonSetModel)
        {
            if (wagonSetModel == SelectedConsist)
                return;

            wagonSetModel = comboBoxConsist.SetComboBoxItem((WagonSetModel wagonSetItem) => string.Equals(wagonSetItem.Id, wagonSetModel.Id, StringComparison.OrdinalIgnoreCase));
            currentSelections = currentSelections with
            {
                WagonSetName = wagonSetModel?.Id,
            };
            SelectedConsist = wagonSetModel;
            _ = comboBoxLocomotive.SetComboBoxItem((IGrouping<string, WagonSetModel> grouping) => grouping.Key != anyConsist.Name && grouping.Where(w => string.Equals(w.Id, wagonSetModel.Id, StringComparison.OrdinalIgnoreCase)).Any());
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
