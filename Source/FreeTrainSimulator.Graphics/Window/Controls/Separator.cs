using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Window.Controls
{
    public class Separator : WindowControl
    {
        public int Padding { get; }

        public Separator(FormBase window, int width, int height, int padding) :
            base(window, 0, 0, width, height)
        {
            Padding = padding;
        }

        internal override void Initialize()
        {
            base.Initialize();
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            spriteBatch.Draw(Window.Owner.WhiteTexture, new Rectangle(offset.X + Bounds.X + Padding, offset.Y + Bounds.Y + Padding, Bounds.Width - (2 * Padding), Bounds.Height - (2 * Padding)), Color.White);
            base.Draw(spriteBatch, offset);
        }
    }
}
