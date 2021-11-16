
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls
{
    public class Separator: WindowControl
    {
        public int Padding { get; }

        private WindowManager windowManager;

        public Separator(int width, int height, int padding) : 
            base(0, 0, width, height)
        {
            Padding = padding;
        }

        public override void Initialize(WindowManager windowManager)
        {
            this.windowManager = windowManager;
            base.Initialize(windowManager);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            spriteBatch.Draw(windowManager.WhiteTexture, new Rectangle(offset.X + Position.X + Padding, offset.Y + Position.Y + Padding, Position.Width - 2 * Padding, Position.Height - 2 * Padding), Color.White);
        }
    }
}
