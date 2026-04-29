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

        public XnaMapRenderingResources(Game game, SpriteBatch spriteBatch)
        {
            SpriteBatch = spriteBatch;
            BasicShapes = BasicShapes.Instance(game);
            TextShape = TextShape.Instance(game, spriteBatch);
        }
    }
}
