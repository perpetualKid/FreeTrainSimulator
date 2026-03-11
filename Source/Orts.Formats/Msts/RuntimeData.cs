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
        public TrackSectionsModel TrackSections { get; init; }
        public TrackModel TrackModel { get; init; }
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
            TrackSectionsModel trackSectionModel = null;
            TrackModel trackModel = null;

            Task.Run(async () =>
            {
                trackSectionModel = await route.GetTrackSectionModel(CancellationToken.None).ConfigureAwait(false);
                trackModel = await route.GetTrackModel(CancellationToken.None).ConfigureAwait(false);
            }).Wait();
            Instance = new RuntimeData(route, trackSectionModel, trackModel, trackDb, roadTrackDB, signalConfig, metricUnits, runtimeReferenceResolver);
        }

        public static void Initialize(RouteModel route, TrackSectionsModel trackSectionModel, TrackModel trackModel, SignalConfigurationFile signalConfig, bool metricUnits, IRuntimeReferenceResolver runtimeReferenceResolver = null)
        {
            Instance = new RuntimeData(route, trackSectionModel, trackModel, null, null, signalConfig, metricUnits, runtimeReferenceResolver);
        }

        protected RuntimeData(RouteModel route, TrackSectionsModel trackSectionModel, TrackModel trackModel,
            TrackDB trackDb, RoadTrackDB roadTrackDB, SignalConfigurationFile signalConfig, bool useMetricUnits, IRuntimeReferenceResolver runtimeReferenceResolver)
        {
            RouteData = route;
            TrackSections = trackSectionModel;
            TrackModel = trackModel;
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
