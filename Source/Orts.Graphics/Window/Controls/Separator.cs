
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls
{
    public class Separator: WindowControl
    {
        public int Padding { get; }

        public Separator(WindowBase window, int width, int height, int padding) : 
            base(window, 0, 0, width, height)
        {
            Padding = padding;
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            spriteBatch.Draw(Window.Owner.WhiteTexture, new Rectangle(offset.X + Position.X + Padding, offset.Y + Position.Y + Padding, Position.Width - 2 * Padding, Position.Height - 2 * Padding), Color.White);
        }
    }
}
