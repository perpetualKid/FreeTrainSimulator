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
        public bool MetricUnits { get; }
        public IRuntimeReferenceResolver RuntimeReferenceResolver { get; }

        public static RuntimeDataResolver Instance { get; private set; }

        public static RuntimeDataResolver GameInstance(Game game)
        {
            return game?.Services.GetService<RuntimeDataResolver>() ?? Instance;
        }

        public static async Task Initialize(RouteModel route, bool metricUnits, IRuntimeReferenceResolver runtimeReferenceResolver = null)
        {
            TrackSectionModel trackSectionModel = await route.GetTrackSectionModel(CancellationToken.None).ConfigureAwait(false);
            TrackModel trackModel = await route.GetTrackModel(CancellationToken.None).ConfigureAwait(false);

            Instance = new RuntimeDataResolver(route, trackSectionModel, trackModel, metricUnits, runtimeReferenceResolver);
        }

        protected RuntimeDataResolver(RouteModel route, TrackSectionModel trackSectionModel, TrackModel trackModel,
            bool useMetricUnits, IRuntimeReferenceResolver runtimeReferenceResolver)
        {
            RouteData = route;
            TrackSections = trackSectionModel;
            TrackModel = trackModel;
            MetricUnits = useMetricUnits;
            RuntimeReferenceResolver = runtimeReferenceResolver;
        }

        protected RuntimeDataResolver()
        { }
    }
}
