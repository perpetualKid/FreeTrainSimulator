using System;

using GetText;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Input;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.Graphics.Window
{
    public abstract class FormBase : IDisposable
    {
        private bool disposedValue;
        private protected Rectangle borderRect;
        private protected ControlLayout windowLayout;

        private protected Point location; // holding the original location in % of screen size)

        public WindowManager Owner { get; }

        public ref readonly Rectangle Borders => ref borderRect;

        public ref readonly Point RelativeLocation => ref location;

        public event EventHandler OnWindowOpened;

        public event EventHandler OnWindowClosed;

        public bool Modal { get; protected set; }

        public bool Interactive { get; protected set; } = true;

        public int ZOrder { get; protected set; }

        public bool TopMost { get; protected set; }

        internal WindowControl CapturedControl { get; set; }

        public bool Visible { get; private set; }

        public Catalog Catalog { get; }

        protected FormBase(WindowManager owner, Catalog catalog)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));            ;
        }

        internal protected virtual void Initialize()
        {
        }

        public virtual bool Open()
        {
            Visible = Owner.OpenWindow(this);
            if (Visible)
                OnWindowOpened?.Invoke(this, EventArgs.Empty);
            return Visible;
        }

        public virtual bool Close()
        {
            OnWindowClosed?.Invoke(this, EventArgs.Empty);
            Visible = false;
            return Owner.CloseWindow(this);
        }

        public virtual void ToggleVisibility()
        {
            _ = Owner.WindowOpen(this) ? Close() : Open();
        }

        internal protected virtual void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (Visible)
                windowLayout.Update(gameTime, shouldUpdate);
        }

        internal protected virtual void Draw(SpriteBatch spriteBatch)
        {
            windowLayout.Draw(spriteBatch, Borders.Location);
        }

        internal protected void Layout()
        {
            WindowControlLayout windowLayout = new WindowControlLayout(this, borderRect.Width, borderRect.Height);
            _ = Layout(windowLayout);
            windowLayout.Initialize();
            this.windowLayout = windowLayout;
        }

        protected virtual ControlLayout Layout(ControlLayout layout, float headerScaling = 1.0f)
        {
            return layout;
        }

        #region IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    windowLayout?.Dispose();
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
