using System.Collections.Immutable;
using System.Linq;
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

        public static string TrackSectionIdEmpty { get; } = GlobalTrackSectionId(0);

        public static Task<bool> ContainsTrackSectionVersion(this ContentModel _, int version) => GlobalTrackSectionModelHandler.Contains(GlobalTrackSectionId(version), CancellationToken.None);

        public static async Task<ImmutableArray<GlobalTrackSectionModel>> GetTrackSectionModels(this FolderModel folderModel, CancellationToken cancellationToken) => 
            (await GlobalTrackSectionModelHandler.GetTrackSectionModels(cancellationToken).ConfigureAwait(false)).Where(t => t.Parent == null || t.Parent.Id == folderModel.Id).ToImmutableArray();

        public static async ValueTask<GlobalTrackSectionModel> Get(this RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            GlobalTrackSectionModel trackSectionModel = await GlobalTrackSectionModelHandler.GetGlobal(cancellationToken).ConfigureAwait(false);

            return null;
        }
    }
}
