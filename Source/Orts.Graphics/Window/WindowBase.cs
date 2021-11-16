using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Input;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Orts.Graphics.Window
{
    public abstract class WindowBase : IDisposable
    {
        private const int BaseFontSize = 16; // DO NOT CHANGE without also changing the graphics for the windows.

        private bool disposedValue;
        private protected Rectangle location;
        private Matrix xnaWorld;
        private ControlLayout windowLayout;
        private VertexBuffer windowVertexBuffer;
        private IndexBuffer windowIndexBuffer;

        protected WindowManager Owner { get; }

        public ref readonly Rectangle Borders => ref location;

        public string Caption { get; }

        public event EventHandler OnWindowClosed;

        public virtual bool Modal => false;

        protected WindowBase(WindowManager owner, string caption, Point position, Point size)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            position = new Point((owner.Game.Window.ClientBounds.Width - size.X) / 2, (owner.Game.Window.ClientBounds.Height - size.Y) / 2);
            location = new Rectangle(position, size);// (100, 100, 200, 100);//TODO 20211030 Load settings or apply default settings
            Caption = caption;
            Resize();
        }

        internal protected virtual void Initialize()
        {
            Resize();
        }

        public virtual bool Open()
        {
            return Owner.AddWindow(this);
        }

        public virtual void Close()
        {
            OnWindowClosed?.Invoke(this, EventArgs.Empty);
            Owner.CloseWindow(this);
        }

        internal void RenderWindow()
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

        internal void DrawContent(SpriteBatch spriteBatch)
        {
            windowLayout.Draw(spriteBatch, Borders.Location);
        }

        protected virtual void SizeChanged()
        {
            Resize();
        }

        private void Resize()
        {
            VertexBuffer tempVertex = windowVertexBuffer;
            windowVertexBuffer = null;
            InitializeBuffers();
            tempVertex?.Dispose();
            Layout();
        }

        internal void HandleMouseDrag(Point position, Vector2 delta, KeyModifiers keyModifiers)
        {
            _ = position;
            _ = keyModifiers;
            location.Location += delta.ToPoint();
            xnaWorld.Translation = new Vector3(location.X, location.Y, 0);
        }

        internal void HandleMouseClick(Point position, KeyModifiers keyModifiers)
        {
            windowLayout.HandleMouseClicked(new WindowMouseEvent(this, position, true, keyModifiers));
        }

        internal protected void Layout()
        {
            WindowControlLayout windowLayout = new WindowControlLayout(this, location.Width, location.Height);
            //{
            //    TextHeight = Owner.TextFontDefault.Height
            //};
            Layout(windowLayout);
            windowLayout.Initialize(Owner);
            this.windowLayout = windowLayout;
        }

        protected virtual ControlLayout Layout(ControlLayout layout)
        {
            // Pad window by 4px, add caption and space between to content area.
            layout = layout?.AddLayoutOffset(4).AddLayoutVertical() ?? throw new ArgumentNullException(nameof(layout));
            layout.Add(new Label(layout.RemainingWidth, 18, Caption, LabelAlignment.Center));
            return layout;
        }

        private void InitializeBuffers()
        {
            if (windowVertexBuffer == null)
            {
                // Edges/corners are 32px (1/4th texture image size).
                int gp = 32 - BaseFontSize + (int)(Owner.TextFontDefault.Height * 1.25);
                VertexPositionTexture[] vertexData = new[] {
					//  0  1  2  3
					new VertexPositionTexture(new Vector3(0 * location.Width + 00, 0 * location.Height + 00, 0), new Vector2(0.00f / 2, 0.00f)),
                    new VertexPositionTexture(new Vector3(0 * location.Width + gp, 0 * location.Height + 00, 0), new Vector2(0.25f / 2, 0.00f)),
                    new VertexPositionTexture(new Vector3(1 * location.Width - gp, 0 * location.Height + 00, 0), new Vector2(0.75f / 2, 0.00f)),
                    new VertexPositionTexture(new Vector3(1 * location.Width - 00, 0 * location.Height + 00, 0), new Vector2(1.00f / 2, 0.00f)),
					//  4  5  6  7
					new VertexPositionTexture(new Vector3(0 * location.Width + 00, 0 * location.Height + gp, 0), new Vector2(0.00f / 2, 0.25f)),
                    new VertexPositionTexture(new Vector3(0 * location.Width + gp, 0 * location.Height + gp, 0), new Vector2(0.25f / 2, 0.25f)),
                    new VertexPositionTexture(new Vector3(1 * location.Width - gp, 0 * location.Height + gp, 0), new Vector2(0.75f / 2, 0.25f)),
                    new VertexPositionTexture(new Vector3(1 * location.Width - 00, 0 * location.Height + gp, 0), new Vector2(1.00f / 2, 0.25f)),
					//  8  9 10 11
					new VertexPositionTexture(new Vector3(0 * location.Width + 00, 1 * location.Height - gp, 0), new Vector2(0.00f / 2, 0.75f)),
                    new VertexPositionTexture(new Vector3(0 * location.Width + gp, 1 * location.Height - gp, 0), new Vector2(0.25f / 2, 0.75f)),
                    new VertexPositionTexture(new Vector3(1 * location.Width - gp, 1 * location.Height - gp, 0), new Vector2(0.75f / 2, 0.75f)),
                    new VertexPositionTexture(new Vector3(1 * location.Width - 00, 1 * location.Height - gp, 0), new Vector2(1.00f / 2, 0.75f)),
					// 12 13 14 15
					new VertexPositionTexture(new Vector3(0 * location.Width + 00, 1 * location.Height - 00, 0), new Vector2(0.00f / 2, 1.00f)),
                    new VertexPositionTexture(new Vector3(0 * location.Width + gp, 1 * location.Height - 00, 0), new Vector2(0.25f / 2, 1.00f)),
                    new VertexPositionTexture(new Vector3(1 * location.Width - gp, 1 * location.Height - 00, 0), new Vector2(0.75f / 2, 1.00f)),
                    new VertexPositionTexture(new Vector3(1 * location.Width - 00, 1 * location.Height - 00, 0), new Vector2(1.00f / 2, 1.00f)),
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
            xnaWorld = Matrix.CreateWorld(new Vector3(location.X, location.Y, 0), -Vector3.UnitZ, Vector3.UnitY);
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

    //TODO 20211112 remove
    public class TestWindow : WindowBase
    {
        public TestWindow(WindowManager owner, Point location, string caption) :
            base(owner, caption, location, new Point(100, 200))
        {
        }
        public static void AddForms(WindowManager manager)
        {
            manager.AddWindow(new TestWindow(manager, new Point(100, 100), "Test"));
            manager.AddWindow(new TestWindow(manager, new Point(200, 150), "Another Test"));
        }


    }
}
