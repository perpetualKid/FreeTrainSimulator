// COPYRIGHT 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Input;

using System;
using System.IO;
using System.Reflection;

namespace Orts.ActivityRunner.Viewer3D.Popups
{
    public abstract class Window : RenderPrimitive, IDisposable
    {
        private const int BaseFontSize = 16; // DO NOT CHANGE without also changing the graphics for the windows.

        public static Point DecorationOffset { get; } = new Point(4, 4 + BaseFontSize + 5);
        public static Point DecorationSize { get; } = new Point(4 + 4, 4 + BaseFontSize + 5 + 4);
        private Matrix xnaWorld;
        public ref readonly Matrix XNAWorld => ref xnaWorld;

        protected WindowManager Owner { get; }
        private protected bool dragged;
        private bool visible;
        private Rectangle location;
        private readonly string caption;
        private readonly PropertyInfo settingsProperty;
        private ControlLayout windowLayout;
        private VertexBuffer windowVertexBuffer;
        private IndexBuffer windowIndexBuffer;
        private bool disposedValue;

        protected Window(WindowManager owner, int width, int height, string caption)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            // We need to correct the window height for the ACTUAL font size, so that the title bar is shown correctly.
            location = new Rectangle(0, 0, width, height - BaseFontSize + owner.TextFontDefault.Height);

            settingsProperty = Owner.Viewer.Settings.GetType().GetProperty("WindowPosition_" + GetType().Name.Replace("Window", "", StringComparison.OrdinalIgnoreCase));
            if (settingsProperty != null)
            {
                int[] value = settingsProperty.GetValue(Owner.Viewer.Settings, null) as int[];
                if (value?.Length >= 2)
                {
                    location.X = (int)Math.Round((float)value[0] * (Owner.ScreenSize.X - location.Width) / 100);
                    location.Y = (int)Math.Round((float)value[1] * (Owner.ScreenSize.Y - location.Height) / 100);
                }
            }

            this.caption = caption;
            Owner.Add(this);
        }

        internal protected virtual void Initialize()
        {
            VisibilityChanged();
            LocationChanged();
            SizeChanged();
        }

        internal protected virtual void Save(BinaryWriter outf)
        {
            if (null == outf)
                throw new ArgumentNullException(nameof(outf));

            outf.Write(visible);
            outf.Write((float)location.X / (Owner.ScreenSize.X - location.Width));
            outf.Write((float)location.Y / (Owner.ScreenSize.Y - location.Height));
        }

        internal protected virtual void Restore(BinaryReader inf)
        {
            visible = inf?.ReadBoolean() ?? throw new ArgumentNullException(nameof(inf));
            int x = location.X;
            int y = location.Y;
            location.X = (int)(inf.ReadSingle() * (Owner.ScreenSize.X - location.Width));
            location.Y = (int)(inf.ReadSingle() * (Owner.ScreenSize.Y - location.Height));
            // This is needed to move the window background to the correct position
            if ((location.X != x) || (location.Y != y))
                LocationChanged();
        }

        protected virtual void VisibilityChanged()
        {
            if (Visible)
            {
                Owner.BringWindowToTop(this);

                if (windowLayout != null)
                    PrepareFrame(ElapsedTime.Zero, true);
            }
        }

        protected virtual void LocationChanged()
        {
            if (settingsProperty != null)
            {
                settingsProperty.SetValue(Owner.Viewer.Settings, new[] { (int)Math.Round(100f * location.X / (Owner.ScreenSize.X - location.Width)), (int)Math.Round(100f * location.Y / (Owner.ScreenSize.Y - location.Height)) }, null);
                Owner.Viewer.Settings.Save(settingsProperty.Name);
            }

            xnaWorld = Matrix.CreateWorld(new Vector3(location.X, location.Y, 0), -Vector3.UnitZ, Vector3.UnitY);
        }

        protected virtual void SizeChanged()
        {
            Layout();
            windowVertexBuffer = null;
        }

        internal virtual void ScreenChanged()
        {
        }

        public bool Visible
        {
            get => visible;
            set
            {
                if (visible != value)
                {
                    visible = value;
                    VisibilityChanged();
                }
            }
        }

        public virtual bool Interactive => true;

        public virtual bool TopMost => false;

        public Rectangle Location => location;

        public virtual void TabAction()
        {
        }

        public void MoveTo(int x, int y)
        {
            x = (int)MathHelper.Clamp(x, 0, Owner.ScreenSize.X - location.Width);
            y = (int)MathHelper.Clamp(y, 0, Owner.ScreenSize.Y - location.Height);

            if ((location.X != x) || (location.Y != y))
            {
                location.X = x;
                location.Y = y;
                LocationChanged();
            }
        }

        internal protected void Layout()
        {
            WindowControlLayout windowLayout = new WindowControlLayout(this, location.Width, location.Height)
            {
                TextHeight = Owner.TextFontDefault.Height
            };
            if (Owner.ScreenSize != Point.Zero)
                Layout(windowLayout);
            windowLayout.Initialize(Owner);
            this.windowLayout = windowLayout;
        }

        protected virtual ControlLayout Layout(ControlLayout layout)
        {
            // Pad window by 4px, add caption and space between to content area.
            ControlLayoutVertical content = layout?.AddLayoutOffset(4, 4, 4, 4).AddLayoutVertical() ?? throw new ArgumentNullException(nameof(layout));
            content.Add(new Label(content.RemainingWidth, Owner.TextFontDefault.Height, caption, LabelAlignment.Center));
            content.AddSpace(0, 5);
            return content;
        }

