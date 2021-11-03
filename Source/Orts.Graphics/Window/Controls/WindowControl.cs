using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls
{
    public abstract class WindowControl: IDisposable
    {
        private Rectangle position;
        private bool disposedValue;

        public ref readonly Rectangle Position => ref position;

        public object Tag { get; set; }

        public event EventHandler<MouseClickEventArgs> OnClick;

        protected WindowControl(int x, int y, int width, int height)
        {
            position = new Rectangle(x, y, width, height);
        }

        public virtual void Initialize(WindowManager windowManager)
        {
        }

        internal abstract void Draw(SpriteBatch spriteBatch, Point offset);

        internal virtual bool HandleMousePressed(WindowMouseEvent e)
        {
            return false;
        }

        internal virtual bool HandleMouseReleased(WindowMouseEvent e)
        {
            MouseClick(e);
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
            position.X += x;
            position.Y += y;
        }

        internal virtual void MouseClick(WindowMouseEvent e)
        {
            OnClick?.Invoke(this, new MouseClickEventArgs(e.MousePosition - position.Location, e.KeyModifiers));
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
