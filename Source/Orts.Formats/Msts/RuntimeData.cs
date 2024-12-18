using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.Formats.Msts
{
    public class RuntimeData
    {
        public RouteModel RouteData { get; }
        /// <summary>Track Section Data, public such that other classes have access as well</summary>
        public TrackSectionsFile TSectionDat { get; }
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

        public static void Initialize(RouteModel route, TrackSectionsFile trackSections, TrackDB trackDb, RoadTrackDB roadTrackDB, SignalConfigurationFile signalConfig, bool metricUnits, IRuntimeReferenceResolver runtimeReferenceResolver = null)
        {
            Instance = new RuntimeData(route, trackSections, trackDb, roadTrackDB, signalConfig, metricUnits, runtimeReferenceResolver);
        }

        protected RuntimeData(RouteModel route, TrackSectionsFile trackSections, TrackDB trackDb, RoadTrackDB roadTrackDB, SignalConfigurationFile signalConfig, bool useMetricUnits, IRuntimeReferenceResolver runtimeReferenceResolver)
        {
            RouteData = route;
            TSectionDat = trackSections;
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
