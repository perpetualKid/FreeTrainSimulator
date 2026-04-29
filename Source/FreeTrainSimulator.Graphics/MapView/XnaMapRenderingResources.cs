using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapRenderingResources : IMapRenderingResources
    {
        public SpriteBatch SpriteBatch { get; }

        public BasicShapes BasicShapes { get; }

        public TextShape TextShape { get; }

        public XnaMapRenderingResources(IMapRenderingLifetime renderingLifetime, SpriteBatch spriteBatch)
        {
            SpriteBatch = spriteBatch;
            BasicShapes = renderingLifetime.GetBasicShapes();
            TextShape = renderingLifetime.GetTextShape(spriteBatch);
        }
    }
}
