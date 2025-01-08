using System;
using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Models.Shim
{
    public static class RouteModelExtensions
    {
        public static ValueTask<RouteModel> Extend(this RouteModelCore routeModel, CancellationToken cancellationToken) => RouteModelHandler.GetExtended(routeModel, cancellationToken);
        public static Task<FrozenSet<PathModelCore>> GetPaths(this RouteModelCore routeModel, CancellationToken cancellationToken) => routeModel.GetRoutePaths(cancellationToken);
        public static Task<FrozenSet<ActivityModelCore>> GetActivities(this RouteModelCore routeModel, CancellationToken cancellationToken) => routeModel.GetRouteActivities(cancellationToken);
        public static Task<FrozenSet<TimetableModel>> GetTimetables(this RouteModelCore routeModel, CancellationToken cancellationToken) => TimetableModelHandler.GetTimetables(routeModel, cancellationToken);
        public static Task<FrozenSet<WeatherModelCore>> GetWeatherFiles(this RouteModelCore routeModel, CancellationToken cancellationToken) => WeatherModelHandler.GetWeatherFiles(routeModel, cancellationToken);

        public static async ValueTask<PathModelCore> PathModel(this RouteModelCore routeModel, string pathName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentException.ThrowIfNullOrEmpty(pathName, nameof(pathName));

            return await PathModelHandler.GetCore(pathName, routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static FrozenSet<PathModelCore> GetPaths(this RouteModelCore routeModel) => Task.Run(async () => await routeModel.GetPaths(CancellationToken.None).ConfigureAwait(false)).Result;

    }
}
