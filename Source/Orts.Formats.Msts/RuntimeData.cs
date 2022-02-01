
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.Formats.Msts
{
    public class RuntimeData
    {
        /// <summary>Name of the route</summary>
        public string RouteName { get; }
        /// <summary>Track Section Data, public such that other classes have access as well</summary>
        public TrackSectionsFile TSectionDat { get; }
        /// <summary>Track database, public such that other classes have access as well</summary>
        public TrackDB TrackDB { get; }
        /// <summary>Road track database</summary>
        public RoadTrackDB RoadTrackDB { get; }
        /// <summary>The signal config file containing, for instance, the information to distinguish normal and non-normal signals</summary>
        public SignalConfigurationFile SignalConfigFile { get; }
        public bool UseMetricUnits { get; }

        public static RuntimeData Instance { get; private set; }

        public static void Initialize(string routeName, TrackSectionsFile trackSections, TrackDB trackDb, RoadTrackDB roadTrackDB, SignalConfigurationFile signalConfig, bool useMetricUnits)
        {
            Instance = new RuntimeData(routeName, trackSections, trackDb, roadTrackDB, signalConfig, useMetricUnits);
        }

        private RuntimeData(string routeName, TrackSectionsFile trackSections, TrackDB trackDb, RoadTrackDB roadTrackDB, SignalConfigurationFile signalConfig, bool useMetricUnits)
        { 
            RouteName = routeName;
            TSectionDat = trackSections;
            TrackDB = trackDb;
            RoadTrackDB = roadTrackDB;
            SignalConfigFile = signalConfig;
            UseMetricUnits = useMetricUnits;
        }

        protected RuntimeData()
        { }
    }
}
