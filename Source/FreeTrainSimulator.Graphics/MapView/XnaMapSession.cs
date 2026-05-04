using System;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapSession : IMapShellSession
    {
        private readonly ContentArea contentArea;

        public IMapRenderer Renderer => contentArea;

        public IMapViewport Viewport => contentArea;

        public IMapHostControl HostControl => contentArea;

        public DrawableGameComponent ShellComponent => contentArea;

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
