using FreeTrainSimulator.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapRenderingResources : IMapRenderingResources
    {
        public SpriteBatch SpriteBatch { get; }

        public BasicShapes BasicShapes { get; }

        public XnaMapRenderingResources(IMapRenderingLifetime renderingLifetime, SpriteBatch spriteBatch)
        {
            SpriteBatch = spriteBatch;
            BasicShapes = renderingLifetime.GetBasicShapes();
        }
    }
}
