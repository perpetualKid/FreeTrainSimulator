
using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls.Layout
{
    public abstract class ScrollboxControlLayout : ControlLayout
    {
        protected const int mouseClickScrollDelay = 100;

        public ControlLayout Client { get; private protected set; }

        private protected int scrollPosition;

        protected abstract bool ThumbVisible { get; }

        private protected readonly RasterizerState scissorTestRasterizer = new RasterizerState { ScissorTestEnable = true };

        private protected static readonly Rectangle topButtonClipping = new Rectangle(0, 0, 16, 16);
        private protected static readonly Rectangle thumbClipping = new Rectangle(1 * 16, 0, 16, 16);
        private protected static readonly Rectangle gutterClipping = new Rectangle(2 * 16, 0, 16, 16);
        private protected static readonly Rectangle bottomButtonClipping = new Rectangle(3 * 16, 0, 16, 16);
        private protected static readonly Vector2 rotateOrigin = new Vector2(0, 16);
        private protected static int size;

        protected ScrollboxControlLayout(WindowBase window, int x, int y, int width, int height) : base(window, x, y, width, height)
        {
            size = (int)(16 * Window.Owner.DpiScaling);
        }

        protected abstract int ContentScrollLength { get; }

        protected abstract int ScrollbarScrollLength {get;}

        protected abstract void SetScrollPosition(int position);

    }

    public class VerticalScrollboxControlLayout : ScrollboxControlLayout
    {
        public VerticalScrollboxControlLayout(WindowBase window, int width, int height) :
            base(window, 0, 0, width, height)
        {
            Client = AddLayoutVertical(RemainingWidth - size - SeparatorPadding * 2);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            int thumbPosition = ScrollbarScrollLength * scrollPosition / ContentScrollLength;
            // Top button
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - size, offset.Y + Bounds.Y, size, size), topButtonClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Top gutter
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - size, offset.Y + Bounds.Y + size, thumbPosition, size), gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Thumb
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - size, offset.Y + Bounds.Y + size + thumbPosition, size, size), ThumbVisible ? thumbClipping : gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Bottom gutter
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - size, offset.Y + Bounds.Y + 2 * size + thumbPosition, Bounds.Height - 3 * size - thumbPosition, size), gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Bottom button
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - size, offset.Y + Bounds.Y + Bounds.Height - size, size, size), bottomButtonClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);

            RasterizerState rasterizer = spriteBatch.GraphicsDevice.RasterizerState;
            Rectangle scissorRectangle = spriteBatch.GraphicsDevice.ScissorRectangle;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, scissorTestRasterizer);
            spriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle(offset.X + Bounds.X, offset.Y + Bounds.Y, Client.Bounds.Width, Client.Bounds.Height);
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
            if (e.MousePosition.X > Bounds.Right - size && e.MousePosition.X < Bounds.Right &&
                e.MousePosition.Y > Bounds.Top + size && e.MousePosition.Y < Bounds.Bottom - size
                    && e.Movement != Point.Zero)
            {
                Window.CapturedControl = this;
                SetScrollPosition(scrollPosition + e.Movement.Y * ContentScrollLength / ScrollbarScrollLength);
                return true;
            }
            return Window.CapturedControl == this || base.HandleMouseDrag(e);
        }

        protected override int ContentScrollLength => Client.CurrentTop - Client.Bounds.Height;

        protected override int ScrollbarScrollLength => Client.Bounds.Height - 3 * size;

        protected override bool ThumbVisible => Client.CurrentTop > Client.Bounds.Height;

        protected override void SetScrollPosition(int position)
        {
            position = MathHelper.Clamp(position, 0, ContentScrollLength);
            Client.MoveBy(0, scrollPosition - position);
            scrollPosition = position;
        }

        private DateTime ticks;

        private bool HandleMouseButton(WindowMouseEvent e)
        {
            if (ticks.AddMilliseconds(mouseClickScrollDelay) > DateTime.UtcNow)
                return true;
            ticks = DateTime.UtcNow;
            if (e.MousePosition.X > Bounds.Right - size)
            {
                // Mouse down occured within the scrollbar.
                if (e.MousePosition.Y < Bounds.Top + size)
                    // Mouse down occured on top button.
                    SetScrollPosition(scrollPosition - Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.Y < Bounds.Top + size + ScrollbarScrollLength * scrollPosition / ContentScrollLength)
                    // Mouse down occured on top gutter.
                    SetScrollPosition(scrollPosition - Client.Bounds.Height);
                else if (e.MousePosition.Y > Bounds.Bottom - size)
                    // Mouse down occured on bottom button.
                    SetScrollPosition(scrollPosition + Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.Y > Bounds.Top + 2 * size + ScrollbarScrollLength * scrollPosition / ContentScrollLength)
                    // Mouse down occured on bottom gutter.
                    SetScrollPosition(scrollPosition + Client.Bounds.Height);
                return true;
            }
            return false;
        }
    }

    public class HorizontalScrollboxControlLayout : ScrollboxControlLayout
    {
        public HorizontalScrollboxControlLayout(WindowBase window, int width, int height) : 
            base(window, 0, 0, width, height)
        {
            Client = AddLayoutHorizontal(RemainingHeight - size - SeparatorPadding * 2);
            SetScrollPosition(200);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            int thumbPosition = ScrollbarScrollLength * scrollPosition / ContentScrollLength;
            // Left button
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X, offset.Y + Bounds.Y + Bounds.Height - size, size, size), topButtonClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            // Left gutter
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + size, offset.Y + Bounds.Y + Bounds.Height - size, thumbPosition, size), gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            // Thumb
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + size + thumbPosition, offset.Y + Bounds.Y + Bounds.Height - size, size, size), ThumbVisible ? thumbClipping : gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            // Right gutter
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + 2 * size + thumbPosition, offset.Y + Bounds.Y + Bounds.Height - size, Bounds.Width - 3 * size - thumbPosition, size), gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            // Right button
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - size, offset.Y + Bounds.Y + Bounds.Height - size, size, size), bottomButtonClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);

            RasterizerState rasterizer = spriteBatch.GraphicsDevice.RasterizerState;
            Rectangle scissorRectangle = spriteBatch.GraphicsDevice.ScissorRectangle;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, scissorTestRasterizer);
            spriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle(offset.X + Bounds.X, offset.Y + Bounds.Y, Client.Bounds.Width, Client.Bounds.Height);
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
            if (e.MousePosition.X > Bounds.Left + size && e.MousePosition.X < Bounds.Right - size &&
                e.MousePosition.Y > Bounds.Bottom - size && e.MousePosition.Y < Bounds.Bottom
                    && e.Movement != Point.Zero)
            {
                Window.CapturedControl = this;
                SetScrollPosition(scrollPosition + e.Movement.X * ContentScrollLength / ScrollbarScrollLength);
                return true;
            }
            return Window.CapturedControl == this || base.HandleMouseDrag(e);
        }


        protected override int ContentScrollLength => Client.CurrentLeft - Client.Bounds.Width;

        protected override int ScrollbarScrollLength => Client.Bounds.Width - 3 * size;

        protected override bool ThumbVisible => Client.CurrentLeft > Client.Bounds.Width;

        protected override void SetScrollPosition(int position)
        {
            position = MathHelper.Clamp(position, 0, ContentScrollLength);
            Client.MoveBy(scrollPosition - position, 0);
            scrollPosition = position;
        }

        private DateTime ticks;

        private bool HandleMouseButton(WindowMouseEvent e)
        {
            if (ticks.AddMilliseconds(mouseClickScrollDelay) > DateTime.UtcNow)
                return true;
            ticks = DateTime.UtcNow;
            if (e.MousePosition.Y > Bounds.Height - size)
            {
                // Mouse down occured within the scrollbar.
                if (e.MousePosition.X < Bounds.Left + size)
                    // Mouse down occured on left button.
                    SetScrollPosition(scrollPosition - Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.X < Bounds.Left + size + ScrollbarScrollLength * scrollPosition / ContentScrollLength)
                    // Mouse down occured on left gutter.
                    SetScrollPosition(scrollPosition - Client.Bounds.Width);
                else if (e.MousePosition.X > Bounds.Width - size)
                    // Mouse down occured on right button.
                    SetScrollPosition(scrollPosition + Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.X > Bounds.Left + 2 * size + ScrollbarScrollLength * scrollPosition / ContentScrollLength)
                    // Mouse down occured on right gutter.
                    SetScrollPosition(scrollPosition + Client.Bounds.Width);
                return true;
            }
            return false;
        }

    }
}
