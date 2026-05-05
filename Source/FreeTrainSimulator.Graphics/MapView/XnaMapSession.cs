using System;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapSession : IXnaMapShellSession
    {
        private readonly ContentArea contentArea;

        public IMapRenderer Renderer => contentArea;

        public IMapViewport Viewport => contentArea;

        public IMapHostControl HostControl => contentArea;

        public IXnaMapShellHost ShellHost { get; }

        IMapShellHost IMapShellSession.ShellHost => ShellHost;

        public XnaMapSession(ContentArea contentArea)
        {
            this.contentArea = contentArea ?? throw new ArgumentNullException(nameof(contentArea));
            ShellHost = new XnaMapShellHost(contentArea);
        }

        public void Dispose()
        {
            contentArea.Dispose();
        }
    }
}
