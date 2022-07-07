
using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls
{
    public class TextBox : WindowControl
    {
        private static readonly char[] whitespaces = { ' ', '\t', '\r', '\n' };
        private const int mouseClickScrollDelay = 100;

        private string[] lines;
        private string text;

        private readonly System.Drawing.Font font;
        private readonly RasterizerState scissorTestRasterizer = new RasterizerState { ScissorTestEnable = true };
        private readonly TextTextureResourceHolder textureHolder;

        private bool wordWrap;
        private static int scrollbarSize;

        private static readonly Rectangle topButtonClipping = new Rectangle(0, 0, 16, 16);
        private static readonly Rectangle thumbClipping = new Rectangle(1 * 16, 0, 16, 16);
        private static readonly Rectangle gutterClipping = new Rectangle(2 * 16, 0, 16, 16);
        private static readonly Rectangle bottomButtonClipping = new Rectangle(3 * 16, 0, 16, 16);
        private static readonly Vector2 rotateOrigin = new Vector2(0, 16);

        private int verticalScrollPosition;
        private int horizontalScrollPosition;

        private int horizontalScrollSize;
        private int verticalScrollSize;

        private int visibleLines;
        private int lineHeight;
        private int lineWidth;

        public Color TextColor { get; set; }

        public void SetText(string text)
        {
            this.text = text;
            InitializeText();
        }

        public bool WordWrap
        {
            get => wordWrap;
            set { wordWrap = value; InitializeText(); }
        }

        public HorizontalAlignment Alignment { get; }

        public TextBox(WindowBase window, int x, int y, int width, int height, string text, HorizontalAlignment alignment, bool wordWrap, System.Drawing.Font font, Color color)
            : base(window, x, y, width, height)
        {
            Alignment = alignment;
            TextColor = color;
            this.font = font ?? window?.Owner.TextFontDefault;
            scrollbarSize = (int)(16 * Window.Owner.DpiScaling);
            textureHolder = new TextTextureResourceHolder(Window.Owner.Game, 10);
            this.wordWrap = wordWrap;
            this.text = text;
            InitializeText();
        }

        public TextBox(WindowBase window, int x, int y, int width, int height, string text, bool wordWrap)
            : this(window, x, y, width, height, text, HorizontalAlignment.Left, wordWrap, null, Color.White)
        {
        }

        public TextBox(WindowBase window, int width, int height, string text, HorizontalAlignment align, bool wordWrap)
            : this(window, 0, 0, width, height, text, align, wordWrap, null, Color.White)
        {
        }

        public TextBox(WindowBase window, int width, int height, string text, bool wordWrap)
            : this(window, 0, 0, width, height, text, HorizontalAlignment.Left, wordWrap, null, Color.White)
        {
        }

        public TextBox(WindowBase window, int width, int height, string text, HorizontalAlignment align, bool wordWrap, Color color)
            : this(window, 0, 0, width, height, text, align, wordWrap, null, color)
        {
        }

        internal override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            int thumbPosition;
            if (!wordWrap)
            {
                thumbPosition = horizontalScrollSize > 0 ? (Bounds.Width - 4 * scrollbarSize) * horizontalScrollPosition / horizontalScrollSize : 0;
                // Left button
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, scrollbarSize, scrollbarSize), topButtonClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
                // Left gutter
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + scrollbarSize, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, thumbPosition, scrollbarSize), gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
                // Thumb
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + scrollbarSize + thumbPosition, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, scrollbarSize, scrollbarSize), horizontalScrollSize > 0 ? thumbClipping : gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
                // Right gutter
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + 2 * scrollbarSize + thumbPosition, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, Bounds.Width - 4 * scrollbarSize - thumbPosition, scrollbarSize), gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
                // Right button
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - 2 * scrollbarSize, offset.Y + Bounds.Y + Bounds.Height - scrollbarSize, scrollbarSize, scrollbarSize), bottomButtonClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            }
            thumbPosition = verticalScrollSize > 0 ? (Bounds.Height - 4 * scrollbarSize) * verticalScrollPosition / verticalScrollSize : 0;
            // Top button
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y, scrollbarSize, scrollbarSize), topButtonClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Top gutter
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + scrollbarSize, thumbPosition, scrollbarSize), gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Thumb
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + scrollbarSize + thumbPosition, scrollbarSize, scrollbarSize), verticalScrollSize > 0 ? thumbClipping : gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Bottom gutter
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + 2 * scrollbarSize + thumbPosition, Bounds.Height - 4 * scrollbarSize - thumbPosition, scrollbarSize), gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            // Bottom button
            spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - scrollbarSize, offset.Y + Bounds.Y + Bounds.Height - 2 * scrollbarSize, scrollbarSize, scrollbarSize), bottomButtonClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);

            RasterizerState rasterizer = spriteBatch.GraphicsDevice.RasterizerState;
            Rectangle scissorRectangle = spriteBatch.GraphicsDevice.ScissorRectangle;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, scissorTestRasterizer);
            spriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle(offset.X + Bounds.X, offset.Y + Bounds.Y, Bounds.Width - 4 - scrollbarSize, Bounds.Height - 4 - scrollbarSize);
            spriteBatch.GraphicsDevice.RasterizerState = scissorTestRasterizer;

            int lineNumber = lineHeight > 0 ? verticalScrollPosition / lineHeight : 0;
            for (int i = lineNumber; i < lineNumber + visibleLines && i < lines.Length; i++)
            {
                Texture2D lineTexture = textureHolder.PrepareResource(lines[i], font);
                spriteBatch.Draw(lineTexture, (Bounds.Location + offset + new Point(GetAlignmentOffset(lineTexture.Width), 0) - new Point(horizontalScrollPosition, verticalScrollPosition) + new Point(0, i * lineHeight)).ToVector2(), TextColor);
            }
            base.Draw(spriteBatch, offset);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, rasterizer, null);
            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRectangle;
        }

        private void InitializeText()
        {
            if (string.IsNullOrEmpty(text))
            {
                lines = Array.Empty<string>();
                return;
            }
            int longestLineIndex = -1;
            int lineLength = 0;
            if (wordWrap)
            {
                lines = WrapLines(text, font, Bounds.Width - scrollbarSize - 4).ToArray();
                Texture2D texture = textureHolder.PrepareResource(lines[0], font);
                lineHeight = texture.Height;
                lineWidth = Bounds.Width - scrollbarSize + 4;
                horizontalScrollSize = -1; 
            }
            else
            {
                lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Length > lineLength)
                    {
                        lineLength = lines[i].Length;
                        longestLineIndex = i;
                    }
                }
                Texture2D texture = textureHolder.PrepareResource(lines[longestLineIndex], font);
                lineHeight = texture.Height;
                lineWidth = texture.Width;
                horizontalScrollSize = texture.Width + scrollbarSize + 4 - Bounds.Width;
            }
            verticalScrollSize = lines.Length * lineHeight + (wordWrap ? 0 : scrollbarSize + 4) - Bounds.Height;
            visibleLines = Bounds.Height / lineHeight;
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
                SetVerticalScrollPosition(verticalScrollPosition + e.Movement.Y * verticalScrollSize / (Bounds.Height - 4 * scrollbarSize));
                return true;
            }
            else if (e.MousePosition.X > Bounds.Left + scrollbarSize && e.MousePosition.X < Bounds.Right - scrollbarSize &&
                e.MousePosition.Y > Bounds.Bottom - scrollbarSize && e.MousePosition.Y < Bounds.Bottom
                    && e.Movement != Point.Zero)
            {
                Window.CapturedControl = this;
                SetHorizontalScrollPosition(horizontalScrollPosition + e.Movement.X * horizontalScrollSize / (Bounds.Width - 4 * scrollbarSize));
                return true;
            }
            return Window.CapturedControl == this || base.HandleMouseDrag(e);
        }

        private void SetHorizontalScrollPosition(int position)
        {
            position = MathHelper.Clamp(position, 0, horizontalScrollSize);
            horizontalScrollPosition = position;
        }

        private void SetVerticalScrollPosition(int position)
        {
            position = MathHelper.Clamp(position, 0, verticalScrollSize);
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
                else if (e.MousePosition.X < Bounds.Left + scrollbarSize + (Bounds.Width - 4 * scrollbarSize) * horizontalScrollPosition / horizontalScrollSize)
                    // Mouse down occured on left gutter.
                    SetHorizontalScrollPosition(horizontalScrollPosition - Bounds.Width);
                else if (e.MousePosition.X > Bounds.Width - 2 * scrollbarSize)
                    // Mouse down occured on right button.
                    SetHorizontalScrollPosition(horizontalScrollPosition + Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.X > Bounds.Left + 2 * scrollbarSize + (Bounds.Width - 4 * scrollbarSize) * horizontalScrollPosition / horizontalScrollSize)
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
                else if (e.MousePosition.Y < Bounds.Top + scrollbarSize + (Bounds.Height - 4 * scrollbarSize) * verticalScrollPosition / verticalScrollSize)
                    // Mouse down occured on top gutter.
                    SetVerticalScrollPosition(verticalScrollPosition - Bounds.Height);
                else if (e.MousePosition.Y > Bounds.Bottom - 2 * scrollbarSize)
                    // Mouse down occured on bottom button.
                    SetVerticalScrollPosition(verticalScrollPosition + Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.Y > Bounds.Top + 2 * scrollbarSize + (Bounds.Height - 4 * scrollbarSize) * verticalScrollPosition / verticalScrollSize)
                    // Mouse down occured on bottom gutter.
                    SetVerticalScrollPosition(verticalScrollPosition + Bounds.Height);
                return true;
            }

            return false;
        }

        private static List<string> WrapLines(string text, System.Drawing.Font font, int maxLineWidth)
        {
            List<string> lines = new List<string>();
            string testesString;

            using (System.Drawing.Bitmap measureBitmap = new System.Drawing.Bitmap(1, 1))
            {
                using (System.Drawing.Graphics measureGraphics = System.Drawing.Graphics.FromImage(measureBitmap))
                {
                    void BreakLongWord(ReadOnlySpan<char> remainingLine)
                    {
                        if (TextTextureRenderer.Measure((testesString = remainingLine.ToString()), font, measureGraphics).Width < maxLineWidth)
                        {
                            lines.Add(testesString);
                            return;
                        }

                        int previousStart = 0;
                        int i = 0;
                        while (i < remainingLine.Length)
                        {
                            while (++i < remainingLine.Length && TextTextureRenderer.Measure(testesString = remainingLine[previousStart..(i + 1)].ToString(), font, measureGraphics).Width < maxLineWidth)
                                ;
                            lines.Add(testesString);
                            previousStart = i + 1;
                        }
                    }

                    foreach (ReadOnlySpan<char> line in text.AsSpan().EnumerateLines())
                    {
                        int currentOffset = 0;
                        int lineOffset = 0;

                        int index = line.IndexOfAny(whitespaces);
                        string previousTestString = string.Empty;

                        while (true)
                        {
                            if (index > -1) // any further break in this line?
                            {
                                testesString = line[lineOffset..(currentOffset + index + 1)].ToString();

                                if (TextTextureRenderer.Measure(testesString, font, measureGraphics).Width < maxLineWidth) // if it fits, check for the next word
                                {
                                    currentOffset += index + 1;
                                    previousTestString = testesString;
                                    index = line[currentOffset..].IndexOfAny(whitespaces);

                                    if (index < 0) // no more breaks in this line?
                                    {
                                        // check if the full remaining line fits
                                        testesString = line[lineOffset..].ToString();
                                        if (TextTextureRenderer.Measure(testesString, font, measureGraphics).Width < maxLineWidth) //fits, so we add the full remainder
                                        {
                                            lines.Add(testesString);
                                            break;
                                        }
                                        else //doesn't fit, so we add the previousy tested bit, advance, and add the remaidner
                                        {
                                            lines.Add(previousTestString);
                                            lineOffset = currentOffset;
                                            BreakLongWord(line[lineOffset..]);
                                            break;
                                        }
                                    }
                                }
                                else //doesn't fit anymore, so add the previously tested string line, and advance to the current position
                                {
                                    if (lineOffset == currentOffset)
                                    {
                                        BreakLongWord(line[lineOffset..(currentOffset + index + 1)]);
                                        currentOffset += index + 1;
                                        lineOffset = currentOffset;
                                        index = line[currentOffset..].IndexOfAny(whitespaces);
                                    }
                                    else
                                    {
                                        lines.Add(previousTestString);
                                        lineOffset = currentOffset;
                                    }
                                }
                            }
                            else // if no further break, add the rest of the line, break if needed
                            {
                                BreakLongWord(line[lineOffset..]);
                                break;
                            }

                        }
                    }

                }
            }
            return lines;
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

        private int GetAlignmentOffset(int textureWidth)
        {
            return Alignment switch
            {
                HorizontalAlignment.Left => 0,
                HorizontalAlignment.Center => (lineWidth - textureWidth) / 2,
                HorizontalAlignment.Right => lineWidth - textureWidth,
                _ => 0,
            };
        }
    }
}
