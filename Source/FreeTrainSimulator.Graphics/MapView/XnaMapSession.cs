using System;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapSession : IMapSession, IContentAreaSessionAccessor
    {
        private readonly ContentArea contentArea;

        public IMapRenderer Renderer => contentArea;

        public IMapViewport Viewport => contentArea;

        public IMapHostControl HostControl => contentArea;

        ContentArea IContentAreaSessionAccessor.ContentArea => contentArea;

        public XnaMapSession(ContentArea contentArea)
        {
            this.contentArea = contentArea ?? throw new ArgumentNullException(nameof(contentArea));
        }

        public void Dispose()
        {
            contentArea.Dispose();
        }
    }
}
