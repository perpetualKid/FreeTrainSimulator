using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Models.Handler
{
    /// <summary>
    /// Handler for the <see cref="TrackSectionModel"/>, which contains track section geometry,
    /// shape definitions, and shape paths for a route (derived from the legacy MSTS
    /// <c>tsection.dat</c> file).
    /// </summary>
    internal class TrackSectionsModelHandler : ContentHandlerBase<TrackSectionModel>
    {
        public static Task<TrackSectionModel> GetCore(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Id;

            if (!modelTaskCache.TryGetValue(key, out Task<TrackSectionModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(key, routeModel, cancellationToken);
            }

            return modelTask;
        }
    }
}
