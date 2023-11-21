using System;

using GetText;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Input;
using Orts.Graphics.Window.Controls;
using Orts.Graphics.Window.Controls.Layout;

namespace Orts.Graphics.Window
{
    public abstract class WindowBase : FormBase
    {
        private static readonly Point EmptyPoint = new Point(-1, -1);

        private bool disposedValue;

        private Matrix xnaWorld;
        private VertexBuffer windowVertexBuffer;
        private IndexBuffer windowIndexBuffer;
        private WindowControl inactiveControl;

        public bool CloseButton { get; protected set; } = true;

        protected bool CapturedForDragging { get; private set; }

        internal WindowControl ActiveControl { get; set; }

        public string Caption { get; protected set; }

        protected WindowBase(WindowManager owner, string caption, Point relativeLocation, Point size, Catalog catalog) : 
            base(owner, catalog)
        {
            Caption = caption;
            location = relativeLocation;
            if (size.X < 0)
            {
                if (size.X < -100)
                    throw new ArgumentOutOfRangeException(nameof(size), "Relative window size must be defined in range between -0 to -100 (% of game window size)");
                size.X = owner.Size.X * -size.X / 100;
            }
            if (size.Y < 0)
            {
                if (size.X < -100)
                    throw new ArgumentOutOfRangeException(nameof(size), "Relative window size must be defined in range between -0 to -100 (% of game window size)");
                size.Y = owner.Size.Y * -size.Y / 100;
            }
            borderRect.Size = new Point((int)(size.X * owner.DpiScaling), (int)(size.Y * owner.DpiScaling));
        }

        protected internal override void Initialize()
        {
            UpdateLocation();
            Resize();
        }

        protected override ControlLayout Layout(ControlLayout layout, float headerScaling = 1)
        {
            ArgumentNullException.ThrowIfNull(layout);
            System.Drawing.Font headerFont = FontManager.Scaled(Owner.FontName, System.Drawing.FontStyle.Bold)[(int)(Owner.FontSize * headerScaling)];
            layout = layout.AddLayoutOffset((int)(4 * Owner.DpiScaling));
            if (CloseButton)
            {
                ControlLayout buttonLine = layout.AddLayoutHorizontal();
                buttonLine.HorizontalChildAlignment = HorizontalAlignment.Right;
                buttonLine.VerticalChildAlignment = VerticalAlignment.Top;
                Label closeLabel = new Label(this, 0, 0, headerFont.Height, headerFont.Height, "❎", HorizontalAlignment.Right, headerFont, Color.White);
                //❎❌
                closeLabel.OnClick += CloseLabel_OnClick;
                buttonLine.Add(closeLabel);
            }
            // Pad window by 4px, add caption and separator between to content area.
            layout = layout.AddLayoutVertical();
            Label headerLabel = new Label(this, 0, 0, layout.RemainingWidth, headerFont.Height, Caption, HorizontalAlignment.Center, headerFont, Color.White);
            layout.Add(headerLabel);
            layout.AddHorizontalSeparator(true);
            return layout;
        }

        protected void Resize(Point size)
        {
            borderRect.Size = new Point((int)(size.X * Owner.DpiScaling), (int)(size.Y * Owner.DpiScaling));
            UpdateLocation();
            Resize();
        }

        protected virtual void SizeChanged()
        {
            Resize();
        }

