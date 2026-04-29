using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapRenderingLifetime : IMapRenderingLifetime
    {
        private readonly Game game;
        private readonly TextTextureRenderer textTextureRenderer;
        private readonly BasicShapes basicShapes;

        public XnaMapRenderingLifetime(Game game)
        {
            this.game = game;
            textTextureRenderer = TextTextureRenderer.Create(game);
            basicShapes = BasicShapes.Create(game.GraphicsDevice);
        }

        public BasicShapes GetBasicShapes()
        {
            return basicShapes;
        }

        public TextShape GetTextShape(SpriteBatch spriteBatch)
        {
            return TextShape.Instance(game, spriteBatch);
        }

        public TextTextureRenderer GetTextTextureRenderer()
        {
            return textTextureRenderer;
        }
    }
}
