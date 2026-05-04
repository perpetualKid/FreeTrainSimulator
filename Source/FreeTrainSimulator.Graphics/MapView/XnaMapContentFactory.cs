using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public sealed class XnaMapContentFactory : IMapContentFactory
    {
        private readonly IMapSessionComposer sessionComposer;

        public XnaMapContentFactory()
            : this(new XnaMapSessionComposer())
        {
        }

        public XnaMapContentFactory(IMapSessionComposer sessionComposer)
        {
            this.sessionComposer = sessionComposer;
        }

        public ToolboxContent CreateToolboxContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null)
        {
            return new ToolboxContent(game, mouseInputGameComponent, sessionComposer, insetHost, textureHelperHost);
        }

        public DispatcherContent CreateDispatcherContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null)
        {
            return new DispatcherContent(game, mouseInputGameComponent, sessionComposer, insetHost, textureHelperHost);
        }
    }
}