        protected void Relocate(Point location)
        {
            this.location = new Point(
                (int)Math.Round(100.0 * location.X / (Owner.Size.X - borderRect.Width)),
                (int)Math.Round(100.0 * location.Y / (Owner.Size.Y - borderRect.Height)));
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

        protected internal virtual void FocusSet()
        {
            ActiveControl = inactiveControl;
        }

        protected internal virtual void FocusLost()
        {
            inactiveControl = ActiveControl;
            ActiveControl = null;
        }


        internal void UpdateLocation()
        {
            Point position;
            if (location != EmptyPoint)
            {
                position.X = (int)Math.Round((float)location.X * (Owner.Size.X - borderRect.Width) / 100);
                position.Y = (int)Math.Round((float)location.Y * (Owner.Size.Y - borderRect.Height) / 100);
            }
            else
            {
                position.X = (int)Math.Round((Owner.Size.X - borderRect.Width) / 2f);
                position.Y = (int)Math.Round((Owner.Size.Y - borderRect.Height) / 2f);
            }
            borderRect.Location = position;
            borderRect.X = MathHelper.Clamp(borderRect.X, 0, Owner.Size.X - borderRect.Width);
            borderRect.Y = MathHelper.Clamp(borderRect.Y, 0, Owner.Size.Y - borderRect.Height);
            xnaWorld.Translation = new Vector3(borderRect.X, borderRect.Y, 0);
        }

        internal virtual void HandleMouseDrag(Point position, Vector2 delta, KeyModifiers keyModifiers)
        {
            if (CapturedForDragging || !windowLayout.HandleMouseDrag(new WindowMouseEvent(this, position, delta, keyModifiers)))
            {
                borderRect.Offset(delta.ToPoint());
                location = new Point(
                    (int)Math.Round(100.0 * borderRect.X / (Owner.Size.X - borderRect.Width)),
                    (int)Math.Round(100.0 * borderRect.Y / (Owner.Size.Y - borderRect.Height)));
                borderRect.X = MathHelper.Clamp(borderRect.X, 0, Owner.Size.X - borderRect.Width);
                borderRect.Y = MathHelper.Clamp(borderRect.Y, 0, Owner.Size.Y - borderRect.Height);
                xnaWorld.Translation = new Vector3(borderRect.X, borderRect.Y, 0);
                CapturedForDragging = true;
            }
        }

        internal virtual bool HandleMouseReleased(Point position, KeyModifiers keyModifiers)
        {
            bool result = false;
            if (!CapturedForDragging)
                result = windowLayout.HandleMouseReleased(new WindowMouseEvent(this, position, false, keyModifiers));
            CapturedForDragging = false;
            return result;
        }

        internal bool HandleMouseScroll(Point position, int scrollDelta, KeyModifiers keyModifiers)
        {
            return windowLayout.HandleMouseScroll(new WindowMouseEvent(this, position, scrollDelta, keyModifiers));
        }

        internal bool HandleMouseClicked(Point position, KeyModifiers keyModifiers)
        {
            return windowLayout.HandleMouseClicked(new WindowMouseEvent(this, position, true, keyModifiers));
        }

        internal bool HandleMouseDown(Point position, KeyModifiers keyModifiers)
        {
            return windowLayout.HandleMouseDown(new WindowMouseEvent(this, position, true, keyModifiers));
        }

        private void CloseLabel_OnClick(object sender, MouseClickEventArgs e)
        {
            _ = Close();
        }

        protected void ActivateControl(WindowControl control)
        {
            ActiveControl = control;
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
                Owner.GraphicsDevice.SetVertexBuffer(windowVertexBuffer);
                Owner.GraphicsDevice.Indices = windowIndexBuffer;
                Owner.GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleStrip, 0, 0, 20);
            }
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
                windowVertexBuffer = new VertexBuffer(Owner.GraphicsDevice, typeof(VertexPositionTexture), vertexData.Length, BufferUsage.WriteOnly);
                windowVertexBuffer.SetData(vertexData);
            }
            if (windowIndexBuffer == null)
            {
                short[] indexData = new short[] {
                    0, 4, 1, 5, 2, 6, 3, 7,
                    11, 6, 10, 5, 9, 4, 8,
                    12, 9, 13, 10, 14, 11, 15,
                };
                windowIndexBuffer = new IndexBuffer(Owner.GraphicsDevice, typeof(short), indexData.Length, BufferUsage.WriteOnly);
                windowIndexBuffer.SetData(indexData);
            }
            xnaWorld = Matrix.CreateWorld(new Vector3(borderRect.X, borderRect.Y, 0), -Vector3.UnitZ, Vector3.UnitY);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    windowVertexBuffer?.Dispose();
                    windowIndexBuffer?.Dispose();
                }
                disposedValue = true;
            }
            base.Dispose(disposing);
        }
    }
}
