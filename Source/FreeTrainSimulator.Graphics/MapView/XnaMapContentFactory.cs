using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public sealed class XnaMapContentFactory : IMapContentFactory
    {
        private readonly IMapHostAdapterFactory adapterFactory;

        public XnaMapContentFactory()
            : this(new XnaMapHostAdapterFactory())
        {
        }

        public XnaMapContentFactory(IMapHostAdapterFactory adapterFactory)
        {
            this.adapterFactory = adapterFactory;
        }

        public ToolboxContent CreateToolboxContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null)
        {
            return new ToolboxContent(game, mouseInputGameComponent, adapterFactory, insetHost, textureHelperHost);
        }

        public DispatcherContent CreateDispatcherContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null)
        {
            return new DispatcherContent(game, mouseInputGameComponent, adapterFactory, insetHost, textureHelperHost);
        }
    }
}
