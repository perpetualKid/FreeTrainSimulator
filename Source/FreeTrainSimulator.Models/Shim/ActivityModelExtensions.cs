using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    public static class ActivityModelExtensions
    {
        public static Task<ActivityModelHeader> Get(this RouteModelHeader routeModel, string activityId, CancellationToken cancellationToken) => ActivityModelHandler.GetCore(activityId, routeModel, cancellationToken);
        public static ValueTask<ActivityModel> GetExtended(this RouteModelHeader routeModel, string activityId, CancellationToken cancellationToken) => ActivityModelHandler.GetExtended(activityId, routeModel, cancellationToken);
        public static ValueTask<ActivityModel> GetExtended(this ActivityModelHeader activityModel, CancellationToken cancellationToken) => ActivityModelHandler.GetExtended(activityModel, cancellationToken);
        public static Task<ImmutableArray<ActivityModelHeader>> GetRouteActivities(this RouteModelHeader routeModel, CancellationToken cancellationToken) => ActivityModelHandler.GetActivities(routeModel, cancellationToken);
        public static Task<ImmutableArray<ActivityModelHeader>> LoadTestActivities(this ContentModel contentModel, CancellationToken cancellationToken) => TestActivityModelHandler.GetTestActivities(contentModel, cancellationToken);
    }
}
