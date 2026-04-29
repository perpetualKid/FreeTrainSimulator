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

        public XnaMapRenderingLifetime(Game game)
        {
            this.game = game;
        }

        public BasicShapes GetBasicShapes()
        {
            return BasicShapes.Instance(game);
        }

        public TextShape GetTextShape(SpriteBatch spriteBatch)
        {
            return TextShape.Instance(game, spriteBatch);
        }

        public TextTextureRenderer GetTextTextureRenderer()
        {
            return TextTextureRenderer.Instance(game);
        }
    }
}
