
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.View.Window.Controls
{
    public class Spacer : WindowControl
    {
        public Spacer(int width, int height)
            : base(0, 0, width, height)
        {
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
        }
    }
}
