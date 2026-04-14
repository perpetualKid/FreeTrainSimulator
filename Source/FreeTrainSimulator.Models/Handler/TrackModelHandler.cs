using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Models.Handler
{
    /// <summary>
    /// Handler for the <see cref="TrackModel"/>, which contains the pre-built track database
    /// (nodes, items, and geometry) for a route (derived from the legacy MSTS <c>.tdb</c> /
    /// <c>.rdb</c> files).
    /// </summary>
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
