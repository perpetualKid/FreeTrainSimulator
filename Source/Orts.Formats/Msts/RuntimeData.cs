using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.Formats.Msts
{
    public class RuntimeData
    {
        /// <summary>Track database, public such that other classes have access as well</summary>
        public TrackDB TrackDB { get; }
        /// <summary>Road track database</summary>
        public RoadTrackDB RoadTrackDB { get; }
        /// <summary>The signal config file containing i.e. the information to distinguish normal and non-normal signals</summary>
        public SignalConfigurationFile SignalConfigFile { get; }
        public IRuntimeReferenceResolver RuntimeReferenceResolver { get; }

        public static RuntimeData Instance => GameService<RuntimeData>.Instance;

        public static RuntimeData GameInstance(Game game) => GameService<RuntimeData>.Get(game);

        public static void Initialize(TrackDB trackDb, RoadTrackDB roadTrackDB, SignalConfigurationFile signalConfig, IRuntimeReferenceResolver runtimeReferenceResolver = null)
        {
            GameService<RuntimeData>.Set(null, new RuntimeData(trackDb, roadTrackDB, signalConfig, runtimeReferenceResolver));
        }

        public static void Initialize(SignalConfigurationFile signalConfig, IRuntimeReferenceResolver runtimeReferenceResolver = null)
        {
            GameService<RuntimeData>.Set(null, new RuntimeData(null, null, signalConfig, runtimeReferenceResolver));
        }

        protected RuntimeData(TrackDB trackDb, RoadTrackDB roadTrackDB, SignalConfigurationFile signalConfig, IRuntimeReferenceResolver runtimeReferenceResolver)
        {
            TrackDB = trackDb;
            RoadTrackDB = roadTrackDB;
            SignalConfigFile = signalConfig;
            RuntimeReferenceResolver = runtimeReferenceResolver;
        }

        protected RuntimeData()
        { }
    }
}
