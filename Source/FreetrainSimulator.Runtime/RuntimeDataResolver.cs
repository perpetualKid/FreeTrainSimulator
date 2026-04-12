using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Runtime
{
    public class RuntimeDataResolver
    {
        public RouteModel RouteData { get; }
        public TrackSectionModel TrackSections { get; }
        public SignalConfigurationModel SignalConfiguration { get; }
        public Track.TrackWorld TrackWorld { get; }
        public bool MetricUnits { get; }
        public IRuntimeReferenceResolver RuntimeReferenceResolver { get; }

        public static RuntimeDataResolver Instance => GameService<RuntimeDataResolver>.Instance;

        public static RuntimeDataResolver GameInstance(Game game) => GameService<RuntimeDataResolver>.Get(game);

        public static async Task Initialize(RouteModel route, bool metricUnits, IRuntimeReferenceResolver runtimeReferenceResolver = null)
        {
            TrackSectionModel trackSectionModel = await route.GetTrackSectionModel(CancellationToken.None).ConfigureAwait(false);
            TrackModel trackModel = await route.GetTrackModel(CancellationToken.None).ConfigureAwait(false);
            SignalConfigurationModel signalConfigurationModel = await route.GetSignalConfigurationModel(CancellationToken.None).ConfigureAwait(false);

            Track.TrackWorld trackWorld = Track.TrackWorld.Initialize(null, trackModel, trackSectionModel);

            _ = GameService<RuntimeDataResolver>.Set(null, new RuntimeDataResolver(route, trackSectionModel, signalConfigurationModel, trackWorld, metricUnits, runtimeReferenceResolver));
        }

        protected RuntimeDataResolver(RouteModel route, TrackSectionModel trackSectionModel, SignalConfigurationModel signalConfiguration,
            Track.TrackWorld trackWorld, bool useMetricUnits, IRuntimeReferenceResolver runtimeReferenceResolver)
        {
            RouteData = route;
            TrackSections = trackSectionModel;
            SignalConfiguration = signalConfiguration;
            TrackWorld = trackWorld;
            MetricUnits = useMetricUnits;
            RuntimeReferenceResolver = runtimeReferenceResolver;
        }

        protected RuntimeDataResolver()
        { }
    }
}
