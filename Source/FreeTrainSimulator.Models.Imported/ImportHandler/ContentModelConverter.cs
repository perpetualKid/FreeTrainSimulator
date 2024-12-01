using System;
using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler.OpenRails;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;

namespace FreeTrainSimulator.Models.Imported.ImportHandler
{
    public static class ContentModelConverter
    {
        public static async Task<ProfileModel> SetupContent(ProfileModel profileModel, bool refresh, IProgress<int> progressClient, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            if (refresh = VersionInfo.Compare(profileModel.Version) > 0 || refresh)
            {
                profileModel = await ProfileModelHandler.Expand(profileModel, cancellationToken).ConfigureAwait(false);

                int folderCount = profileModel.ContentFolders.Count;
                int completedCount = 0;
                await Parallel.ForEachAsync(profileModel.ContentFolders, async (folderModel, cancellationToken) =>
                {
                    await ConvertContent(folderModel, refresh, cancellationToken).ConfigureAwait(false);
                    Interlocked.Increment(ref completedCount);
                    progressClient?.Report(completedCount * 100 / folderCount);
                }).ConfigureAwait(false);
            }
            return profileModel;
        }

        public static async Task<ProfileModel> ConvertContent(ProfileModel profileModel, bool refresh, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            if (refresh = VersionInfo.Compare(profileModel.Version) > 0 || refresh)
            {
                profileModel = await ProfileModelHandler.Expand(profileModel, cancellationToken).ConfigureAwait(false);

                await Parallel.ForEachAsync(profileModel.ContentFolders, async (folderModel, cancellationToken) =>
                {
                    await ConvertContent(folderModel, refresh, cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            return profileModel;
        }

        public static async Task<FolderModel> ConvertContent(FolderModel folderModel, bool refresh, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            if (VersionInfo.Compare(folderModel.Version) > 0 || refresh)
            {
                Task<FrozenSet<RouteModelCore>> routesTask = RouteModelHandler.ExpandRouteModels(folderModel, cancellationToken);
                Task<FrozenSet<WagonSetModel>> wagonSetsTask = WagonSetModelHandler.ExpandWagonSetModels(folderModel, cancellationToken);

                await Task.WhenAll(wagonSetsTask, routesTask).ConfigureAwait(false);

#pragma warning disable CA1849 // Call async methods when in an async method
                await Parallel.ForEachAsync(routesTask.Result, async (routeModel, cancellationToken) =>
#pragma warning restore CA1849 // Call async methods when in an async method
                {
                    await ConvertContent(routeModel, refresh, cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            return folderModel;
        }

        public static async Task<RouteModelCore> ConvertContent(RouteModelCore routeModel, bool refresh, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            if (VersionInfo.Compare(routeModel.Version) > 0 || refresh)
            {
                await Task.WhenAll(
                    PathModelHandler.ExpandPathModels(routeModel, cancellationToken),
                    ActivityModelHandler.ExpandActivityModels(routeModel, cancellationToken),
                    TimetableModelHandler.ExpandTimetableModels(routeModel, cancellationToken),
                    WeatherModelHandler.ExpandPathModels(routeModel, cancellationToken)
                    ).ConfigureAwait(false);
            }

            return routeModel;
        }
    }
}
