using System;
using System.Collections.Frozen;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Independent.Settings;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Menu
{
    public partial class MainForm
    {
        internal ProfileSelectionsModel CurrentSelections { get; private set; }
        private static readonly WagonReferenceModel anyConsist = FrozenSet<WagonSetModel>.Empty.Any();

        private async Task ProfileChanged(ProfileModel profileModel)
        {
            if (profileModel != null && SelectedProfile == profileModel)
                return;

            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            SelectedProfile = await profileModel.Get(ctsProfileLoading.Token).ConfigureAwait(false);
            if (SelectedProfile == null)
            {
                SelectedProfile = await profileModel.Setup(null, ctsProfileLoading.Token).ConfigureAwait(false);
            }
            CurrentSelections = await SelectedProfile.SelectionsModel(ctsProfileLoading.Token).ConfigureAwait(false);

            //Initial setup if necessary
            if (SelectedProfile.ContentFolders.Count == 0)
            {
                await ShowOptionsForm(true).ConfigureAwait(false);
            }
            else
            {
                FrozenSet<FolderModel> contentFolders = await SelectedProfile.GetFolders(ctsProfileLoading.Token).ConfigureAwait(false);
                SetupFoldersDropdown(contentFolders);
                await SetupFolderFromSelection().ConfigureAwait(false);
            }
        }

        private async Task FolderChanged(FolderModel contentFolder)
        {
            if (contentFolder.Name == CurrentSelections.FolderName)
                return;

            contentFolder = comboBoxFolder.SetComboBoxItem((FolderModel folderItem) => string.Equals(folderItem.Name, contentFolder?.Name, StringComparison.OrdinalIgnoreCase));
            CurrentSelections = CurrentSelections with { FolderName = contentFolder?.Name };

            await SetupFolderFromSelection().ConfigureAwait(false);
        }

        private async ValueTask RouteChanged(RouteModelCore routeModel)
        {
            if (routeModel?.Id == CurrentSelections.RouteId)
                return;

            routeModel = comboBoxRoute.SetComboBoxItem((RouteModelCore routeModelItem) => string.Equals(routeModelItem.Name, routeModel?.Name, StringComparison.OrdinalIgnoreCase));

            CurrentSelections = CurrentSelections with { RouteId = routeModel?.Id };
            await SetupRouteFromSelection().ConfigureAwait(false);
        }

        private void ActivityChanged(ActivityModelCore activityModel)
        {
            if (activityModel?.Id == CurrentSelections.ActivityId)
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

            SetupActivityFromSelection();
        }

        private void LocomotiveChanged(WagonSetModel wagonSetModel, bool any)
        {
            if (!any && wagonSetModel?.Id == CurrentSelections.WagonSetId)
                return;

            if (!any)
            {
                _ = comboBoxLocomotive.SetComboBoxItem((IGrouping<string, WagonSetModel> grouping) => grouping.Key != anyConsist.Name && grouping.Where(w => string.Equals(w.Id, wagonSetModel?.Id, StringComparison.OrdinalIgnoreCase)).Any());
                CurrentSelections = CurrentSelections with
                {
                    WagonSetId = wagonSetModel?.Id,
                };
            }
            SetupConsistsDropdown();
            _ = comboBoxConsist.SetComboBoxItem((WagonSetModel wagonSetItem) => string.Equals(wagonSetItem.Id, wagonSetModel.Id, StringComparison.OrdinalIgnoreCase));
        }

        private void ConsistChanged(WagonSetModel wagonSetModel)
        {
            if (wagonSetModel?.Id == CurrentSelections.WagonSetId)
                return;

            wagonSetModel = comboBoxConsist.SetComboBoxItem((WagonSetModel wagonSetItem) => string.Equals(wagonSetItem.Id, wagonSetModel?.Id, StringComparison.OrdinalIgnoreCase));
            CurrentSelections = CurrentSelections with
            {
                WagonSetId = wagonSetModel?.Id,
            };
            _ = comboBoxLocomotive.SetComboBoxItem((IGrouping<string, WagonSetModel> grouping) => grouping.Key != anyConsist.Name && grouping.Where(w => string.Equals(w.Id, wagonSetModel.Id, StringComparison.OrdinalIgnoreCase)).Any());
        }

        private void PathChanged(PathModelCore pathModel)
        {
            if (pathModel?.Id == CurrentSelections.PathId)
                return;

            pathModel = comboBoxStartAt.SetComboBoxItem((IGrouping<string, PathModelCore> grouping) => grouping.Any(p => p.Id == pathModel?.Id)).FirstOrDefault(p => p.Id == pathModel?.Id);

            CurrentSelections = CurrentSelections with { PathId = pathModel?.Id };

            SetupPathEndDropdown();
            _ = comboBoxHeadTo.SetComboBoxItem((PathModelCore pathModelItem) => string.Equals(pathModelItem.Id, pathModel.Id, StringComparison.OrdinalIgnoreCase));
        }

        private void TimetableSetChanged(TimetableModel timetableModel)
        {
            if (timetableModel?.Id == CurrentSelections.TimetableSet)
                return;

            timetableModel = comboBoxTimetableSet.SetComboBoxItem((TimetableModel timetableItem) => string.Equals(timetableModel?.Id, timetableItem.Id, StringComparison.OrdinalIgnoreCase));
            CurrentSelections = CurrentSelections with
            {
                TimetableSet = timetableModel?.Id,
            };
            SetupTimetableFromSelection();
        }

        private void TimetableChanged(IGrouping<string, TimetableTrainModel> timetableTrainModels)
        {
            if (timetableTrainModels?.Key == CurrentSelections.TimetableName)
                return;

            IGrouping<string, TimetableTrainModel> timetable = comboBoxTimetable.SetComboBoxItem((IGrouping<string, TimetableTrainModel> grouping) => string.Equals(grouping.Key, timetableTrainModels?.Key, StringComparison.OrdinalIgnoreCase));

            CurrentSelections = CurrentSelections with
            {
                TimetableName = timetableTrainModels.Key,
            };

            SetupTimetableTrainsDropdown();
        }

        private void TimetableTrainChanged(TimetableTrainModel timetableTrainModel)
        {
            if (timetableTrainModel?.Id == CurrentSelections.TimetableTrain)
                return;

            timetableTrainModel = comboBoxTimetableTrain.SetComboBoxItem((TimetableTrainModel timetableTrainItem) => string.Equals(timetableTrainModel?.Id, timetableTrainItem.Id, StringComparison.OrdinalIgnoreCase));

            CurrentSelections = CurrentSelections with 
            { 
                TimetableTrain = timetableTrainModel.Id 
            };
        }

        private void TimetableWeatherChanged(WeatherModelCore weatherModel)
        {
            if (weatherModel?.Id == CurrentSelections.WeatherChanges)
                return;

            weatherModel = comboBoxTimetableWeatherFile.SetComboBoxItem((WeatherModelCore weatherItem) => string.Equals(weatherItem.Id, weatherModel?.Id, StringComparison.OrdinalIgnoreCase));

            CurrentSelections = CurrentSelections with
            {
                WeatherChanges = weatherModel?.Id,
            };
        }

        #region setup from selections
        private async Task SetupFolderFromSelection()
        {
            if (InvokeRequired)
            {
                await Invoke(SetupFolderFromSelection).ConfigureAwait(false);
                return;
            }

            FolderModel contentFolder = comboBoxFolder.SetComboBoxItem((FolderModel folderItem) => string.Equals(folderItem.Name, CurrentSelections?.FolderName, StringComparison.OrdinalIgnoreCase));

            FrozenSet<RouteModelCore> routeModels = null;
            FrozenSet<WagonSetModel> consistModels = null;
            FrozenSet<WagonReferenceModel> locomotives = null;

            if (contentFolder != null)
            {
                ctsFolderLoading = await ctsFolderLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

                try
                {
                    routeModels = await contentFolder.GetRoutes(ctsFolderLoading.Token).ConfigureAwait(false);
                    consistModels = await contentFolder.GetWagonSets(ctsFolderLoading.Token).ConfigureAwait(false);
                    locomotives = await contentFolder.GetLocomotives(ctsFolderLoading.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException) { return; }
            }

            SetupRoutesDropdown(routeModels);
            SetupLocomotivesDropdown(consistModels);
            await SetupRouteFromSelection().ConfigureAwait(false);
        }

        private async Task SetupRouteFromSelection() 
        {
            if (InvokeRequired)
            {
                await Invoke(SetupRouteFromSelection).ConfigureAwait(false);
                return;
            }

            RouteModelCore routeModel = comboBoxRoute.SetComboBoxItem((RouteModelCore routeModelItem) => string.Equals(routeModelItem.Id, CurrentSelections?.RouteId, StringComparison.OrdinalIgnoreCase));

            CurrentSelections = CurrentSelections with { RouteId = routeModel?.Id };

            FrozenSet<PathModelCore> pathModels = null;
            FrozenSet<ActivityModelCore> activityModels = null;
            FrozenSet<WeatherModelCore> timetableWeatherFiles = null;
            FrozenSet<TimetableModel> timetableModels = null;

            if (routeModel != null)
            {
                ctsRouteLoading = await ctsRouteLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);
                try
                {
                    pathModels = await routeModel.GetPaths(ctsRouteLoading.Token).ConfigureAwait(false);
                    activityModels = await routeModel.GetActivities(ctsRouteLoading.Token).ConfigureAwait(false);
                    timetableModels = await routeModel.GetTimetables(ctsRouteLoading.Token).ConfigureAwait(false);
                    timetableWeatherFiles = await routeModel.GetWeatherFiles(ctsRouteLoading.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException) { }
            }

            SetupActivitiesDropdown(activityModels ?? FrozenSet<ActivityModelCore>.Empty);
            SetupPathStartDropdown(pathModels ?? FrozenSet<PathModelCore>.Empty);
            SetupPathEndDropdown();

            SetupTimetableSetDropdown(timetableModels ?? FrozenSet<TimetableModel>.Empty);
            SetupTimetableWeatherDropdown(timetableWeatherFiles ?? FrozenSet<WeatherModelCore>.Empty);

            SetupActivityFromSelection();
            SetupTimetableFromSelection();
        }

        private void SetupActivityFromSelection()
        {
            if (InvokeRequired)
            {
                Invoke(SetupActivityFromSelection);
                return;
            }

            bool exploreActivity = CurrentSelections != null && (CurrentSelections.ActivityType is ActivityType.ExploreActivity or ActivityType.Explorer);
            bool activity = exploreActivity || (CurrentSelections?.ActivityType is ActivityType.Activity);
            radioButtonModeTimetable.Checked = !(radioButtonModeActivity.Checked = CurrentSelections.ActivityType == ActivityType.TimeTable);

            // values
            _ = comboBoxStartSeason.SetComboBoxItem((ComboBoxItem<SeasonType> cbi) => cbi.Value == CurrentSelections.Season);
            _ = comboBoxStartWeather.SetComboBoxItem((ComboBoxItem<WeatherType> cbi) => cbi.Value == CurrentSelections.Weather);

            comboBoxStartTime.Text = $"{CurrentSelections.StartTime:HH\\:mm\\:ss}";
            comboBoxStartTime.Tag = CurrentSelections.StartTime;

            ActivityModelCore activityModel = null;

            if (activity)
            {
                activityModel = comboBoxActivity.SetComboBoxItem((ActivityModelCore activityItem) => string.Equals(activityItem.Id, CurrentSelections.ActivityId, StringComparison.OrdinalIgnoreCase));
            }
            else if (exploreActivity)
            {
                activityModel = comboBoxActivity.SetComboBoxItem((ActivityModelCore activityItem) => activityItem.ActivityType == CurrentSelections.ActivityType);
            }

            _ = comboBoxLocomotive.SetComboBoxItem((IGrouping<string, WagonSetModel> grouping) => grouping.Key != anyConsist.Name && grouping.Where(w => string.Equals(w.Id, CurrentSelections.WagonSetId, StringComparison.OrdinalIgnoreCase)).Any());
            SetupConsistsDropdown();
            _ = comboBoxConsist.SetComboBoxItem((ComboBoxItem<WagonSetModel> cbi) => string.Equals(cbi.Value.Id, CurrentSelections.WagonSetId, StringComparison.OrdinalIgnoreCase));

            _ = comboBoxStartAt.SetComboBoxItem((IGrouping<string, PathModelCore> grouping) => grouping.Where(p => string.Equals(p.Id, CurrentSelections.PathId, StringComparison.OrdinalIgnoreCase)).Any());
            SetupPathEndDropdown();
            _ = comboBoxHeadTo.SetComboBoxItem((ComboBoxItem<PathModelCore> cbi) => string.Equals(CurrentSelections.PathId, cbi.Value.Id, StringComparison.OrdinalIgnoreCase));

            if (radioButtonModeActivity.Checked)
            {
                CurrentSelections = CurrentSelections with
                {
                    ActivityId = activityModel?.Id,
                    ActivityType = activityModel?.ActivityType ?? ActivityType.None,
                    StartTime = activityModel?.ActivityType == ActivityType.Activity ? activityModel.StartTime : comboBoxStartTime.Tag != null ? (TimeOnly)comboBoxStartTime.Tag : activityModel.StartTime,
                    Season = activityModel?.ActivityType == ActivityType.Activity ? activityModel.Season : (SeasonType)comboBoxStartSeason.SelectedValue,
                    Weather = activityModel?.ActivityType == ActivityType.Activity ? activityModel.Weather : (WeatherType)comboBoxStartWeather.SelectedValue,
                    PathId = activityModel?.ActivityType == ActivityType.Activity ? activityModel.PathId : (comboBoxHeadTo.SelectedValue as PathModelCore)?.Id,
                    WagonSetId = activityModel?.ActivityType == ActivityType.Activity ? activityModel.ConsistId : (comboBoxConsist.SelectedValue as WagonSetModel)?.Id,
                };
            }

            //enabled
            UpdateEnabled();
            ShowDetails();
        }

        private void SetupTimetableFromSelection()
        {
            if (InvokeRequired)
            {
                Invoke(SetupTimetableFromSelection);
                return;
            }

            // values
            _ = comboBoxStartSeason.SetComboBoxItem((ComboBoxItem<SeasonType> cbi) => cbi.Value == CurrentSelections.Season);
            _ = comboBoxStartWeather.SetComboBoxItem((ComboBoxItem<WeatherType> cbi) => cbi.Value == CurrentSelections.Weather);

            TimetableModel timetableModel = comboBoxTimetableSet.SetComboBoxItem((TimetableModel timetableItem) => string.Equals(timetableItem.Id, CurrentSelections.TimetableSet, StringComparison.OrdinalIgnoreCase));
            SetupTimetableDropdown();
            IGrouping<string, TimetableTrainModel> timetable = comboBoxTimetable.SetComboBoxItem((IGrouping<string, TimetableTrainModel> grouping) => string.Equals(grouping.Key, CurrentSelections.TimetableName, StringComparison.OrdinalIgnoreCase));
            SetupTimetableTrainsDropdown();
            TimetableTrainModel timetableTrainModel = comboBoxTimetableTrain.SetComboBoxItem((TimetableTrainModel timetableTrainItem) => string.Equals(timetableTrainItem.Id, CurrentSelections.TimetableTrain, StringComparison.OrdinalIgnoreCase));

            WeatherModelCore weatherModel = comboBoxTimetableWeatherFile.SetComboBoxItem((WeatherModelCore weatherItem) => string.Equals(weatherItem.Id, CurrentSelections.WeatherChanges, StringComparison.OrdinalIgnoreCase));
            comboBoxTimetableDay.SelectedIndex = (int)CurrentSelections.TimetableDay;

            if (radioButtonModeTimetable.Checked)
            {
                CurrentSelections = CurrentSelections with
                {
                    ActivityType = timetableTrainModel != null ? ActivityType.TimeTable : ActivityType.None,
                    TimetableSet = timetableModel?.Id,
                    TimetableName = timetable?.Key,
                    TimetableTrain = timetableTrainModel?.Id,
                    WeatherChanges = weatherModel?.Id,
                };
            }

            UpdateEnabled();
            ShowDetails();
        }
        #endregion
    }
}
