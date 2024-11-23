using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Independent.Settings;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class ProfileSelectionExtensions
    {
        public static async ValueTask<FolderModel> SelectedFolder(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));

            return (await profileSelections.Parent.GetFolders(cancellationToken).ConfigureAwait(false)).GetByName(profileSelections.FolderName);
        }

        public static async ValueTask<RouteModelCore> SelectedRoute(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));

            FolderModel contentFolder = await profileSelections.SelectedFolder(cancellationToken).ConfigureAwait(false);
            return null == contentFolder
                ? null
                : (await contentFolder.GetRoutes(cancellationToken).ConfigureAwait(false)).GetById(profileSelections.RouteId);
        }

        public static async ValueTask<ActivityModelCore> SelectedActivity(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));

            if (profileSelections.ActivityType != Common.ActivityType.Activity)
                return null;

            RouteModelCore routeModel = (await profileSelections.SelectedRoute(cancellationToken).ConfigureAwait(false));
            return null == routeModel
                ? null
                : (await routeModel.GetActivities(cancellationToken).ConfigureAwait(false)).GetById(profileSelections.ActivityId);
        }

        public static async ValueTask<PathModelCore> SelectedPath(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));

            if (profileSelections.ActivityType is not (Common.ActivityType.ExploreActivity or Common.ActivityType.Explorer))
                return null;

            RouteModelCore routeModel = (await profileSelections.SelectedRoute(cancellationToken).ConfigureAwait(false));
            return null == routeModel
                ? null
                : (await routeModel.GetPaths(cancellationToken).ConfigureAwait(false)).GetById(profileSelections.PathId);
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

        public static async Task<WeatherModelCore> WeatherChangesModel(this ProfileSelectionsModel profileSelections, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileSelections, nameof(profileSelections));

            RouteModelCore routeModel = (await profileSelections.SelectedRoute(cancellationToken).ConfigureAwait(false));
            return null == routeModel
                ? null
                : (await routeModel.GetWeatherFiles(cancellationToken).ConfigureAwait(false)).GetById(profileSelections.WeatherChanges);
        }
    }
}
