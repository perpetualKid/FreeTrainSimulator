using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Track;

namespace FreeTrainSimulator.Models.Shim
{
    public static class TrackSectionModelExtensions
    {
        public static string GlobalTrackSectionId(int version) => $"{version:D5}";

        public static Task<bool> ContainsTrackSectionVersion(this ContentModel _, int version) => GlobalTrackSectionModelHandler.Contains(GlobalTrackSectionId(version), CancellationToken.None);
        public static Task<ImmutableArray<GlobalTrackSectionModel>> GetTrackSectionModels(this ContentModel _, CancellationToken cancellationToken) => GlobalTrackSectionModelHandler.GetTrackSectionModels(cancellationToken);

        public static async ValueTask<GlobalTrackSectionModel> Get(this RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            GlobalTrackSectionModel trackSectionModel = await GlobalTrackSectionModelHandler.GetGlobal(cancellationToken);

            return null;
        }
    }
}
