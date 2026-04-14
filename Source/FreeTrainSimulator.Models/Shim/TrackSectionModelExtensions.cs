using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Models.Shim
{
    /// <summary>
    /// Extension methods for loading the <see cref="TrackSectionModel"/> (track sections, shapes,
    /// and shape-path definitions derived from the legacy MSTS <c>tsection.dat</c>) for a given route.
    /// </summary>
    public static class TrackSectionModelExtensions
    {
        public static async ValueTask<TrackSectionModel> Get(this RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return await TrackSectionsModelHandler.GetCore(routeModel, cancellationToken).ConfigureAwait(false);
        }
    }
}
