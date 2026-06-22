using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface ITrackNodeInfoContext
    {
        INameValueInformationProvider TrackNodeInfo { get; }

        IMapViewport Viewport { get; }

        IMapHostControl HostControl { get; }

        ToolboxContent Content { get; }

        TrackWorld TrackWorld { get; }
    }
}
