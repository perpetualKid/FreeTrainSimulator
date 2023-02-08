
using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls
{
    public class TextBox : WindowControl
    {
        private static readonly char[] whitespaces = { ' ', '\t', '\r', '\n' };
        private const int mouseClickScrollDelay = 100;
        private Rectangle clientArea;

        private string[] lines;
        private string text;

        private readonly System.Drawing.Font font;
        private readonly RasterizerState scissorTestRasterizer = new RasterizerState { ScissorTestEnable = true };
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly TextTextureResourceHolder textureHolder;
#pragma warning restore CA2213 // Disposable fields should be disposed

        private bool wordWrap;
        private static int scrollbarSize;

        private static readonly Rectangle topButtonClipping = new Rectangle(0, 0, 16, 16);
        private static readonly Rectangle thumbClipping = new Rectangle(1 * 16, 0, 16, 16);
        private static readonly Rectangle gutterClipping = new Rectangle(2 * 16, 0, 16, 16);
        private static readonly Rectangle bottomButtonClipping = new Rectangle(3 * 16, 0, 16, 16);
        private static readonly Vector2 rotateOrigin = new Vector2(0, 16);

        private int verticalScrollPosition;
        private int horizontalScrollPosition;
        private int horizontalThumbPosition;
        private int verticalThumbPosition;
        private int verticalScrollbarLeft;
        private int horizontalScrollbarTop;

        private int horizontalScrollSize;
        private int verticalScrollSize;
        private int bottomGutterFactor;
        private int rightGutterFactor;

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

        public TextBox(FormBase window, int x, int y, int width, int height, string text, HorizontalAlignment alignment, bool wordWrap, System.Drawing.Font font, Color color)
            : base(window, x, y, width, height)
        {
            Alignment = alignment;
            TextColor = color;
            this.font = font ?? window?.Owner.TextFontDefault;
            scrollbarSize = (int)(16 * Window.Owner.DpiScaling);
            textureHolder = TextTextureResourceHolder.Instance(Window.Owner.Game);
            this.wordWrap = wordWrap;
            this.text = text;
            InitializeText();
        }

        public TextBox(FormBase window, int x, int y, int width, int height, string text, bool wordWrap)
            : this(window, x, y, width, height, text, HorizontalAlignment.Left, wordWrap, null, Color.White)
        {
        }

        public TextBox(FormBase window, int width, int height, string text, HorizontalAlignment align, bool wordWrap)
            : this(window, 0, 0, width, height, text, align, wordWrap, null, Color.White)
        {
        }

        public TextBox(FormBase window, int width, int height, string text, bool wordWrap)
            : this(window, 0, 0, width, height, text, HorizontalAlignment.Left, wordWrap, null, Color.White)
        {
        }

        public TextBox(FormBase window, int width, int height, string text, HorizontalAlignment align, bool wordWrap, Color color)
            : this(window, 0, 0, width, height, text, align, wordWrap, null, color)
        {
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            if (horizontalScrollSize > -1)
            {
                // Left button
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X, offset.Y + horizontalScrollbarTop, scrollbarSize, scrollbarSize), topButtonClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
                // Left gutter
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + scrollbarSize, offset.Y + horizontalScrollbarTop, horizontalThumbPosition, scrollbarSize), gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
                // Thumb
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + scrollbarSize + horizontalThumbPosition, offset.Y + horizontalScrollbarTop, scrollbarSize, scrollbarSize), thumbClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
                // Right gutter
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + 2 * scrollbarSize + horizontalThumbPosition, offset.Y + horizontalScrollbarTop, Bounds.Width - (2 + rightGutterFactor) * scrollbarSize - horizontalThumbPosition, scrollbarSize), gutterClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
                // Right button
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + Bounds.X + Bounds.Width - rightGutterFactor * scrollbarSize, offset.Y + horizontalScrollbarTop, scrollbarSize, scrollbarSize), bottomButtonClipping, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
            }
            if (verticalScrollSize > -1)
            {
                // Top button
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + verticalScrollbarLeft, offset.Y + Bounds.Y, scrollbarSize, scrollbarSize), topButtonClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
                // Top gutter
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + verticalScrollbarLeft, offset.Y + Bounds.Y + scrollbarSize, verticalThumbPosition, scrollbarSize), gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
                // Thumb
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + verticalScrollbarLeft, offset.Y + Bounds.Y + scrollbarSize + verticalThumbPosition, scrollbarSize, scrollbarSize), thumbClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
                // Bottom gutter
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + verticalScrollbarLeft, offset.Y + Bounds.Y + 2 * scrollbarSize + verticalThumbPosition, Bounds.Height - (2 + bottomGutterFactor) * scrollbarSize - verticalThumbPosition, scrollbarSize), gutterClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
                // Bottom button
                spriteBatch.Draw(Window.Owner.ScrollbarTexture, new Rectangle(offset.X + verticalScrollbarLeft, offset.Y + Bounds.Bottom - bottomGutterFactor * scrollbarSize, scrollbarSize, scrollbarSize), bottomButtonClipping, Color.White, MathHelper.PiOver2, rotateOrigin, SpriteEffects.None, 0);
            }
            RasterizerState rasterizer = spriteBatch.GraphicsDevice.RasterizerState;
            Rectangle scissorRectangle = spriteBatch.GraphicsDevice.ScissorRectangle;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, scissorTestRasterizer);
            spriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle(offset + Bounds.Location, clientArea.Size);
            spriteBatch.GraphicsDevice.RasterizerState = scissorTestRasterizer;

            int lineNumber = lineHeight > 0 ? verticalScrollPosition / lineHeight : 0;
            for (int i = lineNumber; i < lineNumber + visibleLines && i < lines.Length; i++)
            {
                Texture2D lineTexture = textureHolder.PrepareResource(lines[i], font);
                spriteBatch.Draw(lineTexture,
                    new Vector2(
                        Bounds.Location.X + offset.X + GetAlignmentOffset(lineTexture.Width) - horizontalScrollPosition,
                        Bounds.Location.Y + offset.Y - verticalScrollPosition + i * lineHeight),
                    TextColor);
                ;
            }
            base.Draw(spriteBatch, offset);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, rasterizer, null);
            spriteBatch.GraphicsDevice.ScissorRectangle = scissorRectangle;
        }

        internal override void Initialize()
        {
            base.Initialize();
            verticalScrollbarLeft = Bounds.Right - scrollbarSize;
            horizontalScrollbarTop = Bounds.Bottom - scrollbarSize;
        }

        private void InitializeText()
        {
            if (string.IsNullOrEmpty(text))
            {
                lines = Array.Empty<string>();
                horizontalScrollSize = -1;
                verticalScrollSize = -1;
                return;
            }

            int longestLineIndex = -1;
            int lineLength = 0;
            clientArea = new Rectangle(0, 0, Bounds.Width - scrollbarSize - 4, Bounds.Height - scrollbarSize - 4);
            if (wordWrap)
            {
                lines = WrapLines(text, font, clientArea.Width).ToArray();
                Texture2D texture = textureHolder.PrepareResource(lines[0], font);
                lineHeight = texture.Height;
                lineWidth = clientArea.Width;
                horizontalScrollSize = -1;
                clientArea.Height = Bounds.Height;
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
                if (texture.Width < clientArea.Width)
                {
                    horizontalScrollSize = -1;
                    clientArea.Height = Bounds.Height;
                }
                else
                {
                    horizontalScrollSize = texture.Width - clientArea.Width;
                }
            }
            if (lines.Length * lineHeight < clientArea.Height)
            {
                verticalScrollSize = -1;
                clientArea.Width = Bounds.Width;

            }
            else
            {
                verticalScrollSize = lines.Length * lineHeight + (wordWrap ? 0 : scrollbarSize + 4) - Bounds.Height;
                bottomGutterFactor = horizontalScrollSize < 0 ? 1 : 2;
            }
            rightGutterFactor = verticalScrollSize < 0 ? 1 : 2;
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
                e.MousePosition.Y > Bounds.Top && e.MousePosition.Y < Bounds.Bottom - scrollbarSize
                    && e.Movement != Point.Zero)
            {
                Window.CapturedControl = this;
                SetVerticalScrollPosition((int)(verticalScrollSize * (e.MousePosition.Y - Bounds.Top - scrollbarSize) / (Bounds.Height - scrollbarSize * 3.0)));
                return true;
            }
            else if (e.MousePosition.X > Bounds.Left && e.MousePosition.X < Bounds.Right - scrollbarSize &&
                e.MousePosition.Y > Bounds.Bottom - scrollbarSize && e.MousePosition.Y < Bounds.Bottom
                    && e.Movement != Point.Zero)
            {
                Window.CapturedControl = this;
                SetHorizontalScrollPosition((int)(horizontalScrollSize * (e.MousePosition.X - Bounds.Left - scrollbarSize) / (Bounds.Width - scrollbarSize * 3.0)));
                return true;
            }
            return Window.CapturedControl == this || base.HandleMouseDrag(e);
        }

        private void SetHorizontalScrollPosition(int position)
        {
            position = MathHelper.Clamp(position, 0, horizontalScrollSize);
            horizontalScrollPosition = position;
            horizontalThumbPosition = horizontalScrollSize > 0 ? (Bounds.Width - (verticalScrollSize > -1 ? 4 : 3) * scrollbarSize) * horizontalScrollPosition / horizontalScrollSize : 0;

        }

        private void SetVerticalScrollPosition(int position)
        {
            position = MathHelper.Clamp(position, 0, verticalScrollSize);
            verticalScrollPosition = position;
            verticalThumbPosition = verticalScrollSize > 0 ? (Bounds.Height - (horizontalScrollSize > -1 ? 4 : 3) * scrollbarSize) * verticalScrollPosition / verticalScrollSize : 0;

        }

        private long ticks;

        private bool HandleMouseButton(WindowMouseEvent e)
        {
            if (Environment.TickCount64 < ticks || Window.CapturedControl != null)
                return true;
            ticks = Environment.TickCount64 + mouseClickScrollDelay;
            if (e.MousePosition.Y > Bounds.Bottom - scrollbarSize && e.MousePosition.X < Bounds.Right - (rightGutterFactor - 1) * scrollbarSize)//horizontal scrollbar
            {
                double mousePositionInScrollbar = (e.MousePosition.X - Bounds.Left - scrollbarSize) / (Bounds.Width - scrollbarSize * 3.0);
                // Mouse down occured within the scrollbar.
                if (e.MousePosition.X < Bounds.Left + scrollbarSize)
                    // Mouse down occured on left button.
                    SetHorizontalScrollPosition(horizontalScrollPosition - Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.X < Bounds.Left + scrollbarSize + (Bounds.Width - (2 + rightGutterFactor) * scrollbarSize) * horizontalScrollPosition / horizontalScrollSize)
                    // Mouse down occured on left gutter.
                    SetHorizontalScrollPosition(Math.Max(horizontalScrollPosition - Bounds.Width, (int)(horizontalScrollSize * mousePositionInScrollbar)));
                else if (e.MousePosition.X > Bounds.Width - rightGutterFactor * scrollbarSize)
                    // Mouse down occured on right button.
                    SetHorizontalScrollPosition(horizontalScrollPosition + Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.X > Bounds.Left + rightGutterFactor * scrollbarSize + (Bounds.Width - (2 + rightGutterFactor) * scrollbarSize) * horizontalScrollPosition / horizontalScrollSize)
                    // Mouse down occured on right gutter.
                    SetHorizontalScrollPosition(Math.Min(horizontalScrollPosition + Bounds.Width, (int)(horizontalScrollSize * mousePositionInScrollbar)));
                return true;
            }
            else if (e.MousePosition.X > Bounds.Right - scrollbarSize && e.MousePosition.Y < Bounds.Bottom - (bottomGutterFactor - 1) * scrollbarSize)//vertical scrollbar
            {
                double mousePositionInScrollbar = (e.MousePosition.Y - Bounds.Top - scrollbarSize) / (Bounds.Height - scrollbarSize * 3.0);
                // Mouse down occured within the scrollbar.
                if (e.MousePosition.Y < Bounds.Top + scrollbarSize)
                    // Mouse down occured on top button.
                    SetVerticalScrollPosition(verticalScrollPosition - Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.Y < Bounds.Top + scrollbarSize + (Bounds.Height - (2 + bottomGutterFactor) * scrollbarSize) * verticalScrollPosition / verticalScrollSize)
                    // Mouse down occured on top gutter.
                    SetVerticalScrollPosition(Math.Max(verticalScrollPosition - Bounds.Height, (int)(verticalScrollSize * mousePositionInScrollbar)));
                else if (e.MousePosition.Y > Bounds.Bottom - bottomGutterFactor * scrollbarSize)
                    // Mouse down occured on bottom button.
                    SetVerticalScrollPosition(verticalScrollPosition + Window.Owner.TextFontDefault.Height);
                else if (e.MousePosition.Y > Bounds.Top + bottomGutterFactor * scrollbarSize + (Bounds.Height - (2 + bottomGutterFactor) * scrollbarSize) * verticalScrollPosition / verticalScrollSize)
                    // Mouse down occured on bottom gutter.
                    SetVerticalScrollPosition(Math.Min(verticalScrollPosition + Bounds.Height, (int)(verticalScrollSize * mousePositionInScrollbar)));
                return true;
            }

            return false;
        }

        private static List<string> WrapLines(string text, System.Drawing.Font font, int maxLineWidth)
        {
            List<string> lines = new List<string>();
            string testedString;

            using (System.Drawing.Bitmap measureBitmap = new System.Drawing.Bitmap(1, 1))
            {
                using (System.Drawing.Graphics measureGraphics = System.Drawing.Graphics.FromImage(measureBitmap))
                {
                    void BreakLongWord(ReadOnlySpan<char> remainingLine)
                    {
                        if (measureGraphics.MeasureString(testedString = remainingLine.ToString(), font).Width < maxLineWidth)
                        {
                            lines.Add(testedString);
                            return;
                        }

                        int previousStart = 0;
                        int i = 0;
                        while (i < remainingLine.Length)
                        {
                            while (++i < remainingLine.Length && measureGraphics.MeasureString(testedString = remainingLine[previousStart..(i + 1)].ToString(), font).Width < maxLineWidth)
                                ;
                            lines.Add(testedString);
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
                                testedString = line[lineOffset..(currentOffset + index + 1)].ToString();

                                if (measureGraphics.MeasureString(testedString, font).Width < maxLineWidth) // if it fits, check for the next word
                                {
                                    currentOffset += index + 1;
                                    previousTestString = testedString;
                                    index = line[currentOffset..].IndexOfAny(whitespaces);

                                    if (index < 0) // no more breaks in this line?
                                    {
                                        // check if the full remaining line fits
                                        testedString = line[lineOffset..].ToString();
                                        if (measureGraphics.MeasureString(testedString, font).Width < maxLineWidth) //fits, so we add the full remainder
                                        {
                                            lines.Add(testedString);
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
