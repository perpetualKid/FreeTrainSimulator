using System;
using System.Collections.Immutable;
using System.Diagnostics;
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
        public static async Task<ContentModel> SetupContent(ContentModel contentModel, bool refresh, IProgress<int> progressClient, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            if (refresh = VersionInfo.Compare(contentModel.Version) > 0 || refresh)
            {
                contentModel = await ContentModelImportHandler.ExpandContentModel(contentModel, cancellationToken).ConfigureAwait(false);

                int folderCount = contentModel.ContentFolders.Length;
                int completedCount = 0;
                await Parallel.ForEachAsync(contentModel.ContentFolders, async (folderModel, cancellationToken) =>
                {
                    _ = await ConvertContent(folderModel, refresh, cancellationToken).ConfigureAwait(false);
                    _ = Interlocked.Increment(ref completedCount);
                    progressClient?.Report(completedCount * 100 / folderCount);
                }).ConfigureAwait(false);
            }
            return contentModel;
        }

        public static async Task<ContentModel> ConvertContent(ContentModel contentModel, bool refresh, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            if (refresh = VersionInfo.Compare(contentModel.Version) > 0 || refresh)
            {
                contentModel = await ContentModelImportHandler.ExpandContentModel(contentModel, cancellationToken).ConfigureAwait(false);

                await Parallel.ForEachAsync(contentModel.ContentFolders, async (folderModel, cancellationToken) =>
                {
                    _ = await ConvertContent(folderModel, refresh, cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            return contentModel;
        }

        private static async Task<FolderModel> ConvertContent(FolderModel folderModel, bool refresh, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));

            if (VersionInfo.Compare(folderModel.Version) > 0 || refresh)
            {
                Task<ImmutableArray<RouteModelHeader>> routesTask =
                    RouteModelImportHandler.ExpandRouteModels(folderModel, cancellationToken);
                Task<ImmutableArray<WagonSetModel>> wagonSetsTask =
                    WagonSetModelImportHandler.ExpandWagonSetModels(folderModel, cancellationToken);

                await Task.WhenAll(wagonSetsTask, routesTask).ConfigureAwait(false);

                ImmutableArray<RouteModelHeader> routes = await routesTask.ConfigureAwait(false);

                await Parallel.ForEachAsync(routes, cancellationToken, async (routeModel, iterationCancellationToken) =>
                {
                    _ = await ConvertContent(routeModel, refresh, iterationCancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }

            return folderModel;
        }

        private static async Task<RouteModelHeader> ConvertContent(RouteModelHeader routeModel, bool refresh, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            bool modelVersionUpgradeRequired = VersionInfo.Compare(routeModel.Version) > 0;
            if (!modelVersionUpgradeRequired && !refresh)
                return routeModel;

            bool sourceBacked = RouteModelImportHandler.IsSourceBackedRoute(routeModel);

            // Routes without a resolvable MSTS source cannot be re-imported. Migrate them in place only when
            // the stored model version is behind the application version; otherwise preserve them untouched.
            if (!sourceBacked || !RouteModelImportHandler.HasResolvableSourceRoute(routeModel))
            {
                if (modelVersionUpgradeRequired)
                {
                    Trace.TraceInformation($"Migrating persisted route '{routeModel.Id}' in place due to version upgrade.");
                    return await RouteModelImportHandler.RefreshPersistedRouteModel(routeModel, cancellationToken).ConfigureAwait(false);
                }

                if (sourceBacked)
                    Trace.TraceWarning($"Preserving source-backed route '{routeModel.Id}' although its MSTS source is unavailable; version is already current.");
                return routeModel;
            }

            await Task.WhenAll(
                TrackSectionModelImportHandler.ExpandTrackSectionModel(routeModel, cancellationToken),
                PathModelImportHandler.ExpandPathModels(routeModel, cancellationToken),
                ActivityModelImportHandler.ExpandActivityModels(routeModel, cancellationToken),
                TimetableModelHandler.ExpandTimetableModels(routeModel, cancellationToken),
                WeatherModelHandler.ExpandPathModels(routeModel, cancellationToken),
                SignalConfigurationModelImportHandler.ExpandSignalConfigurationModel(routeModel, cancellationToken)
                ).ConfigureAwait(false);
            // Expanding track model needs the track sections and signal config expanded before
            await Task.WhenAll(
                TrackModelImportHandler.ExpandTrackModel(routeModel, cancellationToken)
                ).ConfigureAwait(false);

            return routeModel;
        }
    }
}
