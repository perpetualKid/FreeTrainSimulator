using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls
{
    public abstract class WindowControl: IDisposable
    {
        private Rectangle bounds;
        private bool disposedValue;

        public ref readonly Rectangle Bounds => ref bounds;

        public WindowBase Window { get; }

        public object Tag { get; set; }

        public event EventHandler<MouseClickEventArgs> OnClick;

        protected WindowControl(WindowBase window, int x, int y, int width, int height)
        {
            bounds = new Rectangle(x, y, width, height);
            Window = window;
        }

        public virtual void Initialize()
        {
        }

        internal abstract void Draw(SpriteBatch spriteBatch, Point offset);

        internal virtual bool HandleMouseClicked(WindowMouseEvent e)
        {
            MouseClick(e);
            return false;
        }

        internal virtual bool HandleMouseReleased(WindowMouseEvent e)
        {
            return false;
        }

        internal virtual bool HandleMouseDown(WindowMouseEvent e)
        {
            return false;
        }

        internal virtual bool HandleMouseMove(WindowMouseEvent e)
        {
            return false;
        }

        internal virtual bool HandleMouseScroll(WindowMouseEvent e)
        {
            return false;
        }

        internal virtual void MoveBy(int x, int y)
        {
            bounds.Offset(x, y);
        }

        internal virtual void MouseClick(WindowMouseEvent e)
        {
            OnClick?.Invoke(this, new MouseClickEventArgs(e.MousePosition - bounds.Location, e.KeyModifiers));
        }

        #region IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
