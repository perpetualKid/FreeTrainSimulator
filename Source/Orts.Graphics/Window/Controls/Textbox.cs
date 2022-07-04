
using System;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics.Xna;

using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace Orts.Graphics.Window.Controls
{
    public class TextBox : WindowControl
    {
        private const int mouseClickScrollDelay = 100;

        private string[] lines;

        private readonly System.Drawing.Font font;
        private readonly RasterizerState scissorTestRasterizer = new RasterizerState { ScissorTestEnable = true };
        private readonly TextTextureResourceHolder textureHolder;

        private bool wordWrap;
        private Point alignmentOffset;
        private Rectangle? clippingRectangle;
        private static int scrollbarSize;

        private static readonly Rectangle topButtonClipping = new Rectangle(0, 0, 16, 16);
        private static readonly Rectangle thumbClipping = new Rectangle(1 * 16, 0, 16, 16);
        private static readonly Rectangle gutterClipping = new Rectangle(2 * 16, 0, 16, 16);
        private static readonly Rectangle bottomButtonClipping = new Rectangle(3 * 16, 0, 16, 16);
        private static readonly Vector2 rotateOrigin = new Vector2(0, 16);

        private int verticalScrollPosition;
        private int horizontalScrollPosition;

        private int horizontalContentSize;
        private int verticalContentSize;
        private bool horizontalThumbVisible;
        private bool verticalThumbVisible;

        private int visibleLines;
        private int lineHeight;

        public Color TextColor { get; set; }

        public void SetText(string text)
        {
            InitializeText(text); 
        }

        public bool WordWrap
        {
            get => wordWrap;
            set { wordWrap = value; Initialize(); }
        }

        public HorizontalAlignment Alignment { get; }

        public TextBox(WindowBase window, int x, int y, int width, int height, string text, HorizontalAlignment alignment, System.Drawing.Font font, Color color)
            : base(window, x, y, width, height)
        {
            Alignment = alignment;
            TextColor = color;
            this.font = font ?? window?.Owner.TextFontDefault;
            scrollbarSize = (int)(16 * Window.Owner.DpiScaling);
            textureHolder = new TextTextureResourceHolder(Window.Owner.Game, 10);
            InitializeText(text);
        }

        public TextBox(WindowBase window, int x, int y, int width, int height, string text)
            : this(window, x, y, width, height, text, HorizontalAlignment.Left, null, Color.White)
        {
        }

        public TextBox(WindowBase window, int width, int height, string text, HorizontalAlignment align)
            : this(window, 0, 0, width, height, text, align, null, Color.White)
        {
        }

        public TextBox(WindowBase window, int width, int height, string text)
            : this(window, 0, 0, width, height, text, HorizontalAlignment.Left, null, Color.White)
        {
        }

        public TextBox(WindowBase window, int width, int height, string text, HorizontalAlignment align, Color color)
            : this(window, 0, 0, width, height, text, align, null, color)
        {
        }

        internal override void Initialize()
        {
            base.Initialize();
            //RenderText(Text);
            clippingRectangle = null;
            //switch (Alignment)
            //{
            //    case HorizontalAlignment.Left:
            //        alignmentOffset = Point.Zero;
            //        if (texture.Width > Bounds.Width)
            //            clippingRectangle = new Rectangle(Point.Zero, Bounds.Size - new Point(scrollbarSize, scrollbarSize));
            //        break;
            //    case HorizontalAlignment.Center:
            //        alignmentOffset = new Point((Bounds.Width - System.Math.Min(texture.Width, Bounds.Width)) / 2, 0);
            //        if (texture.Width > Bounds.Width)
            //            clippingRectangle = new Rectangle(new Point((texture.Width - Bounds.Width) / 2, 0), Bounds.Size - new Point(scrollbarSize, scrollbarSize));
            //        break;
            //    case HorizontalAlignment.Right:
            //        alignmentOffset = new Point(Bounds.Width - System.Math.Min(texture.Width, Bounds.Width), 0);
            //        if (texture.Width > Bounds.Width)
            //            clippingRectangle = new Rectangle(new Point(texture.Width - Bounds.Width, 0), Bounds.Size - new Point(scrollbarSize, scrollbarSize));
            //        break;
            //}
        }

        internal override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            int thumbPosition = (Bounds.Width - 4 * scrollbarSize) * horizontalScrollPosition / horizontalContentSize;
            // Left button
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, scrollbarSize, scrollbarSize), topButtonClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            // Left gutter
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + scrollbarSize, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, thumbPosition, scrollbarSize), gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            // Thumb
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + scrollbarSize + thumbPosition, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, scrollbarSize, scrollbarSize), horizontalThumbVisible ? thumbClipping : gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            // Right gutter
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + 2 * scrollbarSize + thumbPosition, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, Bounds.Width - 4 * scrollbarSize - thumbPosition, scrollbarSize), gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            // Right button
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - 2 * scrollbarSize, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, scrollbarSize, scrollbarSize), bottomButtonClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);

            thumbPosition = (Bounds.Height - 4 * scrollbarSize) * verticalScrollPosition / verticalContentSize;
            // Top button
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y, scrollbarSize, scrollbarSize), topButtonClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Top gutter
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + scrollbarSize, thumbPosition, scrollbarSize), gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Thumb
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + scrollbarSize + thumbPosition, scrollbarSize, scrollbarSize), verticalThumbVisible ? thumbClipping : gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Bottom gutter
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + 2 * scrollbarSize + thumbPosition, Bounds.Height - 4 * scrollbarSize - thumbPosition, scrollbarSize), gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Bottom button
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + Bounds.Height - 2 * scrollbarSize, scrollbarSize, scrollbarSize), bottomButtonClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);

            RasterizerState rasterizer = spriteBatch.GraphicsDevice.RasterizerState;
            Rectangle scissorRectangle = spriteBatch.GraphicsDevice.ScissorRectangle;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, scissorTestRasterizer);
            spriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle(offset.X + Bounds.X, offset.Y + Bounds.Y, Bounds.Width - 4 - scrollbarSize, Bounds.Height - 4 -scrollbarSize);
            spriteBatch.GraphicsDevice.RasterizerState = scissorTestRasterizer;

            int lineNumber = verticalScrollPosition / lineHeight;
            for (int i = lineNumber; i < lineNumber + visibleLines && i < lines.Length; i++)
            {
                Texture2D lineTexture = textureHolder.PrepareResource(lines[i], font);
                spriteBatch.Draw(lineTexture, (Bounds.Location + offset + alignmentOffset - new Point(horizontalScrollPosition, verticalScrollPosition) + new Point(0, i * lineHeight)).ToVector2(), clippingRectangle, TextColor, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
            }
            base.Draw(spriteBatch, offset);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, rasterizer, null);
            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRectangle;
        }

        private void InitializeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            { 
                lines = Array.Empty<string>();
                return;
            }
            int longestLineIndex = -1;
            int lineLength = 0;
            lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > lineLength)
                {
                    lineLength = lines[i].Length;
                    longestLineIndex = i;
                }
            }
            if (wordWrap)
            {
            }
            Texture2D texture = textureHolder.PrepareResource(lines[longestLineIndex], font);
            lineHeight = texture.Height;
            horizontalContentSize = texture.Width - Bounds.Width + scrollbarSize + 4;
            horizontalThumbVisible = horizontalContentSize > 0;//Bounds.Width; - scrollbarSize;
            verticalContentSize = lines.Length * lineHeight - Bounds.Height + scrollbarSize + 4;
            verticalThumbVisible = verticalContentSize > 0;//Bounds.Height - scrollbarSize;
            visibleLines = Bounds.Height / lineHeight + 1;
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
                SetVerticalScrollPosition(verticalScrollPosition - e.MouseWheelDelta);
                return true;
            }
            return base.HandleMouseScroll(e);
        }

        internal override bool HandleMouseDrag(WindowMouseEvent e)
        {
            if (e.MousePosition.X > Bounds.Right - scrollbarSize && e.MousePosition.X < Bounds.Right &&
                e.MousePosition.Y > Bounds.Top + scrollbarSize && e.MousePosition.Y < Bounds.Bottom - scrollbarSize
                    && e.Movement != Point.Zero)
            {
                Window.CapturedControl = this;
                SetVerticalScrollPosition(verticalScrollPosition + e.Movement.Y * verticalContentSize / (Bounds.Height - 4 * scrollbarSize));
                return true;
            }
            else if (e.MousePosition.X > Bounds.Left + scrollbarSize && e.MousePosition.X < Bounds.Right - scrollbarSize &&
                e.MousePosition.Y > Bounds.Bottom - scrollbarSize && e.MousePosition.Y < Bounds.Bottom
                    && e.Movement != Point.Zero)
            {
                Window.CapturedControl = this;
                SetHorizontalScrollPosition(horizontalScrollPosition + e.Movement.X * horizontalContentSize / (Bounds.Width - 4 * scrollbarSize));
                return true;
            }
            return Window.CapturedControl == this || base.HandleMouseDrag(e);
        }

        private void SetHorizontalScrollPosition(int position)
        {
            position = MathHelper.Clamp(position, 0, horizontalContentSize);
//            Client.MoveBy(scrollPosition - position, 0);
            horizontalScrollPosition = position;
        }

        private void SetVerticalScrollPosition(int position)
        {
            position = MathHelper.Clamp(position, 0, verticalContentSize);
            //            Client.MoveBy(scrollPosition - position, 0);
            verticalScrollPosition = position;
        }

        private DateTime ticks;

        private bool HandleMouseButton(WindowMouseEvent e)
        {
            if (ticks.AddMilliseconds(mouseClickScrollDelay) > DateTime.UtcNow)
                return true;
            ticks = DateTime.UtcNow;
            if (e.MousePosition.Y > Bounds.Bottom - scrollbarSize && e.MousePosition.X < Bounds.Right - scrollbarSize)//horizontal scrollbar
            {
                // Mouse down occured within the scrollbar.
                if (e.MousePosition.X < Bounds.Left + scrollbarSize)
                    // Mouse down occured on left button.
                    SetHorizontalScrollPosition(horizontalScrollPosition - Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.X < Bounds.Left + scrollbarSize + (Bounds.Width - 4 * scrollbarSize) * horizontalScrollPosition / horizontalContentSize)
                    // Mouse down occured on left gutter.
                    SetHorizontalScrollPosition(horizontalScrollPosition - Bounds.Width);
                else if (e.MousePosition.X > Bounds.Width - 2 * scrollbarSize)
                    // Mouse down occured on right button.
                    SetHorizontalScrollPosition(horizontalScrollPosition + Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.X > Bounds.Left + 2 * scrollbarSize + (Bounds.Width - 4 * scrollbarSize) * horizontalScrollPosition / horizontalContentSize)
                    // Mouse down occured on right gutter.
                    SetHorizontalScrollPosition(horizontalScrollPosition + Bounds.Width);
                return true;
            }
            else if (e.MousePosition.X > Bounds.Right - scrollbarSize && e.MousePosition.Y < Bounds.Bottom - scrollbarSize)
            {
                // Mouse down occured within the scrollbar.
                if (e.MousePosition.Y < Bounds.Top + scrollbarSize)
                    // Mouse down occured on top button.
                    SetVerticalScrollPosition(verticalScrollPosition - Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.Y < Bounds.Top + scrollbarSize + (Bounds.Height - 4 * scrollbarSize) * verticalScrollPosition / verticalContentSize)
                    // Mouse down occured on top gutter.
                    SetVerticalScrollPosition(verticalScrollPosition - Bounds.Height);
                else if (e.MousePosition.Y > Bounds.Bottom - 2 * scrollbarSize)
                    // Mouse down occured on bottom button.
                    SetVerticalScrollPosition(verticalScrollPosition + Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.Y > Bounds.Top + 2 * scrollbarSize + (Bounds.Height - 4 * scrollbarSize) * verticalScrollPosition / verticalContentSize)
                    // Mouse down occured on bottom gutter.
                    SetVerticalScrollPosition(verticalScrollPosition + Bounds.Height);
                return true;
            }

            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                scissorTestRasterizer?.Dispose();
                textureHolder?.Dispose();
            }
            base.Dispose(disposing);
        }

    }
}
