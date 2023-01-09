
using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls.Layout
{
    public abstract class ScrollboxControlLayout : ControlLayout
    {
        protected const int mouseClickScrollDelay = 100;
        private protected long scrollDelayTicks;

        public ControlLayout Client { get; private protected set; }

        private protected int scrollPosition;

        protected abstract bool ThumbVisible { get; }

        private protected readonly RasterizerState scissorTestRasterizer = new RasterizerState { ScissorTestEnable = true };

        private protected static readonly Rectangle topButtonClipping = new Rectangle(0, 0, 16, 16);
        private protected static readonly Rectangle thumbClipping = new Rectangle(1 * 16, 0, 16, 16);
        private protected static readonly Rectangle gutterClipping = new Rectangle(2 * 16, 0, 16, 16);
        private protected static readonly Rectangle bottomButtonClipping = new Rectangle(3 * 16, 0, 16, 16);
        private protected static readonly Vector2 rotateOrigin = new Vector2(0, 16);
        private protected static int scrollbarSize;

        protected ScrollboxControlLayout(FormBase window, int x, int y, int width, int height) : base(window, x, y, width, height)
        {
            scrollbarSize = (int)(16 * Window.Owner.DpiScaling);
        }

        protected abstract int ContentScrollLength { get; }

        protected abstract int ScrollbarScrollLength { get; }

        protected abstract void SetScrollPosition(int position);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                scissorTestRasterizer?.Dispose();
            }
            base.Dispose(disposing);
        }

        public override void Clear()
        {
            // resetting the client control's position so they can be re-added in place
            foreach(WindowControl control in Client.Controls)
            {
                control.MoveBy(-control.Bounds.Left, -control.Bounds.Top);
            }
            Client.Clear();
            SetScrollPosition(0);
        }

        public void UpdateContent()
        {
            Initialize();
        }
    }

    public class VerticalScrollboxControlLayout : ScrollboxControlLayout
    {
        private readonly int usableHeight;

        public VerticalScrollboxControlLayout(FormBase window, int width, int height) :
            base(window, 0, 0, width, height)
        {
            Client = AddLayoutVertical(RemainingWidth - scrollbarSize - SeparatorPadding * 2);
            usableHeight = Client.Bounds.Height;
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            int thumbPosition = ScrollbarScrollLength * scrollPosition / ContentScrollLength;
            // Top button
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y, scrollbarSize, scrollbarSize), topButtonClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Top gutter
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + scrollbarSize, thumbPosition, scrollbarSize), gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Thumb
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + scrollbarSize + thumbPosition, scrollbarSize, scrollbarSize), ThumbVisible ? thumbClipping : gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Bottom gutter
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + 2 * scrollbarSize + thumbPosition, Bounds.Height - 3 * scrollbarSize - thumbPosition, scrollbarSize), gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Bottom button
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, scrollbarSize, scrollbarSize), bottomButtonClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);

            RasterizerState rasterizer = spriteBatch.GraphicsDevice.RasterizerState;
            Rectangle scissorRectangle = spriteBatch.GraphicsDevice.ScissorRectangle;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, scissorTestRasterizer);
            spriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle(offset.X + Bounds.X, offset.Y + Bounds.Y, Client.Bounds.Width, usableHeight);
            spriteBatch.GraphicsDevice.RasterizerState = scissorTestRasterizer;
            base.Draw(spriteBatch, offset);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, rasterizer, null);
            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRectangle;
        }

        internal override bool HandleMouseDown(WindowMouseEvent e)
        {
            return HandleMouseButton(e) || base.HandleMouseDown(e);
        }

        internal override bool HandleMouseClicked(WindowMouseEvent e)
        {
            return HandleMouseButton(e) || base.HandleMouseDown(e);
        }

        internal override bool HandleMouseReleased(WindowMouseEvent e)
        {
            return base.HandleMouseReleased(e);
        }

        internal override bool HandleMouseScroll(WindowMouseEvent e)
        {
            if (e.MouseWheelDelta != 0)
            {
                SetScrollPosition(scrollPosition - e.MouseWheelDelta);
                return true;
            }
            return base.HandleMouseScroll(e);
        }

        internal override bool HandleMouseDrag(WindowMouseEvent e)
        {
            if (e.MousePosition.X > Bounds.Right - scrollbarSize && e.MousePosition.X < Bounds.Right &&
                e.MousePosition.Y > Bounds.Top && e.MousePosition.Y < Bounds.Bottom
                    && e.Movement != Point.Zero)
            {
                Window.CapturedControl = this;
                SetScrollPosition((int)(ContentScrollLength * (e.MousePosition.Y - Bounds.Top - scrollbarSize) / (Bounds.Height - scrollbarSize * 2.0)));
                return true;
            }
            return Window.CapturedControl == this || base.HandleMouseDrag(e);
        }

        protected override int ContentScrollLength => Client.CurrentTop - usableHeight;

        protected override int ScrollbarScrollLength => usableHeight- 3 * scrollbarSize;

        protected override bool ThumbVisible => Client.CurrentTop > usableHeight;

        protected override void SetScrollPosition(int position)
        {
            position = MathHelper.Clamp(position, 0, ContentScrollLength);
            Client.MoveBy(0, scrollPosition - position);
            scrollPosition = position;
        }

        private bool HandleMouseButton(WindowMouseEvent e)
        {
            if (Environment.TickCount64 < scrollDelayTicks)
                return true;
            scrollDelayTicks = Environment.TickCount64 + mouseClickScrollDelay;
            if (e.MousePosition.X > Bounds.Right - scrollbarSize && Window.CapturedControl == null)
            {
                double mousePositionInScrollbar = (e.MousePosition.Y - Bounds.Top - scrollbarSize) / (Bounds.Height - scrollbarSize * 2.0);

                // Mouse down occured within the scrollbar.
                if (e.MousePosition.Y < Bounds.Top + scrollbarSize)
                    // Mouse down occured on top button.
                    SetScrollPosition(scrollPosition - Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.Y < Bounds.Top + scrollbarSize + ScrollbarScrollLength * scrollPosition / ContentScrollLength)
                    // Mouse down occured on top gutter.
                    SetScrollPosition(Math.Max(scrollPosition - usableHeight, (int)(ContentScrollLength * mousePositionInScrollbar)));
                else if (e.MousePosition.Y > Bounds.Bottom - scrollbarSize)
                    // Mouse down occured on bottom button.
                    SetScrollPosition(scrollPosition + Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.Y > Bounds.Top + scrollbarSize * 2 + ScrollbarScrollLength * scrollPosition / ContentScrollLength)
                    // Mouse down occured on bottom gutter.
                    SetScrollPosition(Math.Min(scrollPosition + usableHeight, (int)(ContentScrollLength * mousePositionInScrollbar)));
                return true;
            }
            return false;
        }
    }

    public class HorizontalScrollboxControlLayout : ScrollboxControlLayout
    {
        private readonly int usableWidth;

        public HorizontalScrollboxControlLayout(FormBase window, int width, int height) :
            base(window, 0, 0, width, height)
        {
            Client = AddLayoutHorizontal(RemainingHeight - scrollbarSize - SeparatorPadding * 2);
            usableWidth = Client.Bounds.Width;
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            int thumbPosition = ScrollbarScrollLength * scrollPosition / ContentScrollLength;
            // Left button
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, scrollbarSize, scrollbarSize), topButtonClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            // Left gutter
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + scrollbarSize, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, thumbPosition, scrollbarSize), gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            // Thumb
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + scrollbarSize + thumbPosition, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, scrollbarSize, scrollbarSize), ThumbVisible ? thumbClipping : gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            // Right gutter
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + 2 * scrollbarSize + thumbPosition, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, Bounds.Width - 3 * scrollbarSize - thumbPosition, scrollbarSize), gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            // Right button
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, scrollbarSize, scrollbarSize), bottomButtonClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);

            RasterizerState rasterizer = spriteBatch.GraphicsDevice.RasterizerState;
            Rectangle scissorRectangle = spriteBatch.GraphicsDevice.ScissorRectangle;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, scissorTestRasterizer);
            spriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle(offset.X + Bounds.X, offset.Y + Bounds.Y, usableWidth, Client.Bounds.Height);
            spriteBatch.GraphicsDevice.RasterizerState = scissorTestRasterizer;
            base.Draw(spriteBatch, offset);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, rasterizer, null);
            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRectangle;
        }

        internal override bool HandleMouseDown(WindowMouseEvent e)
        {
            return HandleMouseButton(e) || base.HandleMouseDown(e);
        }

        internal override bool HandleMouseClicked(WindowMouseEvent e)
        {
            return HandleMouseButton(e) || base.HandleMouseDown(e);
        }

        internal override bool HandleMouseReleased(WindowMouseEvent e)
        {
            return base.HandleMouseReleased(e);
        }

        internal override bool HandleMouseScroll(WindowMouseEvent e)
        {
            if (e.MouseWheelDelta != 0)
            {
                SetScrollPosition(scrollPosition - e.MouseWheelDelta);
                return true;
            }
            return base.HandleMouseScroll(e);
        }

        internal override bool HandleMouseDrag(WindowMouseEvent e)
        {
            if (e.MousePosition.X > Bounds.Left && e.MousePosition.X < Bounds.Right &&
                e.MousePosition.Y > Bounds.Bottom - scrollbarSize && e.MousePosition.Y < Bounds.Bottom
                    && e.Movement != Point.Zero)
            {
                Window.CapturedControl = this;
                SetScrollPosition((int)(ContentScrollLength * (e.MousePosition.X - Bounds.Left - scrollbarSize) / (Bounds.Width - scrollbarSize * 2.0)));
                return true;
            }
            return Window.CapturedControl == this || base.HandleMouseDrag(e);
        }


        protected override int ContentScrollLength => Client.CurrentLeft - usableWidth;

        protected override int ScrollbarScrollLength => usableWidth - 3 * scrollbarSize;

        protected override bool ThumbVisible => Client.CurrentLeft > usableWidth;

        protected override void SetScrollPosition(int position)
        {
            position = MathHelper.Clamp(position, 0, ContentScrollLength);
            Client.MoveBy(scrollPosition - position, 0);
            scrollPosition = position;
        }

        private bool HandleMouseButton(WindowMouseEvent e)
        {
            if (Environment.TickCount64 < scrollDelayTicks)
                return true;
            scrollDelayTicks = Environment.TickCount64 + mouseClickScrollDelay;
            if (e.MousePosition.Y > Bounds.Bottom - scrollbarSize && Window.CapturedControl == null)
            {
                double mousePositionInScrollbar = (e.MousePosition.X - Bounds.Left - scrollbarSize) / (Bounds.Width - scrollbarSize * 2.0);
                // Mouse down occured within the scrollbar.
                if (e.MousePosition.X < Bounds.Left + scrollbarSize)
                    // Mouse down occured on left button.
                    SetScrollPosition(scrollPosition - Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.X < Bounds.Left + scrollbarSize + ScrollbarScrollLength * scrollPosition / ContentScrollLength)
                    // Mouse down occured on left gutter.
                    SetScrollPosition(Math.Max(scrollPosition - usableWidth, (int)(ContentScrollLength * mousePositionInScrollbar)));
                else if (e.MousePosition.X > Bounds.Width - scrollbarSize)
                    // Mouse down occured on right button.
                    SetScrollPosition(scrollPosition + Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.X > Bounds.Left + 2 * scrollbarSize + ScrollbarScrollLength * scrollPosition / ContentScrollLength)
                    // Mouse down occured on right gutter.
                    SetScrollPosition(Math.Min(scrollPosition + usableWidth, (int)(ContentScrollLength * mousePositionInScrollbar)));
                return true;
            }
            return false;
        }

    }
}
