using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Models.Shim
{
    public static class TrackSectionModelExtensions
    {
        public static async ValueTask<TrackSectionModel> Get(this RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            return await TrackSectionsModelHandler.GetCore(routeModel, cancellationToken).ConfigureAwait(false);
        }
    }
}
