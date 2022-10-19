using System.Drawing;

using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls
{
    public abstract class TextControl : WindowTextureControl
    {
        private protected string text;
        private protected Font font;
        private protected static Brush whiteBrush = new SolidBrush(Color.White);

        private readonly TextTextureResourceHolder resourceHolder;

        protected TextControl(WindowBase window, int x, int y, int width, int height) :
            base(window, x, y, width, height)
        {
            resourceHolder = TextTextureResourceHolder.Instance(Window.Owner.Game);
            Window.OnWindowOpened += Window_OnWindowOpened;
            Window.OnWindowClosed += Window_OnWindowClosed;
        }

        private void Window_OnWindowClosed(object sender, System.EventArgs e)
        {
            resourceHolder.Refresh -= ResourceHolder_Refresh;
        }

        private void Window_OnWindowOpened(object sender, System.EventArgs e)
        {
            InitializeText(text);
            resourceHolder.Refresh += ResourceHolder_Refresh;
        }

        private void ResourceHolder_Refresh(object sender, System.EventArgs e)
        {
            InitializeText(text);
        }

        protected virtual void InitializeText(string text)
        {
            texture = string.IsNullOrEmpty(text) ? resourceHolder.EmptyTexture : resourceHolder.PrepareResource(text, font);
        }
    }
}
