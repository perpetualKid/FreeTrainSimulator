using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Runtime
{
    public class RuntimeDataResolver
    {
        public RouteModel RouteData { get; }
        public TrackSectionModel TrackSections { get; }
        public TrackModel TrackModel { get; }
        public Track.TrackWorld TrackWorld { get; }
        public bool MetricUnits { get; }
        public IRuntimeReferenceResolver RuntimeReferenceResolver { get; }

        public static RuntimeDataResolver Instance => GameService<RuntimeDataResolver>.Instance;

        public static RuntimeDataResolver GameInstance(Game game) => GameService<RuntimeDataResolver>.Get(game);

        public static async Task Initialize(RouteModel route, bool metricUnits, IRuntimeReferenceResolver runtimeReferenceResolver = null)
        {
            TrackSectionModel trackSectionModel = await route.GetTrackSectionModel(CancellationToken.None).ConfigureAwait(false);
            TrackModel trackModel = await route.GetTrackModel(CancellationToken.None).ConfigureAwait(false);

            Track.TrackWorld trackWorld = Track.TrackWorld.Initialize(null, trackModel);

            _ = GameService<RuntimeDataResolver>.Set(null, new RuntimeDataResolver(route, trackSectionModel, trackModel, trackWorld, metricUnits, runtimeReferenceResolver));
        }

        protected RuntimeDataResolver(RouteModel route, TrackSectionModel trackSectionModel, TrackModel trackModel, Track.TrackWorld trackWorld,
            bool useMetricUnits, IRuntimeReferenceResolver runtimeReferenceResolver)
        {
            RouteData = route;
            TrackSections = trackSectionModel;
            TrackModel = trackModel;
            TrackWorld = trackWorld;
            MetricUnits = useMetricUnits;
            RuntimeReferenceResolver = runtimeReferenceResolver;
        }

        protected RuntimeDataResolver()
        { }
    }
}
