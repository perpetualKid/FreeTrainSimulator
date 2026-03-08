using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Models.Handler
{
    internal class TrackModelHandler : ContentHandlerBase<TrackModel>
    {
        public static Task<TrackModel> GetCore(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Id;

            if (!modelTaskCache.TryGetValue(key, out Task<TrackModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(key, routeModel, cancellationToken);
            }

            return modelTask;
        }
    }
}
