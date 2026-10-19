using System;

using FreeTrainSimulator.Common.DebugInfo;
using FreeTrainSimulator.Graphics.MapView;

namespace FreeTrainSimulator.Toolbox
{
    public class ContentAreaChangedEventArgs : EventArgs
    {
        public ContentArea ContentArea { get; }

        public IMapRenderer Renderer => ContentArea;

        public IMapViewport Viewport => ContentArea;

        public IMapHostControl HostControl => ContentArea;

        public IMapLocationContext LocationContext => ContentArea as IMapLocationContext;

        public IMapDisplaySettingsContext DisplaySettingsContext => ContentArea as IMapDisplaySettingsContext;

        public ITrackNodeInfoContext TrackNodeInfoContext => ContentArea?.Content as ITrackNodeInfoContext;

        public ITrackItemInfoContext TrackItemInfoContext => ContentArea?.Content as ITrackItemInfoContext;

        public INameValueInformationProvider RouteInformationProvider => ContentArea?.Content;

        public ContentAreaChangedEventArgs(ContentArea contentArea)
        {
            ContentArea = contentArea;
        }
    }
}
