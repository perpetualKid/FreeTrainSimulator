using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Independent.Settings;
using FreeTrainSimulator.Models.Loader.Shim;
using FreeTrainSimulator.Models.Simplified;

namespace Orts.Menu
{
    public partial class MainForm
    {
        private ProfileSelectionsModel currentSelections;

        private async ValueTask ProfileChanged()
        {
            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            SelectedProfile = await SelectedProfile.Get(ctsProfileLoading.Token).ConfigureAwait(false);
            currentSelections = await SelectedProfile.SelectionsModel(ctsProfileLoading.Token).ConfigureAwait(false);
            SetupActivitySelections();
            try
            {
                if (SelectedProfile.SetupRequired())
                {
                    SelectedProfile = await SelectedProfile.Convert(settings.FolderSettings.Folders.Select(item => (item.Key, item.Value)), ctsProfileLoading.Token).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
            }
            SelectedProfile ??= SelectedProfile.Default();

            radioButtonModeActivity.Checked = !(radioButtonModeTimetable.Checked = currentSelections.ActivityType == ActivityType.TimeTable);

            //Initial setup if necessary
            if (SelectedProfile.ContentFolders.Count == 0)
            {
                await ShowOptionsForm(true);
            }
            else
            {
                await FoldersChanged(SelectedProfile.ContentFolders).ConfigureAwait(false);
            }
        }

        private async ValueTask FoldersChanged(FrozenSet<FolderModel> contentFolders)
        {
            SetupFoldersDropdown(contentFolders);
            FolderModel folderModel = contentFolders.Where(f => f.Name == currentSelections?.FolderName).FirstOrDefault();
            await FolderChanged(folderModel).ConfigureAwait(false);
        }

        private async ValueTask FolderChanged(FolderModel contentFolder)
        {
            if (SelectedFolder == contentFolder)
                return;
            currentSelections = (currentSelections ?? new ProfileSelectionsModel()) with { FolderName = contentFolder?.Name };
            SelectedFolder = contentFolder;

            if (!comboBoxFolder.SetComboBoxItem((ComboBoxItem<FolderModel> cbi) => string.Equals(cbi.Value.Name, currentSelections.FolderName, StringComparison.OrdinalIgnoreCase)))
            {

            }

            ctsRouteLoading = await ctsRouteLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);
            FrozenSet<RouteModelCore> routeModels = null;
            if (contentFolder != null)
            {
                try
                {
                    routeModels = contentFolder.SetupRequired() ? (await contentFolder.Convert(ctsRouteLoading.Token).ConfigureAwait(false)).Routes : contentFolder.Routes;
                }
                catch (TaskCanceledException)
                {
                }
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

            currentSelections = (currentSelections ?? new ProfileSelectionsModel()) with { RouteName = routeModel?.Name };
            SelectedRoute = routeModel;

            if (!comboBoxRoute.SetComboBoxItem((ComboBoxItem<RouteModelCore> cbi) => string.Equals(cbi.Value.Name, currentSelections.RouteName, StringComparison.OrdinalIgnoreCase)))
            {
                routeModel = comboBoxRoute.SynchronizedValue<RouteModelCore>();
                if (routeModel != null)
                    await RouteChanged(routeModel).ConfigureAwait(false);
                return;
            }

            // Activities
            ctsActivityLoading = await ctsActivityLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);
            try
            {
                activities = (await Activity.GetActivities(SelectedFolder.MstsContentFolder(), SelectedRoute.MstsRouteFolder(), ctsActivityLoading.Token).ConfigureAwait(true)).OrderBy(a => a.Name);
            }
            catch (TaskCanceledException)
            {
                activities = Array.Empty<Activity>();
            }
            await ActivitiesChanged(activities).ConfigureAwait(false);

            // Paths
            ctsPathLoading = await ctsPathLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);
            FrozenSet<PathModelCore> pathModels = null;
            if (routeModel != null)
            {
                if (routeModel.SetupRequired())
                    routeModel = await (routeModel.Convert(ctsRouteLoading.Token)).ConfigureAwait(false);

                try
                {
                    pathModels = routeModel.ChildsInitialized ? routeModel.TrainPaths :
                        (routeModel = await routeModel.Expand(ctsRouteLoading.Token).ConfigureAwait(false)).TrainPaths;
                }
                catch (TaskCanceledException)
                {
                }
            }
            pathModels ??= FrozenSet<PathModelCore>.Empty;

            await PathsChanged(pathModels).ConfigureAwait(false);

            //TODO load Timetablesets
            SelectedRoute = routeModel;
        }

        private async ValueTask ActivitiesChanged(IEnumerable<Activity> activities)
        {
            SetupActivitiesDropdown(activities);
            Activity activity = activities.Where(a => a.Name == currentSelections?.ActivityName).FirstOrDefault();
            await ActivityChanged(activity).ConfigureAwait(false);
        }

        private async ValueTask ActivityChanged(Activity activity)
        {
            if (SelectedActivity == activity)
                return;
            currentSelections = (currentSelections ?? new ProfileSelectionsModel()) with 
            { 
                ActivityName = activity?.Name, 
                ActivityType = activity switch { DefaultExploreActivity => ActivityType.Explorer, ExploreThroughActivity => ActivityType.ExploreActivity, _ => ActivityType.Activity} 
            };
            SelectedActivity = activity;

            if (!comboBoxActivity.SetComboBoxItem((ComboBoxItem<Activity> cbi) => string.Equals(cbi.Value.Name, currentSelections.ActivityName, StringComparison.OrdinalIgnoreCase)))
            {
                activity = comboBoxActivity.SynchronizedValue<Activity>();
                if (activity != null)
                    await ActivityChanged(activity).ConfigureAwait(false);
                return;
            }
            SetupActivityStartDetails((activity.Season, activity.Weather, TimeOnly.FromTimeSpan(activity.StartTime)));
            PathModelCore pathModel = SelectedRoute.TrainPaths?.Where(p => p.Name == activity?.Path.Name).FirstOrDefault();
            await PathChanged(pathModel).ConfigureAwait(false);
            ShowDetails();
        }

        private async ValueTask PathsChanged(FrozenSet<PathModelCore> pathModels)
        {
            SetupPathStartDropdown(pathModels);
            PathModelCore pathModel = pathModels.Where(p => p.Name == currentSelections?.PathName).FirstOrDefault();
            await PathChanged(pathModel).ConfigureAwait(false);
        }

        private async ValueTask PathChanged(PathModelCore pathModel)
        {
            if (pathModel == SelectedPath)
                return;
            currentSelections = (currentSelections ?? new ProfileSelectionsModel()) with { PathName = pathModel?.Name };
            SelectedPath = pathModel;
            if (!comboBoxStartAt.SetComboBoxItem((ComboBoxItem<IGrouping<string, PathModelCore>> cbi) => cbi.Value.Where(p => p.Name == currentSelections.PathName).Any()))
            {
                pathModel = comboBoxStartAt.SynchronizedValue<IGrouping<string, PathModelCore>>().FirstOrDefault();
                if (pathModel != null)
                    await PathChanged(pathModel).ConfigureAwait(false);
            }
            SetupPathEndDropdown();
            comboBoxHeadTo.SetComboBoxItem((ComboBoxItem<PathModelCore> cbi) => string.Equals(currentSelections.PathName, cbi.Value.Name, StringComparison.OrdinalIgnoreCase));
            UpdateEnabled();
        }
    }
}
