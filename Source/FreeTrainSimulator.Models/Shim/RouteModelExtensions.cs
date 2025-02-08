using System;
using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    public static class RouteModelExtensions
    {
        public static ValueTask<RouteModel> Extend(this RouteModelCore routeModel, CancellationToken cancellationToken) => RouteModelHandler.GetExtended(routeModel, cancellationToken);
        public static Task<FrozenSet<PathModelCore>> GetPaths(this RouteModelCore routeModel, CancellationToken cancellationToken) => routeModel.GetRoutePaths(cancellationToken);
        public static Task<FrozenSet<ActivityModelCore>> GetActivities(this RouteModelCore routeModel, CancellationToken cancellationToken) => routeModel.GetRouteActivities(cancellationToken);
        public static Task<FrozenSet<TimetableModel>> GetTimetables(this RouteModelCore routeModel, CancellationToken cancellationToken) => TimetableModelHandler.GetTimetables(routeModel, cancellationToken);
        public static Task<FrozenSet<WeatherModelCore>> GetWeatherFiles(this RouteModelCore routeModel, CancellationToken cancellationToken) => WeatherModelHandler.GetWeatherFiles(routeModel, cancellationToken);

        public static async ValueTask<ActivityModel> ActivityModel(this RouteModelCore routeModel, string activityId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentException.ThrowIfNullOrEmpty(activityId, nameof(activityId));

            return await ActivityModelHandler.GetExtended(activityId, routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<PathModel> PathModel(this RouteModelCore routeModel, string pathId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentException.ThrowIfNullOrEmpty(pathId, nameof(pathId));

            return await PathModelHandler.GetExtended(pathId, routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<TimetableModel> TimetableModel(this RouteModelCore routeModel, string timetableId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentException.ThrowIfNullOrEmpty(timetableId, nameof(timetableId));

            return await TimetableModelHandler.GetCore(timetableId, routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static FrozenSet<PathModelCore> GetPaths(this RouteModelCore routeModel) => Task.Run(async () => await routeModel.GetPaths(CancellationToken.None).ConfigureAwait(false)).Result;


        public static string SavePointName(this RouteModelCore routeModelCore, ActivityType activityType)
        {
            ArgumentNullException.ThrowIfNull(routeModelCore, nameof(routeModelCore));
            return activityType == ActivityType.ExploreActivity ? $"ea${routeModelCore.Id}$" : routeModelCore.Id;
        }

        public static string SavePointName(this RouteModelCore routeModelCore, ActivityModelCore activityModel)
        {
            ArgumentNullException.ThrowIfNull(routeModelCore, nameof(routeModelCore));
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));
            return $"{routeModelCore.Id} {activityModel.Id}";
        }

        public static string SavePointName(this RouteModelCore routeModelCore, TimetableModel timetableModel)
        {
            ArgumentNullException.ThrowIfNull(routeModelCore, nameof(routeModelCore));
            ArgumentNullException.ThrowIfNull(timetableModel, nameof(timetableModel));
            return $"{routeModelCore.Id} {timetableModel.Id}";
        }
    }
}
