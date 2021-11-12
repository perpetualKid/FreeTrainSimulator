
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls
{
    public class Separator: WindowControl
    {
        public int Padding;

        public Separator(int width, int height, int padding) : 
            base(0, 0, width, height)
        {
            Padding = padding;
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            spriteBatch.Draw(WindowManager.WhiteTexture, new Rectangle(offset.X + Position.X + Padding, offset.Y + Position.Y + Padding, Position.Width - 2 * Padding, Position.Height - 2 * Padding), Color.White);
        }
    }
}
