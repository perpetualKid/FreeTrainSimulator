using System;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Runtime;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public sealed class XnaMapContentFactory : IMapContentFactory
    {
        public ToolboxContent CreateToolboxContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null)
        {
            return new ToolboxContent(CreateContext(game, mouseInputGameComponent, insetHost, textureHelperHost));
        }

        /// <summary>Creates a candidate against its private, prepared runtime without looking up global services.</summary>
        public static ToolboxContent CreateToolboxContent(Game game, MouseInputGameComponent mouseInputGameComponent,
            IMapInsetHost insetHost, IMapTextureHelperHost textureHelperHost, RuntimeDataResolver runtimeData)
        {
            ArgumentNullException.ThrowIfNull(game);
            ArgumentNullException.ThrowIfNull(mouseInputGameComponent);
            ArgumentNullException.ThrowIfNull(runtimeData);
            return new ToolboxContent(new MapContentContext(new MapRuntimeServices(runtimeData),
                new XnaMapSessionComposer(game, mouseInputGameComponent), insetHost, textureHelperHost));
        }

        public DispatcherContent CreateDispatcherContent(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost = null, IMapTextureHelperHost textureHelperHost = null)
        {
            return new DispatcherContent(CreateContext(game, mouseInputGameComponent, insetHost, textureHelperHost));
        }

        private static MapContentContext CreateContext(Game game, MouseInputGameComponent mouseInputGameComponent, IMapInsetHost insetHost, IMapTextureHelperHost textureHelperHost)
        {
            ArgumentNullException.ThrowIfNull(game);
            ArgumentNullException.ThrowIfNull(mouseInputGameComponent);

            return new MapContentContext(
                new MapRuntimeServices(game),
                new XnaMapSessionComposer(game, mouseInputGameComponent),
                insetHost,
                textureHelperHost);
        }
    }
}
