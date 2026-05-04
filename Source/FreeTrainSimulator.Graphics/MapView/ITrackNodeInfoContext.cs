using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface ITrackNodeInfoContext
    {
        IMapViewport Viewport { get; }

        IMapHostControl HostControl { get; }

        ToolboxContent Content { get; }

        TrackWorld TrackWorld { get; }
    }
}
