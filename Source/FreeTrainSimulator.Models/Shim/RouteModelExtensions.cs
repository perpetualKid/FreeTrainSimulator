using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    public static class RouteModelExtensions
    {
        public static ValueTask<RouteModel> GetExtended(this RouteModelHeader routeModel, CancellationToken cancellationToken) => RouteModelHandler.GetExtended(routeModel, cancellationToken);
        public static Task<ImmutableArray<PathModelHeader>> GetPaths(this RouteModelHeader routeModel, CancellationToken cancellationToken) => routeModel.GetRoutePaths(cancellationToken);
        public static Task<ImmutableArray<ActivityModelHeader>> GetActivities(this RouteModelHeader routeModel, CancellationToken cancellationToken) => routeModel.GetRouteActivities(cancellationToken);
        public static Task<ImmutableArray<TimetableModel>> GetTimetables(this RouteModelHeader routeModel, CancellationToken cancellationToken) => TimetableModelHandler.GetTimetables(routeModel, cancellationToken);
        public static Task<ImmutableArray<WeatherModelHeader>> GetWeatherFiles(this RouteModelHeader routeModel, CancellationToken cancellationToken) => WeatherModelHandler.GetWeatherFiles(routeModel, cancellationToken);

        public static async ValueTask<ActivityModel> ActivityModel(this RouteModelHeader routeModel, string activityId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentException.ThrowIfNullOrEmpty(activityId, nameof(activityId));

            return await ActivityModelHandler.GetExtended(activityId, routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<PathModel> PathModel(this RouteModelHeader routeModel, string pathId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentException.ThrowIfNullOrEmpty(pathId, nameof(pathId));

            return await PathModelHandler.GetExtended(pathId, routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<TimetableModel> TimetableModel(this RouteModelHeader routeModel, string timetableId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentException.ThrowIfNullOrEmpty(timetableId, nameof(timetableId));

            return await TimetableModelHandler.GetCore(timetableId, routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static ImmutableArray<PathModelHeader> GetPaths(this RouteModelHeader routeModel) => Task.Run(async () => await routeModel.GetPaths(CancellationToken.None).ConfigureAwait(false)).Result;

        public static string SavePointName(this RouteModelHeader routeModelHeader, ActivityType activityType)
        {
            ArgumentNullException.ThrowIfNull(routeModelHeader, nameof(routeModelHeader));
            return activityType == ActivityType.ExploreActivity ? $"ea${routeModelHeader.Id}$" : routeModelHeader.Id;
        }

        public static string SavePointName(this RouteModelHeader routeModelHeader, ActivityModelHeader activityModel)
        {
            ArgumentNullException.ThrowIfNull(routeModelHeader, nameof(routeModelHeader));
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));
            return $"{routeModelHeader.Id} {activityModel.Id}";
        }

        public static string SavePointName(this RouteModelHeader routeModelHeader, TimetableModel timetableModel)
        {
            ArgumentNullException.ThrowIfNull(routeModelHeader, nameof(routeModelHeader));
            ArgumentNullException.ThrowIfNull(timetableModel, nameof(timetableModel));
            return $"{routeModelHeader.Id} {timetableModel.Id}";
        }

        public static Task<PathModel> Save(this RouteModelHeader routeModel, PathModel pathModel)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));

            pathModel.Initialize(routeModel);
            return PathModelHandler.ToFile(pathModel, CancellationToken.None);
        }
    }
}
