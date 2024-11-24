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
        internal ProfileSelectionsModel CurrentSelections { get; private set; }
        private static WagonReferenceModel anyConsist;

        private async Task ProfileChanged(ProfileModel profileModel)
        {
            if (profileModel != null && SelectedProfile == profileModel)
                return;

            ctsModelLoading = await ctsModelLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            SelectedProfile = await (profileModel ?? ProfileModel.None).Get(ctsModelLoading.Token).ConfigureAwait(false);
            CurrentSelections = await SelectedProfile.SelectionsModel(ctsModelLoading.Token).ConfigureAwait(false);

            //Initial setup if necessary
            if (SelectedProfile.ContentFolders.Count == 0)
            {
                await (ShowOptionsForm(true)).ConfigureAwait(false);
            }
            else
            {
                FrozenSet<FolderModel> contentFolders = await SelectedProfile.GetFolders(ctsModelLoading.Token).ConfigureAwait(false);
                SetupFoldersDropdown(contentFolders);
                await FolderChanged(contentFolders.GetByNameOrFirstByName(CurrentSelections?.FolderName)).ConfigureAwait(false);
            }
            SetupActivityFromSelection(CurrentSelections);
            SetupTimetableFromSelection(CurrentSelections);
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
            CurrentSelections = CurrentSelections with { FolderName = contentFolder?.Name };
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
            RouteModelCore routeModel = routeModels.GetById(CurrentSelections?.RouteId);
            await RouteChanged(routeModel).ConfigureAwait(false);
        }

        private async ValueTask RouteChanged(RouteModelCore routeModel)
        {
            if (SelectedRoute == routeModel)
                return;

            routeModel = comboBoxRoute.SetComboBoxItem((RouteModelCore routeModelItem) => string.Equals(routeModelItem.Name, routeModel?.Name, StringComparison.OrdinalIgnoreCase));

            CurrentSelections = CurrentSelections with { RouteId = routeModel?.Id };
            SelectedRoute = routeModel;

            FrozenSet<PathModelCore> pathModels = null;
            FrozenSet<ActivityModelCore> activityModels = null;
            FrozenSet<WeatherModelCore> timetableWeatherFiles = null;
            FrozenSet<TimetableModel> timetableModels = null;

            if (routeModel != null)
            {
                ctsModelLoading = await ctsModelLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);
                try
                {
                    pathModels = await routeModel.GetPaths(ctsModelLoading.Token).ConfigureAwait(false);
                    activityModels = await routeModel.GetActivities(ctsModelLoading.Token).ConfigureAwait(false);
                    timetableModels = await routeModel.GetTimetables(ctsModelLoading.Token).ConfigureAwait(false);
                    timetableWeatherFiles = await routeModel.GetWeatherFiles(ctsModelLoading.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException) { }
            }

            SetupActivitiesDropdown(activityModels ?? FrozenSet<ActivityModelCore>.Empty);
            SetupPathStartDropdown(pathModels ?? FrozenSet<PathModelCore>.Empty);
            SetupPathEndDropdown();

            SetupTimetableSetDropdown(timetableModels ?? FrozenSet<TimetableModel>.Empty);
            SetupTimetableWeatherDropdown(timetableWeatherFiles ?? FrozenSet<WeatherModelCore>.Empty);
            SelectedRoute = routeModel;
        }

        private void ActivityChanged(ActivityModelCore activityModel)
        {
            if (SelectedActivity == activityModel)
                return;

            activityModel = comboBoxActivity.SetComboBoxItem((ActivityModelCore activityItem) => string.Equals(activityItem.Id, activityModel?.Id, StringComparison.OrdinalIgnoreCase));

            CurrentSelections = CurrentSelections with
            {
                ActivityId = activityModel?.Id,
                ActivityType = activityModel.ActivityType,
                StartTime = activityModel.ActivityType == ActivityType.Activity ? activityModel.StartTime : comboBoxStartTime.Tag != null ? (TimeOnly)comboBoxStartTime.Tag : activityModel.StartTime,
                Season = activityModel.ActivityType == ActivityType.Activity ? activityModel.Season : (SeasonType)comboBoxStartSeason.SelectedValue,
                Weather = activityModel.ActivityType == ActivityType.Activity ? activityModel.Weather : (WeatherType)comboBoxStartWeather.SelectedValue,
                PathId = activityModel.ActivityType == ActivityType.Activity ? activityModel.PathId : (comboBoxHeadTo.SelectedValue as PathModelCore)?.Id,
                WagonSetId = activityModel.ActivityType == ActivityType.Activity ? activityModel.ConsistId : (comboBoxConsist.SelectedValue as WagonSetModel)?.Id,
            };
            SelectedActivity = activityModel;

            SetupActivityFromSelection(CurrentSelections);
        }

        private void LocomotiveChanged(WagonSetModel wagonSetModel, bool any)
        {
            if (!any && wagonSetModel == SelectedConsist)
                return;

            if (!any)
            {
                _ = comboBoxLocomotive.SetComboBoxItem((IGrouping<string, WagonSetModel> grouping) => grouping.Key != anyConsist.Name && grouping.Where(w => string.Equals(w.Id, wagonSetModel.Id, StringComparison.OrdinalIgnoreCase)).Any());
                CurrentSelections = CurrentSelections with
                {
                    WagonSetId = wagonSetModel?.Id,
                };
                SelectedConsist = wagonSetModel;
            }
            SetupConsistsDropdown();
            _ = comboBoxConsist.SetComboBoxItem((WagonSetModel wagonSetItem) => string.Equals(wagonSetItem.Id, CurrentSelections.WagonSetId, StringComparison.OrdinalIgnoreCase));
        }

        private void ConsistChanged(WagonSetModel wagonSetModel)
        {
            if (wagonSetModel == SelectedConsist)
                return;

            wagonSetModel = comboBoxConsist.SetComboBoxItem((WagonSetModel wagonSetItem) => string.Equals(wagonSetItem.Id, wagonSetModel.Id, StringComparison.OrdinalIgnoreCase));
            CurrentSelections = CurrentSelections with
            {
                WagonSetId = wagonSetModel?.Id,
            };
            SelectedConsist = wagonSetModel;
            _ = comboBoxLocomotive.SetComboBoxItem((IGrouping<string, WagonSetModel> grouping) => grouping.Key != anyConsist.Name && grouping.Where(w => string.Equals(w.Id, wagonSetModel.Id, StringComparison.OrdinalIgnoreCase)).Any());
        }

        private void PathChanged(PathModelCore pathModel)
        {
            if (pathModel == SelectedPath)
                return;

            pathModel = comboBoxStartAt.SetComboBoxItem((IGrouping<string, PathModelCore> grouping) => grouping.Any(p => p.Id == pathModel?.Id)).FirstOrDefault(p => p.Id == pathModel?.Id);

            CurrentSelections = CurrentSelections with { PathId = pathModel?.Id };
            SelectedPath = pathModel;

            SetupPathEndDropdown();
            _ = comboBoxHeadTo.SetComboBoxItem((PathModelCore pathModelItem) => string.Equals(CurrentSelections.PathId, pathModelItem.Id, StringComparison.OrdinalIgnoreCase));
        }

        private void TimetableSetChanged(TimetableModel timetableModel)
        {
            if (timetableModel.Id == CurrentSelections.TimetableSet)
                return;

            timetableModel = comboBoxTimetableSet.SetComboBoxItem((TimetableModel timetableItem) => string.Equals(timetableModel?.Id, timetableItem.Id, StringComparison.OrdinalIgnoreCase));

            SetupTimetableDropdown();
            IGrouping<string, TimetableTrainModel> timetable = comboBoxTimetable.SetComboBoxItem((IGrouping<string, TimetableTrainModel> grouping) => string.Equals(grouping.Key, CurrentSelections.TimetableName, StringComparison.OrdinalIgnoreCase));

            TimetableChanged(timetable);

            CurrentSelections = CurrentSelections with
            {
                TimetableSet = timetableModel?.Id,
            };
            SetupTimetableFromSelection(CurrentSelections);
        }

        private void TimetableChanged(IGrouping<string, TimetableTrainModel> timetableTrainModels)
        {
            if (timetableTrainModels.Key == CurrentSelections.TimetableName)
                return;

            IGrouping<string, TimetableTrainModel> timetable = comboBoxTimetable.SetComboBoxItem((IGrouping<string, TimetableTrainModel> grouping) => string.Equals(grouping.Key, timetableTrainModels.Key, StringComparison.OrdinalIgnoreCase));

            CurrentSelections = CurrentSelections with
            {
                TimetableName = timetableTrainModels.Key,
            };

            SetupTimetableTrainsDropdown();
        }

        private void TimetableTrainChanged(TimetableTrainModel timetableTrainModel)
        {
            if (timetableTrainModel.Id == CurrentSelections.TimetableTrain)
                return;

            timetableTrainModel = comboBoxTimetableTrain.SetComboBoxItem((TimetableTrainModel timetableTrainItem) => string.Equals(timetableTrainModel?.Id, timetableTrainItem.Id, StringComparison.OrdinalIgnoreCase));

            CurrentSelections = CurrentSelections with 
            { 
                TimetableTrain = timetableTrainModel.Id 
            };
        }

        private void TimetableWeatherChanged(WeatherModelCore weatherModel)
        {
            if (weatherModel.Id == CurrentSelections.WeatherChanges)
                return;

            weatherModel = comboBoxTimetableWeatherFile.SetComboBoxItem((WeatherModelCore weatherItem) => string.Equals(weatherItem.Id, weatherModel?.Id, StringComparison.OrdinalIgnoreCase));

            CurrentSelections = CurrentSelections with
            {
                WeatherChanges = weatherModel?.Id,
            };
        }
    }
}
