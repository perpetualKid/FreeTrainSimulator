using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics.MapView.Shapes;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.Graphics.Window.Controls
{
    public abstract class WindowControl: IDisposable
    {
        private Rectangle bounds;
        private bool disposedValue;

        public ref readonly Rectangle Bounds => ref bounds;

        public FormBase Window { get; }

        public ControlLayout Container { get; internal set; }

        public Color BorderColor { get; set; } = Color.Transparent;

        public object Tag { get; set; }

        public event EventHandler<MouseClickEventArgs> OnClick;

        public bool Visible { get; set; } = true;

        protected WindowControl(FormBase window, int x, int y, int width, int height)
        {
            bounds = new Rectangle(x, y, width, height);
            Window = window ?? throw new ArgumentNullException(nameof(window));
        }

        internal virtual void Initialize()
        { }

        internal virtual void Update(GameTime gameTime, bool shouldUpdate)
        { }

        internal virtual void Draw(SpriteBatch spriteBatch, Point offset)
        {
            if (BorderColor != Color.Transparent)
            {
                Window.Owner.BasicShapes.DrawLine(1, BorderColor, (offset + Bounds.Location).ToVector2(), (offset + Bounds.Location + new Point(Bounds.Width, 0)).ToVector2(), spriteBatch);
                Window.Owner.BasicShapes.DrawLine(1, BorderColor, (offset + Bounds.Location + new Point(0, Bounds.Height)).ToVector2(), (offset + Bounds.Location + new Point(Bounds.Width, Bounds.Height)).ToVector2(), spriteBatch);
                Window.Owner.BasicShapes.DrawLine(1, BorderColor, (offset + Bounds.Location).ToVector2(), (offset + Bounds.Location + new Point(0, Bounds.Height)).ToVector2(), spriteBatch);
                Window.Owner.BasicShapes.DrawLine(1, BorderColor, (offset + Bounds.Location + new Point(Bounds.Width, 0)).ToVector2(), (offset + Bounds.Location + Bounds.Size).ToVector2(), spriteBatch);
            }
        }

        internal virtual bool HandleMouseClicked(WindowMouseEvent e)
        {
            return false;
        }

        internal virtual bool HandleMouseReleased(WindowMouseEvent e)
        {
            return RaiseMouseClick(e);
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

        internal virtual bool HandleMouseDrag(WindowMouseEvent e)
        {
            return false;
        }

        internal virtual void MoveBy(int x, int y)
        {
            bounds.Offset(x, y);
        }

        internal virtual void Resize(in Point size)
        {
            bounds.Size = size;
        }

        internal virtual bool RaiseMouseClick(WindowMouseEvent e)
        {
            OnClick?.Invoke(this, new MouseClickEventArgs(e.MousePosition - bounds.Location, e.KeyModifiers));
            return OnClick != null;
        }

        internal virtual bool HandleTextInput(TextInputEventArgs e) => false;

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
