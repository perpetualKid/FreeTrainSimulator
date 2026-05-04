using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface ITrackItemInfoContext
    {
        INameValueInformationProvider TrackItemInfo { get; }

        IMapViewport Viewport { get; }

        TrackWorld TrackWorld { get; }
    }
}
