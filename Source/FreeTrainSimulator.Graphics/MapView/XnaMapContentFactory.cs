using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public sealed class XnaMapContentFactory : IMapContentFactory
    {
        public ToolboxContent CreateToolboxContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null)
        {
            MapContentContext context = new MapContentContext(
                new MapRuntimeServices(game),
                new XnaMapSessionComposer(game, mouseInputGameComponent),
                insetHost,
                textureHelperHost);
            return new ToolboxContent(context);
        }

        public DispatcherContent CreateDispatcherContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null)
        {
            MapContentContext context = new MapContentContext(
                new MapRuntimeServices(game),
                new XnaMapSessionComposer(game, mouseInputGameComponent),
                insetHost,
                textureHelperHost);
            return new DispatcherContent(context);
        }
    }
}
