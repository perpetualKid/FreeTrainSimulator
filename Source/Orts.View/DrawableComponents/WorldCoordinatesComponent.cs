
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.View.Xna;

namespace Orts.View.DrawableComponents
{
    public class WorldCoordinatesComponent: QuickRepeatableDrawableTextComponent
    {
        private Vector2 position;
        private Vector2 positionOffset;

        private readonly SpriteBatch spriteBatch;
        private Color color = Color.Black;

        public WorldCoordinatesComponent(Game game, System.Drawing.Font font, Color color, Vector2 position) : 
            base(game, font, color, position)
        {
            spriteBatch = new SpriteBatch(game?.GraphicsDevice);
            this.color = color;
            this.position = position;
            if (position.X < 0 || position.Y < 0)
            {
                positionOffset = position;
            }
        }
    }
}
