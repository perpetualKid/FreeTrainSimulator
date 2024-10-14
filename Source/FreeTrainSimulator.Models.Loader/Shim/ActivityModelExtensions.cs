using System;
using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class ActivityModelExtensions
    {
        public static ValueTask<ActivityModelCore> Get(this RouteModelCore routeModel, string activityId, CancellationToken cancellationToken) => ActivityModelHandler.GetCore(activityId, routeModel, cancellationToken);
        public static ValueTask<ActivityModel> GetExtended(this RouteModelCore routeModel, string activityId, CancellationToken cancellationToken) => ActivityModelHandler.GetExtended(activityId, routeModel, cancellationToken);
        public static ValueTask<ActivityModel> GetExtended(this ActivityModelCore activityModel, CancellationToken cancellationToken) => ActivityModelHandler.GetExtended(activityModel, cancellationToken);
        public static ValueTask<FrozenSet<PathModelCore>> GetRouteActivities(this RouteModelCore routeModel, CancellationToken cancellationToken) => PathModelHandler.GetPaths(routeModel, cancellationToken);

        public static async ValueTask<FrozenSet<TestActivityModel>> LoadTestActivities(this ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            return await TestActivityModelHandler.GetTestActivities(profileModel, cancellationToken).ConfigureAwait(false);
        }
    }
}
