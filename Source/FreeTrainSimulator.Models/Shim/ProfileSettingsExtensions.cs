using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Settings;

namespace FreeTrainSimulator.Models.Shim
{
    public static class ProfileSettingsExtensions
    {
        #region settings
        public static Task<T> LoadSettingsModel<T>(this ProfileModel profileModel, CancellationToken cancellationToken) where T : ProfileSettingsModelBase, new()
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            T settingsModel = new T() { Id = profileModel.Name, Name = profileModel.Name };
            settingsModel.RefreshModel();
            settingsModel.Initialize(profileModel);

            return ProfileSettingModelHandler<T>.FromFile(settingsModel, cancellationToken);
        }

        public static Task<T> UpdateSettingsModel<T>(this ProfileModel profileModel, T settingsModel, CancellationToken cancellationToken) where T : ProfileSettingsModelBase
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            ArgumentNullException.ThrowIfNull(settingsModel, nameof(settingsModel));

            settingsModel.Initialize(profileModel);
            return ProfileSettingModelHandler<T>.ToFile(settingsModel, cancellationToken);
        }
        #endregion

        public static async ValueTask<FolderModel> SelectedFolder(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));
            ContentModel content = await ContentModelExtensions.Get(null, cancellationToken).ConfigureAwait(false);
            return content.ContentFolders.GetByName(profileSelections.FolderName);
        }

        public static async ValueTask<RouteModelCore> SelectedRoute(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));

            FolderModel contentFolder = await profileSelections.SelectedFolder(cancellationToken).ConfigureAwait(false);
            return null == contentFolder
                ? null
                : (await contentFolder.GetRoutes(cancellationToken).ConfigureAwait(false)).GetById(profileSelections.RouteId);
        }

        public static RouteModelCore SelectedRoute(this ProfileSelectionsModel profileSelections)
        {
            return Task.Run(async () => await profileSelections.SelectedRoute(CancellationToken.None).ConfigureAwait(false)).Result;
        }

        public static async ValueTask<ActivityModelCore> SelectedActivity(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));

            if (profileSelections.ActivityType != Common.ActivityType.Activity)
                return null;

            RouteModelCore routeModel = await profileSelections.SelectedRoute(cancellationToken).ConfigureAwait(false);
            return null == routeModel
                ? null
                : (await routeModel.GetActivities(cancellationToken).ConfigureAwait(false)).GetById(profileSelections.ActivityId);
        }

        public static ActivityModelCore SelectedActivity(this ProfileSelectionsModel profileSelections)
        {
            return Task.Run(async () => await profileSelections.SelectedActivity(CancellationToken.None).ConfigureAwait(false)).Result;
        }

        public static async ValueTask<PathModelCore> SelectedPath(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));

            if (profileSelections.ActivityType is not (Common.ActivityType.ExploreActivity or Common.ActivityType.Explorer))
                return null;

            RouteModelCore routeModel = await profileSelections.SelectedRoute(cancellationToken).ConfigureAwait(false);
            return null == routeModel
                ? null
                : (await routeModel.GetPaths(cancellationToken).ConfigureAwait(false)).GetById(profileSelections.PathId);
        }

        public static PathModelCore SelectedPath(this ProfileSelectionsModel profileSelections)
        {
            return Task.Run(async () => await profileSelections.SelectedPath(CancellationToken.None).ConfigureAwait(false)).Result;
        }

        public static async ValueTask<WagonSetModel> SelectedWagonSet(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));

            if (profileSelections.ActivityType is not (Common.ActivityType.ExploreActivity or Common.ActivityType.Explorer))
                return null;

            FolderModel contentFolder = await profileSelections.SelectedFolder(cancellationToken).ConfigureAwait(false);
            return null == contentFolder
                ? null
                : (await contentFolder.GetWagonSets(cancellationToken).ConfigureAwait(false)).GetById(profileSelections.WagonSetId);
        }

        public static WagonSetModel SelectedWagonSet(this ProfileSelectionsModel profileSelections)
        {
            return Task.Run(async () => await profileSelections.SelectedWagonSet(CancellationToken.None).ConfigureAwait(false)).Result;
        }

        public static async ValueTask<WeatherModelCore> SelectedWeatherChangesModel(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));

            RouteModelCore routeModel = await profileSelections.SelectedRoute(cancellationToken).ConfigureAwait(false);
            return null == routeModel
                ? null
                : (await routeModel.GetWeatherFiles(cancellationToken).ConfigureAwait(false)).GetById(profileSelections.WeatherChanges);
        }

        public static WeatherModelCore SelectedWeatherChangesModel(this ProfileSelectionsModel profileSelections)
        {
            return Task.Run(async () => await profileSelections.SelectedWeatherChangesModel(CancellationToken.None).ConfigureAwait(false)).Result;
        }

        public static async ValueTask<TimetableModel> SelectedTimetable(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));

            RouteModelCore routeModel = await profileSelections.SelectedRoute(cancellationToken).ConfigureAwait(false);
            return null == routeModel
                ? null
                : (await routeModel.GetTimetables(cancellationToken).ConfigureAwait(false)).GetById(profileSelections.TimetableSet);
        }

        public static TimetableModel SelectedTimetable(this ProfileSelectionsModel profileSelections)
        {
            return Task.Run(async () => await profileSelections.SelectedTimetable(CancellationToken.None).ConfigureAwait(false)).Result;
        }

        public static async ValueTask<TimetableTrainModel> SelectedTimetableTrain(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));

            TimetableModel timetableModel = await profileSelections.SelectedTimetable(cancellationToken).ConfigureAwait(false);
            return timetableModel?.TimetableTrains.GetById(profileSelections.TimetableTrain);
        }

        public static TimetableTrainModel SelectedTimetableTrain(this ProfileSelectionsModel profileSelections)
        {
            return Task.Run(async () => await profileSelections.SelectedTimetableTrain(CancellationToken.None).ConfigureAwait(false)).Result;
        }
    }
}
