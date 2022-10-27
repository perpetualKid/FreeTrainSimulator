using System.Drawing;

using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls
{
    public abstract class TextControl : WindowTextureControl
    {
        private protected string text;
        private protected Font font;
        private protected static Brush whiteBrush = new SolidBrush(Color.White);

        private protected readonly TextTextureResourceHolder resourceHolder;

        protected TextControl(WindowBase window, int x, int y, int width, int height) :
            base(window, x, y, width, height)
        {
            resourceHolder = TextTextureResourceHolder.Instance(Window.Owner.Game);
            Window.OnWindowOpened += Window_OnWindowOpened;
            Window.OnWindowClosed += Window_OnWindowClosed;
            if (Window.Visible)
                resourceHolder.Refresh += RefreshResources;
        }

        private void Window_OnWindowClosed(object sender, System.EventArgs e)
        {
            resourceHolder.Refresh -= RefreshResources;
        }

        private void Window_OnWindowOpened(object sender, System.EventArgs e)
        {
            InitializeText(text);
            resourceHolder.Refresh += RefreshResources;
        }

        private protected virtual void RefreshResources(object sender, System.EventArgs e)
        {
            InitializeText(text);
        }

        protected virtual void InitializeText(string text)
        {
            texture = string.IsNullOrEmpty(text) ? resourceHolder.EmptyTexture : resourceHolder.PrepareResource(text, font);
        }
    }
}
