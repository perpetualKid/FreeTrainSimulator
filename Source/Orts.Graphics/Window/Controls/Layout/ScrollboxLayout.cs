
using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Window.Controls.Layout
{
    public abstract class ScrollboxControlLayout : ControlLayout
    {
        public ControlLayout Client { get; protected set; }
        private protected int scrollPosition;
        private protected static readonly RasterizerState scissorTestRasterizer = new RasterizerState { ScissorTestEnable = true };

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

        protected abstract int ScrollSize { get; }
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
            int thumbOffset = (Bounds.Height - 3 * size) * scrollPosition / ScrollSize;

            // Top button
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - size, offset.Y + Bounds.Y, size, size), topButtonClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Top gutter
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - size, offset.Y + Bounds.Y + size, thumbOffset, size), gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Thumb
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - size, offset.Y + Bounds.Y + size + thumbOffset, size, size), ScrollSize > 0 ? thumbClipping : gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Bottom gutter
            spriteBatch.Draw(Window.Owner.scrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - size, offset.Y + Bounds.Y + 2 * size + thumbOffset, Bounds.Height - 3 * size - thumbOffset, size), gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
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
            return base.HandleMouseDown(e);
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

        protected override int ScrollSize => Client.CurrentTop - Bounds.Height;

        protected override void SetScrollPosition(int position)
        {
            position = MathHelper.Clamp(position, 0, ScrollSize);
            Client.MoveBy(0, scrollPosition - position);
            scrollPosition = position;
        }

    }
}
