using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public sealed class XnaMapContentFactory : IMapContentFactory
    {
        public ToolboxContent CreateToolboxContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null)
        {
            return new ToolboxContent(game, mouseInputGameComponent, new XnaMapSessionComposer(game, mouseInputGameComponent), insetHost, textureHelperHost);
        }

        public DispatcherContent CreateDispatcherContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null)
        {
            return new DispatcherContent(game, mouseInputGameComponent, new XnaMapSessionComposer(game, mouseInputGameComponent), insetHost, textureHelperHost);
        }
    }
}
