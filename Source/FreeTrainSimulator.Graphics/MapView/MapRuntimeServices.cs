using System;

using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class MapRuntimeServices : IMapRuntimeServices
    {
        public RuntimeDataResolver RuntimeData { get; }

        public TrackWorld TrackWorld { get; }

        public string RouteName => RuntimeData.RouteData.Name;

        public bool UseMetricUnits => RuntimeData.MetricUnits;

        public MapRuntimeServices(Game game) : this(RuntimeDataResolver.GameInstance(game))
        {
            ArgumentNullException.ThrowIfNull(game);
        }

        public MapRuntimeServices(RuntimeDataResolver runtimeData)
        {
            RuntimeData = runtimeData ?? throw new InvalidOperationException("Runtime data is not initialized.");
            TrackWorld = RuntimeData.TrackWorld;
        }
    }
}
