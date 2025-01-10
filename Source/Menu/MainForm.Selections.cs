using System;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Logging;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Menu
{
    public partial class MainForm
    {
        private static readonly WagonReferenceModel anyConsist = FrozenSet<WagonSetModel>.Empty.Any();

        private async Task ProfileChanged(ProfileModel profileModel)
        {
            if (profileModel != null && SelectedProfile == profileModel)
                return;

            ctsProfileLoading = await ctsProfileLoading.ResetCancellationTokenSource(semaphoreSlim, true).ConfigureAwait(false);

            ContentModel = await ContentModel.Get(ctsProfileLoading.Token).ConfigureAwait(false);
            SelectedProfile = profileModel;
            UpdateProfilesDropdown(profileModel);
            ProfileSelections = await SelectedProfile.LoadSettingsModel<ProfileSelectionsModel>(ctsProfileLoading.Token).ConfigureAwait(false);
            ProfileUserSettings = await SelectedProfile.LoadSettingsModel<ProfileUserSettingsModel>(ctsProfileLoading.Token).ConfigureAwait(false);

            if (ProfileUserSettings.TraceType != TraceEventType.Critical)
            {
                string logFileName = RuntimeInfo.LogFile(ProfileUserSettings.LogFilePath, ProfileUserSettings.LogFileName);
                LoggingUtil.InitLogging(logFileName, TraceEventType.Error, false, false);
                ProfileUserSettings.Log();
            }

            LoadLanguage();
            LoadOptions();
            await CheckForUpdateAsync().ConfigureAwait(false);

            //Initial setup if necessary
            if (ContentModel.ContentFolders.Count == 0)
            {
                await ShowOptionsForm(true).ConfigureAwait(false);
            }
            else
            {
                SetupFoldersDropdown(ContentModel.ContentFolders);
                await SetupFolderFromSelection().ConfigureAwait(false);
            }
        }

        private async Task FolderChanged(FolderModel contentFolder)
        {
            if (contentFolder.Name == ProfileSelections.FolderName)
                return;

            contentFolder = comboBoxFolder.SetComboBoxItem((FolderModel folderItem) => string.Equals(folderItem.Name, contentFolder?.Name, StringComparison.OrdinalIgnoreCase));
            ProfileSelections.FolderName = contentFolder?.Name;

            await SetupFolderFromSelection().ConfigureAwait(false);
        }

        private async ValueTask RouteChanged(RouteModelCore routeModel)
        {
            if (routeModel?.Id == ProfileSelections.RouteId)
                return;

            routeModel = comboBoxRoute.SetComboBoxItem((RouteModelCore routeModelItem) => string.Equals(routeModelItem.Name, routeModel?.Name, StringComparison.OrdinalIgnoreCase));

            ProfileSelections.RouteId = routeModel?.Id;
            await SetupRouteFromSelection().ConfigureAwait(false);
        }

        private void ActivityChanged(ActivityModelCore activityModel)
        {
            if (activityModel?.Id == ProfileSelections.ActivityId)
                return;

            activityModel = comboBoxActivity.SetComboBoxItem((ActivityModelCore activityItem) => string.Equals(activityItem.Id, activityModel?.Id, StringComparison.OrdinalIgnoreCase));

            ProfileSelections.ActivityId = activityModel?.Id;
            ProfileSelections.ActivityType = activityModel.ActivityType;
            ProfileSelections.StartTime = activityModel.ActivityType == ActivityType.Activity ? activityModel.StartTime : comboBoxStartTime.Tag != null ? (TimeOnly)comboBoxStartTime.Tag : activityModel.StartTime;
            ProfileSelections.Season = activityModel.ActivityType == ActivityType.Activity ? activityModel.Season : (SeasonType)comboBoxStartSeason.SelectedValue;
            ProfileSelections.Weather = activityModel.ActivityType == ActivityType.Activity ? activityModel.Weather : (WeatherType)comboBoxStartWeather.SelectedValue;
            ProfileSelections.PathId = activityModel.ActivityType == ActivityType.Activity ? activityModel.PathId : (comboBoxHeadTo.SelectedValue as PathModelCore)?.Id;
            ProfileSelections.WagonSetId = activityModel.ActivityType == ActivityType.Activity ? activityModel.ConsistId : (comboBoxConsist.SelectedValue as WagonSetModel)?.Id;

            SetupActivityFromSelection();
        }

        private void LocomotiveChanged(WagonSetModel wagonSetModel, bool any)
        {
            if (!any && wagonSetModel?.Id == ProfileSelections.WagonSetId)
                return;

            if (!any)
            {
                _ = comboBoxLocomotive.SetComboBoxItem((IGrouping<string, WagonSetModel> grouping) => grouping.Key != anyConsist.Name && grouping.Where(w => string.Equals(w.Id, wagonSetModel?.Id, StringComparison.OrdinalIgnoreCase)).Any());
                ProfileSelections.WagonSetId = wagonSetModel?.Id;
            }
            SetupConsistsDropdown();
            _ = comboBoxConsist.SetComboBoxItem((WagonSetModel wagonSetItem) => string.Equals(wagonSetItem.Id, wagonSetModel.Id, StringComparison.OrdinalIgnoreCase));
        }

        private void ConsistChanged(WagonSetModel wagonSetModel)
        {
            if (wagonSetModel?.Id == ProfileSelections.WagonSetId)
                return;

            wagonSetModel = comboBoxConsist.SetComboBoxItem((WagonSetModel wagonSetItem) => string.Equals(wagonSetItem.Id, wagonSetModel?.Id, StringComparison.OrdinalIgnoreCase));
            ProfileSelections.WagonSetId = wagonSetModel?.Id;
            _ = comboBoxLocomotive.SetComboBoxItem((IGrouping<string, WagonSetModel> grouping) => grouping.Key != anyConsist.Name && grouping.Where(w => string.Equals(w.Id, wagonSetModel.Id, StringComparison.OrdinalIgnoreCase)).Any());
        }

        private void PathChanged(PathModelCore pathModel)
        {
            if (pathModel?.Id == ProfileSelections.PathId)
                return;

            pathModel = comboBoxStartAt.SetComboBoxItem((IGrouping<string, PathModelCore> grouping) => grouping.Any(p => p.Id == pathModel?.Id)).FirstOrDefault(p => p.Id == pathModel?.Id);

            ProfileSelections.PathId = pathModel?.Id;

            SetupPathEndDropdown();
            _ = comboBoxHeadTo.SetComboBoxItem((PathModelCore pathModelItem) => string.Equals(pathModelItem.Id, pathModel.Id, StringComparison.OrdinalIgnoreCase));
        }

        private void TimetableSetChanged(TimetableModel timetableModel)
        {
            if (timetableModel?.Id == ProfileSelections.TimetableSet)
                return;

            timetableModel = comboBoxTimetableSet.SetComboBoxItem((TimetableModel timetableItem) => string.Equals(timetableModel?.Id, timetableItem.Id, StringComparison.OrdinalIgnoreCase));
            ProfileSelections.TimetableSet = timetableModel?.Id;
            SetupTimetableFromSelection();
        }

        private void TimetableChanged(IGrouping<string, TimetableTrainModel> timetableTrainModels)
        {
            if (timetableTrainModels?.Key == ProfileSelections.TimetableName)
                return;

            IGrouping<string, TimetableTrainModel> timetable = comboBoxTimetable.SetComboBoxItem((IGrouping<string, TimetableTrainModel> grouping) => string.Equals(grouping.Key, timetableTrainModels?.Key, StringComparison.OrdinalIgnoreCase));

            ProfileSelections.TimetableName = timetableTrainModels.Key;

            SetupTimetableTrainsDropdown();
        }

        private void TimetableTrainChanged(TimetableTrainModel timetableTrainModel)
        {
            if (timetableTrainModel?.Id == ProfileSelections.TimetableTrain)
                return;

            timetableTrainModel = comboBoxTimetableTrain.SetComboBoxItem((TimetableTrainModel timetableTrainItem) => string.Equals(timetableTrainModel?.Id, timetableTrainItem.Id, StringComparison.OrdinalIgnoreCase));

            ProfileSelections.TimetableTrain = timetableTrainModel.Id;
        }

        private void TimetableWeatherChanged(WeatherModelCore weatherModel)
        {
            if (weatherModel?.Id == ProfileSelections.WeatherChanges)
                return;

            weatherModel = comboBoxTimetableWeatherFile.SetComboBoxItem((WeatherModelCore weatherItem) => string.Equals(weatherItem.Id, weatherModel?.Id, StringComparison.OrdinalIgnoreCase));

            ProfileSelections.WeatherChanges = weatherModel?.Id;
        }

        #region setup from selections
        private async Task SetupFolderFromSelection()
        {
            if (InvokeRequired)
            {
                await Invoke(SetupFolderFromSelection).ConfigureAwait(false);
                return;
            }

            FolderModel contentFolder = comboBoxFolder.SetComboBoxItem((FolderModel folderItem) => string.Equals(folderItem.Name, ProfileSelections?.FolderName, StringComparison.OrdinalIgnoreCase));

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

            RouteModelCore routeModel = comboBoxRoute.SetComboBoxItem((RouteModelCore routeModelItem) => string.Equals(routeModelItem.Id, ProfileSelections?.RouteId, StringComparison.OrdinalIgnoreCase));

            ProfileSelections.RouteId = routeModel?.Id;

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

            bool exploreActivity = ProfileSelections != null && (ProfileSelections.ActivityType is ActivityType.ExploreActivity or ActivityType.Explorer);
            bool activity = exploreActivity || (ProfileSelections?.ActivityType is ActivityType.Activity);
            radioButtonModeActivity.Checked = !(radioButtonModeTimetable.Checked = ProfileSelections.ActivityType == ActivityType.TimeTable);

            // values
            _ = comboBoxStartSeason.SetComboBoxItem((ComboBoxItem<SeasonType> cbi) => cbi.Value == ProfileSelections.Season);
            _ = comboBoxStartWeather.SetComboBoxItem((ComboBoxItem<WeatherType> cbi) => cbi.Value == ProfileSelections.Weather);

            comboBoxStartTime.Text = $"{ProfileSelections.StartTime:HH\\:mm\\:ss}";
            comboBoxStartTime.Tag = ProfileSelections.StartTime;

            ActivityModelCore activityModel = null;

            if (activity)
            {
                activityModel = comboBoxActivity.SetComboBoxItem((ActivityModelCore activityItem) => string.Equals(activityItem.Id, ProfileSelections.ActivityId, StringComparison.OrdinalIgnoreCase));
            }
            else if (exploreActivity)
            {
                activityModel = comboBoxActivity.SetComboBoxItem((ActivityModelCore activityItem) => activityItem.ActivityType == ProfileSelections.ActivityType);
            }

            _ = comboBoxLocomotive.SetComboBoxItem((IGrouping<string, WagonSetModel> grouping) => grouping.Key != anyConsist.Name && grouping.Where(w => string.Equals(w.Id, ProfileSelections.WagonSetId, StringComparison.OrdinalIgnoreCase)).Any());
            SetupConsistsDropdown();
            _ = comboBoxConsist.SetComboBoxItem((ComboBoxItem<WagonSetModel> cbi) => string.Equals(cbi.Value.Id, ProfileSelections.WagonSetId, StringComparison.OrdinalIgnoreCase));

            _ = comboBoxStartAt.SetComboBoxItem((IGrouping<string, PathModelCore> grouping) => grouping.Where(p => string.Equals(p.Id, ProfileSelections.PathId, StringComparison.OrdinalIgnoreCase)).Any());
            SetupPathEndDropdown();
            _ = comboBoxHeadTo.SetComboBoxItem((ComboBoxItem<PathModelCore> cbi) => string.Equals(ProfileSelections.PathId, cbi.Value.Id, StringComparison.OrdinalIgnoreCase));

            if (radioButtonModeActivity.Checked)
            {
                ProfileSelections.ActivityId = activityModel?.Id;
                ProfileSelections.ActivityType = activityModel?.ActivityType ?? ActivityType.Explorer;
                ProfileSelections.StartTime = activityModel?.ActivityType == ActivityType.Activity ? activityModel.StartTime : comboBoxStartTime.Tag != null ? (TimeOnly)comboBoxStartTime.Tag : activityModel.StartTime;
                ProfileSelections.Season = activityModel?.ActivityType == ActivityType.Activity ? activityModel.Season : comboBoxStartSeason.SelectedValue!= null ? (SeasonType)comboBoxStartSeason.SelectedValue : ProfileSelections.Season;
                ProfileSelections.Weather = activityModel?.ActivityType == ActivityType.Activity ? activityModel.Weather : comboBoxStartWeather.SelectedValue!= null ? (WeatherType)comboBoxStartWeather.SelectedValue: ProfileSelections.Weather;
                ProfileSelections.PathId = activityModel?.ActivityType == ActivityType.Activity ? activityModel.PathId : (comboBoxHeadTo.SelectedValue as PathModelCore)?.Id;
                ProfileSelections.WagonSetId = activityModel?.ActivityType == ActivityType.Activity ? activityModel.ConsistId : (comboBoxConsist.SelectedValue as WagonSetModel)?.Id;
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
            _ = comboBoxStartSeason.SetComboBoxItem((ComboBoxItem<SeasonType> cbi) => cbi.Value == ProfileSelections.Season);
            _ = comboBoxStartWeather.SetComboBoxItem((ComboBoxItem<WeatherType> cbi) => cbi.Value == ProfileSelections.Weather);

            TimetableModel timetableModel = comboBoxTimetableSet.SetComboBoxItem((TimetableModel timetableItem) => string.Equals(timetableItem.Id, ProfileSelections.TimetableSet, StringComparison.OrdinalIgnoreCase));
            SetupTimetableDropdown();
            IGrouping<string, TimetableTrainModel> timetable = comboBoxTimetable.SetComboBoxItem((IGrouping<string, TimetableTrainModel> grouping) => string.Equals(grouping.Key, ProfileSelections.TimetableName, StringComparison.OrdinalIgnoreCase));
            SetupTimetableTrainsDropdown();
            TimetableTrainModel timetableTrainModel = comboBoxTimetableTrain.SetComboBoxItem((TimetableTrainModel timetableTrainItem) => string.Equals(timetableTrainItem.Id, ProfileSelections.TimetableTrain, StringComparison.OrdinalIgnoreCase));

            WeatherModelCore weatherModel = comboBoxTimetableWeatherFile.SetComboBoxItem((WeatherModelCore weatherItem) => string.Equals(weatherItem.Id, ProfileSelections.WeatherChanges, StringComparison.OrdinalIgnoreCase));
            comboBoxTimetableDay.SelectedIndex = (int)ProfileSelections.TimetableDay;

            if (radioButtonModeTimetable.Checked)
            {
                ProfileSelections.ActivityType = timetableTrainModel != null ? ActivityType.TimeTable : ActivityType.Explorer;
                ProfileSelections.TimetableSet = timetableModel?.Id;
                ProfileSelections.TimetableName = timetable?.Key;
                ProfileSelections.TimetableTrain = timetableTrainModel?.Id;
                ProfileSelections.WeatherChanges = weatherModel?.Id;
            }

            UpdateEnabled();
            ShowDetails();
        }
        #endregion
    }
}