        public virtual void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime, bool updateFull)
        {
            if (Visible)
                PrepareFrame(elapsedTime, updateFull);
        }

        public override void Draw()
        {
            if (windowVertexBuffer == null)
            {
                // Edges/corners are 32px (1/4th image size).
                int gp = 32 - BaseFontSize + Owner.TextFontDefault.Height;
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
                windowVertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionTexture), vertexData.Length, BufferUsage.WriteOnly);
                windowVertexBuffer.SetData(vertexData);
            }
            if (windowIndexBuffer == null)
            {
                short[] indexData = new short[] {
                    0, 4, 1, 5, 2, 6, 3, 7,
                    11, 6, 10, 5, 9, 4, 8,
                    12, 9, 13, 10, 14, 11, 15,
                };
                windowIndexBuffer = new IndexBuffer(graphicsDevice, typeof(short), indexData.Length, BufferUsage.WriteOnly);
                windowIndexBuffer.SetData(indexData);
            }

            graphicsDevice.SetVertexBuffer(windowVertexBuffer);
            graphicsDevice.Indices = windowIndexBuffer;
            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleStrip, 0, 0, 20);
        }

        public virtual void PrepareFrame(in ElapsedTime elapsedTime, bool updateFull)
        {
        }

        public virtual void Draw(SpriteBatch spriteBatch)
        {
            windowLayout.Draw(spriteBatch, Location.Location);
        }

        public void MousePressed(Point position, KeyModifiers keyModifiers)
        {
            windowLayout.HandleMousePressed(new WindowMouseEvent(this, position, true, keyModifiers));
        }

        public void MouseDown(Point position, KeyModifiers keyModifiers)
        {
            windowLayout.HandleMouseDown(new WindowMouseEvent(this, position, true, keyModifiers));
        }

        public void MouseReleased(Point position, KeyModifiers keyModifiers)
        {
            windowLayout.HandleMouseReleased(new WindowMouseEvent(this, position, false, keyModifiers));
            dragged = false;
        }

        public void MouseDrag(Point position, Vector2 delta, KeyModifiers keyModifiers)
        {
            dragged = true;
            windowLayout.HandleMouseMove(new WindowMouseEvent(this, position, delta, keyModifiers));
        }

        public void MouseScroll(Point position, int delta, KeyModifiers keyModifiers)
        {
            windowLayout.HandleMouseScroll(new WindowMouseEvent(this, position, delta, keyModifiers));
        }

        public virtual void Mark()
        {
        }

        protected virtual void Dispose(bool disposing)
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
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Window()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class WindowMouseEvent
    {
        public Point MousePosition { get; }
        public int MouseWheelDelta { get; }
        public Point Movement { get; }
        public bool ButtonDown { get; }
        public KeyModifiers KeyModifiers { get; }

        public WindowMouseEvent(Window window, Point mouseLocation, int mouseWheelDelta, KeyModifiers modifiers)
        {
            MousePosition = mouseLocation - window?.Location.Location ?? throw new ArgumentNullException(nameof(window));
            MouseWheelDelta = mouseWheelDelta;
            KeyModifiers = modifiers;
        }

        public WindowMouseEvent(Window window, Point mouseLocation, bool buttonDown, KeyModifiers modifiers)
        {
            MousePosition = mouseLocation - window?.Location.Location ?? throw new ArgumentNullException(nameof(window));
            ButtonDown = buttonDown;
            KeyModifiers = modifiers;
        }

        public WindowMouseEvent(Window window, Point mouseLocation, Vector2 delta, KeyModifiers modifiers)
        {
            MousePosition = mouseLocation - window?.Location.Location ?? throw new ArgumentNullException(nameof(window));
            Movement = delta.ToPoint();
            ButtonDown = true;
            KeyModifiers = modifiers;
        }
    }

    internal class WindowControlLayout : ControlLayout
    {
        public readonly Window Window;

        private bool capturedForDragging;

        public WindowControlLayout(Window window, int width, int height)
            : base(0, 0, width, height)
        {
            Window = window;
        }

        internal override bool HandleMousePressed(WindowMouseEvent e)
        {
            if (base.HandleMousePressed(e))
                return true;

            capturedForDragging = true;
            // prevent from dragging when clicking on vertical scrollbar
            if (MathHelper.Distance(base.RemainingWidth, e.MousePosition.X) < 20)
                return false;

            // prevent from dragging when clicking on horizontal scrollbar
            if (MathHelper.Distance(base.RemainingHeight, e.MousePosition.Y) < 20)
                return false;

            return true;
        }

        internal override bool HandleMouseReleased(WindowMouseEvent e)
        {
            if (base.HandleMouseReleased(e))
                return true;
            capturedForDragging = false;
            return true;
        }

        internal override bool HandleMouseMove(WindowMouseEvent e)
        {
            if (base.HandleMouseMove(e))
                return true;

            if (capturedForDragging)
                Window.MoveTo(Window.Location.X + e.Movement.X, Window.Location.Y + e.Movement.Y);
            return true;
        }

        internal override bool HandleMouseScroll(WindowMouseEvent e)
        {
            if (base.HandleMouseScroll(e))
                return true;
            return true;
        }
    }
}
