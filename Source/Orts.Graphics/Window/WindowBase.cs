using System;

using GetText;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Input;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.Graphics.Window
{
    public abstract class WindowBase : IDisposable
    {
        private static readonly Point EmptyPoint = new Point(-1, -1);

        private bool disposedValue;
        private protected Rectangle borderRect;
        private Matrix xnaWorld;
        private ControlLayout windowLayout;
        private VertexBuffer windowVertexBuffer;
        private IndexBuffer windowIndexBuffer;

        private Point location; // holding the original location in % of screen size)

        public WindowManager Owner { get; }

        protected bool CapturedForDragging { get; private set; }

        public ref readonly Rectangle Borders => ref borderRect;

        public ref readonly Point RelativeLocation => ref location;

        public string Caption { get; protected set; }

        public event EventHandler OnWindowClosed;

        public bool Modal { get; protected set; }

        public bool Interactive { get; protected set; } = true;

        public int ZOrder { get; protected set; }

        internal WindowControl CapturedControl { get; set; }

        public Catalog Catalog { get; }

        protected WindowBase(WindowManager owner, string caption, Point relativeLocation, Point size, Catalog catalog = null)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            location = relativeLocation;
            borderRect.Size = new Point((int)(size.X * owner.DpiScaling), (int)(size.Y * owner.DpiScaling));
            Catalog = catalog ?? CatalogManager.Catalog;
            Caption = catalog?.GetString(caption) ?? caption;
        }

        internal protected virtual void Initialize()
        {
            UpdateLocation();
            Resize();
        }

        public virtual bool Open()
        {
            return Owner.OpenWindow(this);
        }

        public virtual bool Close()
        {
            OnWindowClosed?.Invoke(this, EventArgs.Empty);
            return Owner.CloseWindow(this);
        }

        public virtual void ToggleVisibility()
        {
            if (Owner.WindowOpen(this))
                Close();
            else
                Open();
        }

        public virtual void TabAction(UserCommandArgs args)
        { }

        internal protected virtual void Update(GameTime gameTime)
        {
            windowLayout.Update(gameTime);
        }

        internal protected virtual void WindowDraw()
        {
            ref readonly Matrix xnaView = ref Owner.XNAView;
            ref readonly Matrix xnaProjection = ref Owner.XNAProjection;
            Matrix wvp = xnaWorld * xnaView * xnaProjection;
            Owner.WindowShader.World = xnaWorld;
            Owner.WindowShader.WorldViewProjection = wvp;

            foreach (EffectPass pass in Owner.WindowShader.CurrentTechnique.Passes)
            {
                pass.Apply();
                Owner.Game.GraphicsDevice.SetVertexBuffer(windowVertexBuffer);
                Owner.Game.GraphicsDevice.Indices = windowIndexBuffer;
                Owner.Game.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleStrip, 0, 0, 20);
            }
        }

        internal protected virtual void Draw(SpriteBatch spriteBatch)
        {
            windowLayout.Draw(spriteBatch, Borders.Location);
        }

        protected virtual void SizeChanged()
        {
            Resize();
        }

        protected void Resize(Point size)
        {
            borderRect.Size = new Point((int)(size.X * Owner.DpiScaling), (int)(size.Y * Owner.DpiScaling));
            UpdateLocation();
            Resize();
        }

        protected void Relocate(Point location)
        {
            this.location = new Point(
                (int)Math.Round(100.0 * location.X / (Owner.ClientBounds.Width - borderRect.Width)),
                (int)Math.Round(100.0 * location.Y / (Owner.ClientBounds.Height - borderRect.Height)));
            UpdateLocation();
        }

        private void Resize()
        {
            VertexBuffer tempVertex = windowVertexBuffer;
            windowVertexBuffer = null;
            InitializeBuffers();
            tempVertex?.Dispose();
            Layout();
        }

        internal void UpdateLocation()
        {
            Point position;
            if (location != EmptyPoint)
            {
                position.X = (int)Math.Round((float)location.X * (Owner.ClientBounds.Width - borderRect.Width) / 100);
                position.Y = (int)Math.Round((float)location.Y * (Owner.ClientBounds.Height - borderRect.Height) / 100);
            }
            else
            {
                position.X = (int)Math.Round((Owner.ClientBounds.Width - borderRect.Width) / 2f);
                position.Y = (int)Math.Round((Owner.ClientBounds.Height - borderRect.Height) / 2f);
            }
            borderRect.Location = position;
            borderRect.X = MathHelper.Clamp(borderRect.X, 0, Owner.ClientBounds.Width - borderRect.Width);
            borderRect.Y = MathHelper.Clamp(borderRect.Y, 0, Owner.ClientBounds.Height - borderRect.Height);
            xnaWorld.Translation = new Vector3(borderRect.X, borderRect.Y, 0);
        }

        internal void HandleMouseDrag(Point position, Vector2 delta, KeyModifiers keyModifiers)
        {
            if (CapturedForDragging || !windowLayout.HandleMouseDrag(new WindowMouseEvent(this, position, delta, keyModifiers)))
            {
                borderRect.Offset(delta.ToPoint());
                location = new Point(
                    (int)Math.Round(100.0 * borderRect.X / (Owner.ClientBounds.Width - borderRect.Width)),
                    (int)Math.Round(100.0 * borderRect.Y / (Owner.ClientBounds.Height - borderRect.Height)));
                borderRect.X = MathHelper.Clamp(borderRect.X, 0, Owner.ClientBounds.Width - borderRect.Width);
                borderRect.Y = MathHelper.Clamp(borderRect.Y, 0, Owner.ClientBounds.Height - borderRect.Height);
                xnaWorld.Translation = new Vector3(borderRect.X, borderRect.Y, 0);
                CapturedForDragging = true;
            }
        }

        internal void HandleMouseReleased(Point position, KeyModifiers keyModifiers)
        {
            if (!CapturedForDragging)
                windowLayout.HandleMouseReleased(new WindowMouseEvent(this, position, false, keyModifiers));
            CapturedForDragging = false;
        }

        internal void HandleMouseScroll(Point position, int scrollDelta, KeyModifiers keyModifiers)
        {
            windowLayout.HandleMouseScroll(new WindowMouseEvent(this, position, scrollDelta, keyModifiers));
        }

        internal void HandleMouseClicked(Point position, KeyModifiers keyModifiers)
        { 
            windowLayout.HandleMouseClicked(new WindowMouseEvent(this, position, true, keyModifiers));
        }

        internal void HandleMouseDown(Point position, KeyModifiers keyModifiers)
        {
            windowLayout.HandleMouseDown(new WindowMouseEvent(this, position, true, keyModifiers));
        }

        internal protected void Layout()
        {
            WindowControlLayout windowLayout = new WindowControlLayout(this, borderRect.Width, borderRect.Height);
            Layout(windowLayout);
            windowLayout.Initialize();
            this.windowLayout = windowLayout;
        }

        protected internal virtual void FocusSet()
        { }

        protected internal virtual void FocusLost()
        { }

        protected virtual ControlLayout Layout(ControlLayout layout, float headerScaling = 1.0f)
        {
            System.Drawing.Font headerFont = FontManager.Scaled(Owner.DefaultFont, System.Drawing.FontStyle.Bold)[(int)(Owner.DefaultFontSize * headerScaling)];
            // Pad window by 4px, add caption and separator between to content area.
            layout = layout?.AddLayoutOffset((int)(4 * Owner.DpiScaling)).AddLayoutVertical() ?? throw new ArgumentNullException(nameof(layout));
            Label headerLabel = new Label(this, 0, 0, layout.RemainingWidth, headerFont.Height, Caption, HorizontalAlignment.Center, headerFont, Color.White);
            layout.Add(headerLabel);
            layout.AddHorizontalSeparator(true);
            return layout;
        }

        private void InitializeBuffers()
        {
            if (windowVertexBuffer == null)
            {
                // Edges/corners size. 32px is 1/4th texture image size
                int gp = Math.Min(24, borderRect.Height / 2);
                VertexPositionTexture[] vertexData = new[] {
					//  0  1  2  3
					new VertexPositionTexture(new Vector3(0 * borderRect.Width + 00, 0 * borderRect.Height + 00, 0), new Vector2(0.00f / 2.001f, 0.00f / 1.001f)),
                    new VertexPositionTexture(new Vector3(0 * borderRect.Width + gp, 0 * borderRect.Height + 00, 0), new Vector2(0.25f / 2.001f, 0.00f / 1.001f)),
                    new VertexPositionTexture(new Vector3(1 * borderRect.Width - gp, 0 * borderRect.Height + 00, 0), new Vector2(0.75f / 2.001f, 0.00f / 1.001f)),
                    new VertexPositionTexture(new Vector3(1 * borderRect.Width - 00, 0 * borderRect.Height + 00, 0), new Vector2(1.00f / 2.001f, 0.00f / 1.001f)),
					//  4  5  6  7
					new VertexPositionTexture(new Vector3(0 * borderRect.Width + 00, 0 * borderRect.Height + gp, 0), new Vector2(0.00f / 2.001f, 0.25f / 1.001f)),
                    new VertexPositionTexture(new Vector3(0 * borderRect.Width + gp, 0 * borderRect.Height + gp, 0), new Vector2(0.25f / 2.001f, 0.25f / 1.001f)),
                    new VertexPositionTexture(new Vector3(1 * borderRect.Width - gp, 0 * borderRect.Height + gp, 0), new Vector2(0.75f / 2.001f, 0.25f / 1.001f)),
                    new VertexPositionTexture(new Vector3(1 * borderRect.Width - 00, 0 * borderRect.Height + gp, 0), new Vector2(1.00f / 2.001f, 0.25f / 1.001f)),
					//  8  9 10 11
					new VertexPositionTexture(new Vector3(0 * borderRect.Width + 00, 1 * borderRect.Height - gp, 0), new Vector2(0.00f / 2.001f, 0.75f / 1.001f)),
                    new VertexPositionTexture(new Vector3(0 * borderRect.Width + gp, 1 * borderRect.Height - gp, 0), new Vector2(0.25f / 2.001f, 0.75f / 1.001f)),
                    new VertexPositionTexture(new Vector3(1 * borderRect.Width - gp, 1 * borderRect.Height - gp, 0), new Vector2(0.75f / 2.001f, 0.75f / 1.001f)),
                    new VertexPositionTexture(new Vector3(1 * borderRect.Width - 00, 1 * borderRect.Height - gp, 0), new Vector2(1.00f / 2.001f, 0.75f / 1.001f)),
					// 12 13 14 15
					new VertexPositionTexture(new Vector3(0 * borderRect.Width + 00, 1 * borderRect.Height - 00, 0), new Vector2(0.00f / 2.001f, 1.00f / 1.001f)),
                    new VertexPositionTexture(new Vector3(0 * borderRect.Width + gp, 1 * borderRect.Height - 00, 0), new Vector2(0.25f / 2.001f, 1.00f / 1.001f)),
                    new VertexPositionTexture(new Vector3(1 * borderRect.Width - gp, 1 * borderRect.Height - 00, 0), new Vector2(0.75f / 2.001f, 1.00f / 1.001f)),
                    new VertexPositionTexture(new Vector3(1 * borderRect.Width - 00, 1 * borderRect.Height - 00, 0), new Vector2(1.00f / 2.001f, 1.00f / 1.001f)),
                };
                windowVertexBuffer = new VertexBuffer(Owner.Game.GraphicsDevice, typeof(VertexPositionTexture), vertexData.Length, BufferUsage.WriteOnly);
                windowVertexBuffer.SetData(vertexData);
            }
            if (windowIndexBuffer == null)
            {
                short[] indexData = new short[] {
                    0, 4, 1, 5, 2, 6, 3, 7,
                    11, 6, 10, 5, 9, 4, 8,
                    12, 9, 13, 10, 14, 11, 15,
                };
                windowIndexBuffer = new IndexBuffer(Owner.Game.GraphicsDevice, typeof(short), indexData.Length, BufferUsage.WriteOnly);
                windowIndexBuffer.SetData(indexData);
            }
            Owner.Game.GraphicsDevice.SetVertexBuffer(windowVertexBuffer);
            Owner.Game.GraphicsDevice.Indices = windowIndexBuffer;
            xnaWorld = Matrix.CreateWorld(new Vector3(borderRect.X, borderRect.Y, 0), -Vector3.UnitZ, Vector3.UnitY);
        }

        #region IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    windowVertexBuffer?.Dispose();
                    windowIndexBuffer?.Dispose();
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
