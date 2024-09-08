using System;
using System.Collections.Frozen;
using System.Linq;
using System.Text;
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
                if (SelectedProfile.SetupRequired())
                {
                    SelectedProfile = await SelectedProfile.Convert(settings.FolderSettings.Folders.Select(item => (item.Key, item.Value)), ctsProfileLoading.Token).ConfigureAwait(false);
                }
            } catch (TaskCanceledException) { return; }

            SelectedProfile ??= SelectedProfile.Default();

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

            contentFolder = comboBoxFolder.SetComboBoxItem((FolderModel folderItem) => string.Equals(folderItem.Name, contentFolder?.Name, StringComparison.OrdinalIgnoreCase));
            currentSelections = (currentSelections ?? new ProfileSelectionsModel()) with { FolderName = contentFolder?.Name };
            SelectedFolder = contentFolder;

            ctsRouteLoading = await ctsRouteLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            FrozenSet<RouteModelCore> routeModels = null;
            if (contentFolder != null)
            {
                try
                {
                    routeModels = contentFolder.SetupRequired() ? (await contentFolder.Convert(ctsRouteLoading.Token).ConfigureAwait(false)).Routes : contentFolder.Routes;
                }
                catch (TaskCanceledException) { return; }
            }
            routeModels ??= FrozenSet<RouteModelCore>.Empty;
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

            currentSelections = (currentSelections ?? new ProfileSelectionsModel()) with { RouteName = routeModel?.Name };
            SelectedRoute = routeModel;

            if (routeModel != null)
            {
                ctsPathLoading = await ctsPathLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);
                try
                {
                    if (!routeModel.ChildsInitialized)
                        await routeModel.Expand(ctsRouteLoading.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException) { }
            }

            ActivityModelCore activityModel = await routeModel.ActivityModelFromSettings(currentSelections, ctsPathLoading.Token).ConfigureAwait(false);
            // Activities
            // Paths

            SetupActivitiesDropdown(routeModel.RouteActivities ?? FrozenSet<ActivityModelCore>.Empty);
            SetupPathStartDropdown(routeModel.TrainPaths ?? FrozenSet<PathModelCore>.Empty);

            //TODO load Timetablesets
            SelectedRoute = routeModel;
        }

        private void ActivityChanged(ActivityModelCore activity)
        {
            if (SelectedActivity == activity)
                return;

            activity = comboBoxActivity.SetComboBoxItem((ActivityModelCore activityItem) => string.Equals(activityItem.Name, activity?.Name, StringComparison.OrdinalIgnoreCase));

            currentSelections = (currentSelections ?? new ProfileSelectionsModel()) with
            {
                ActivityName = activity?.Name,
                ActivityType = activity.ActivityType,
                StartTime = activity.ActivityType == ActivityType.Activity ? activity.StartTime : comboBoxStartTime.Tag != null ? (TimeOnly)comboBoxStartTime.Tag : activity.StartTime,
                Season = activity.ActivityType == ActivityType.Activity ? activity.Season : (SeasonType)comboBoxStartSeason.SelectedValue,
                Weather = activity.ActivityType == ActivityType.Activity ? activity.Weather : (WeatherType)comboBoxStartWeather.SelectedValue,
                PathName = activity.ActivityType == ActivityType.Activity ? activity.PathId : (comboBoxHeadTo.SelectedValue as PathModelCore)?.PathId,
            };
            SelectedActivity = activity;

            SetupActivityFromSelection(currentSelections);
        }

        private void PathChanged(PathModelCore pathModel)
        {
            if (pathModel == SelectedPath)
                return;

            pathModel = comboBoxStartAt.SetComboBoxItem((IGrouping<string, PathModelCore> grouping) => grouping.Where(p => p.Name == pathModel?.Name).Any()).Where(p => p.Name == pathModel?.Name).FirstOrDefault();

            currentSelections = (currentSelections ?? new ProfileSelectionsModel()) with { PathName = pathModel?.Name };
            SelectedPath = pathModel;

            SetupPathEndDropdown();
            _ = comboBoxHeadTo.SetComboBoxItem((ComboBoxItem<PathModelCore> cbi) => string.Equals(currentSelections.PathName, cbi.Value.Name, StringComparison.OrdinalIgnoreCase));
            UpdateEnabled();
            return;
        }
    }
}
