using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.Formats.Msts
{
    public class RuntimeData
    {
        public RouteModel RouteData { get; }
        public TrackSectionModel TrackSections { get; init; }
        /// <summary>Track database, public such that other classes have access as well</summary>
        public TrackDB TrackDB { get; }
        /// <summary>Road track database</summary>
        public RoadTrackDB RoadTrackDB { get; }
        /// <summary>The signal config file containing i.e. the information to distinguish normal and non-normal signals</summary>
        public SignalConfigurationFile SignalConfigFile { get; }
        public bool MetricUnits { get; }
        public IRuntimeReferenceResolver RuntimeReferenceResolver { get; }

        public static RuntimeData Instance { get; private set; }

        public static RuntimeData GameInstance(Game game)
        {
            return game?.Services.GetService<RuntimeData>() ?? Instance;
        }

        public static void Initialize(RouteModel route, TrackDB trackDb, RoadTrackDB roadTrackDB, SignalConfigurationFile signalConfig, bool metricUnits, IRuntimeReferenceResolver runtimeReferenceResolver = null)
        {
            TrackSectionModel trackSectionModel = null;

            Task.Run(async () =>
            {
                trackSectionModel = await route.GetTrackSectionModel(CancellationToken.None).ConfigureAwait(false);
            }).Wait();
            Instance = new RuntimeData(route, trackSectionModel, trackDb, roadTrackDB, signalConfig, metricUnits, runtimeReferenceResolver);
        }

        public static void Initialize(RouteModel route, TrackSectionModel trackSectionModel, SignalConfigurationFile signalConfig, bool metricUnits, IRuntimeReferenceResolver runtimeReferenceResolver = null)
        {
            Instance = new RuntimeData(route, trackSectionModel, null, null, signalConfig, metricUnits, runtimeReferenceResolver);
        }

        protected RuntimeData(RouteModel route, TrackSectionModel trackSectionModel, TrackDB trackDb, RoadTrackDB roadTrackDB, 
            SignalConfigurationFile signalConfig, bool useMetricUnits, IRuntimeReferenceResolver runtimeReferenceResolver)
        {
            RouteData = route;
            TrackSections = trackSectionModel;
            TrackDB = trackDb;
            RoadTrackDB = roadTrackDB;
            SignalConfigFile = signalConfig;
            MetricUnits = useMetricUnits;
            RuntimeReferenceResolver = runtimeReferenceResolver;
        }

        protected RuntimeData()
        { }
    }
}
