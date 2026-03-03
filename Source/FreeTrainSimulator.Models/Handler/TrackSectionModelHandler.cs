using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Models.Handler
{
    internal class TrackSectionModelHandler : ContentHandlerBase<TrackSectionModel>
    {
        public static Task<TrackSectionModel> GetCore(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            string key = routeModel.Hierarchy();

            if (!modelTaskCache.TryGetValue(key, out Task<TrackSectionModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile<TrackSectionModel>(key, null, cancellationToken);
            }

            return modelTask;
        }
    }
}
