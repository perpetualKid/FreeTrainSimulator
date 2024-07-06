using FreeTrainSimulator.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Window.Controls
{
    public class KeyLabel : Label
    {
        private Color keyColor;

        public KeyLabel(FormBase window, int x, int y, int width, int height, string text, System.Drawing.Font font, Color keyColor)
            : base(window, x, y, width, height, text, HorizontalAlignment.Center, font, Color.White)
        {
            this.keyColor = keyColor;
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            Rectangle targetRectangle = Bounds;
            targetRectangle.Offset(offset);
            Window.Owner.BasicShapes.DrawTexture(BasicTextureType.BlankPixel, targetRectangle, Color.White, spriteBatch);
            targetRectangle.Inflate(-1, -1);
            Window.Owner.BasicShapes.DrawTexture(BasicTextureType.BlankPixel, targetRectangle, keyColor, spriteBatch);
            base.Draw(spriteBatch, offset);
        }
    }
}
