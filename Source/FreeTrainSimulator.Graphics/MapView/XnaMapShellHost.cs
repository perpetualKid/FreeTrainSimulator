using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapShellHost : IXnaMapShellHost
    {
        private readonly ContentArea contentArea;

        public IMapHostControl HostControl => contentArea;

        public DrawableGameComponent Component => contentArea;

        public XnaMapShellHost(ContentArea contentArea)
        {
            this.contentArea = contentArea;
        }
    }
}
