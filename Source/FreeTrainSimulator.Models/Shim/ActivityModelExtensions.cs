using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    public static class ActivityModelExtensions
    {
        public static Task<ActivityModelCore> Get(this RouteModelCore routeModel, string activityId, CancellationToken cancellationToken) => ActivityModelHandler.GetCore(activityId, routeModel, cancellationToken);
        public static ValueTask<ActivityModel> GetExtended(this RouteModelCore routeModel, string activityId, CancellationToken cancellationToken) => ActivityModelHandler.GetExtended(activityId, routeModel, cancellationToken);
        public static ValueTask<ActivityModel> GetExtended(this ActivityModelCore activityModel, CancellationToken cancellationToken) => ActivityModelHandler.GetExtended(activityModel, cancellationToken);
        public static Task<ImmutableArray<ActivityModelCore>> GetRouteActivities(this RouteModelCore routeModel, CancellationToken cancellationToken) => ActivityModelHandler.GetActivities(routeModel, cancellationToken);
        public static Task<ImmutableArray<ActivityModelCore>> LoadTestActivities(this ContentModel contentModel, CancellationToken cancellationToken) => TestActivityModelHandler.GetTestActivities(contentModel, cancellationToken);
    }
}
