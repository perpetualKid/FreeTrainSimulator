using System.Drawing;

using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls
{
    public abstract class TextControl : WindowTextureControl
    {
        private protected Font font;
        private protected static Brush whiteBrush = new SolidBrush(Color.White);

        private readonly TextTextureResourceHolder resourceHolder;

        protected TextControl(WindowBase window, int x, int y, int width, int height) : 
            base(window, x, y, width, height)
        {
            resourceHolder = TextTextureResourceHolder.Instance(Window.Owner.Game);
        }

        protected virtual void InitializeText(string text)
        {
            texture = string.IsNullOrEmpty(text) ? resourceHolder.EmptyTexture : resourceHolder.PrepareResource(text, font);
        }
    }
}
