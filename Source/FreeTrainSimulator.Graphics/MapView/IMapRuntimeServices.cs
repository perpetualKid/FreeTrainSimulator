using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapRuntimeServices
    {
        RuntimeDataResolver RuntimeData { get; }

        TrackWorld TrackWorld { get; }

        string RouteName { get; }

        bool UseMetricUnits { get; }
    }
}
