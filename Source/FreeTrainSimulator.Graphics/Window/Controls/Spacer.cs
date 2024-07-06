using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Window.Controls
{
    public class Spacer : WindowControl
    {
        public Spacer(FormBase window, int width, int height)
            : base(window, 0, 0, width, height)
        {
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            base.Draw(spriteBatch, offset);
        }
    }
}
