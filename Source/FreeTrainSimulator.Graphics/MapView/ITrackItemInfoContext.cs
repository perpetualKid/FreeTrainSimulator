using FreeTrainSimulator.Common.DebugInfo;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface ITrackItemInfoContext
    {
        INameValueInformationProvider TrackItemInfo { get; }

        IMapViewport Viewport { get; }
    }
}
