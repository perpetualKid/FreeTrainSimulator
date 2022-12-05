// COPYRIGHT 2011, 2012, 2013 by the Open Rails project.
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

#define WINDOWTEXT_SPRITEBATCH

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Native;

using Font = System.Drawing.Font;
using FontStyle = System.Drawing.FontStyle;
using GraphicsUnit = System.Drawing.GraphicsUnit;

namespace Orts.ActivityRunner.Viewer3D.Popups
{
    public enum LabelAlignment
    {
        Left,
        Center,
        Right,
    }

    public sealed class WindowTextManager
    {
        // THREAD SAFETY:
        //   All accesses must be done in local variables. No modifications to the objects are allowed except by
        //   assignment of a new instance (possibly cloned and then modified).
        private Dictionary<string, WindowTextFont> Fonts = new Dictionary<string, WindowTextFont>();

        /// <summary>
        /// Provides a <see cref="WindowTextFont"/> for the specified <paramref name="fontFamily"/>,
        /// <paramref name="sizeInPt"/> and <paramref name="style"/>, as if the system was using
        /// 96 DPI (even when it isn't).
        /// </summary>
        /// <remarks>
        /// All <see cref="WindowTextFont"/> instances are cached by the <see cref="WindowTextManager"/>.
        /// </remarks>
        /// <param name="fontFamily">Font family name (e.g. "Arial")</param>
        /// <param name="sizeInPt">Size of the font, in 96 DPI points (e.g. 9)</param>
        /// <param name="style">Style of the font (e.g. <c>FontStyle.Normal</c>)</param>
        /// <returns>A <see cref="WindowTextFont"/> that can be used to draw text of the given font family,
        /// size and style.</returns>
        public WindowTextFont GetExact(string fontFamily, float sizeInPt, FontStyle style)
        {
            return GetExact(fontFamily, sizeInPt, style, 0);
        }

        /// <summary>
        /// Provides a <see cref="WindowTextFont"/> for the specified <paramref name="fontFamily"/>,
        /// <paramref name="sizeInPt"/> and <paramref name="style"/> with the specified <paramref name="outlineSize"/>,
        /// as if the system was using 96 DPI (even when it isn't).
        /// </summary>
        /// <remarks>
        /// All <see cref="WindowTextFont"/> instances are cached by the <see cref="WindowTextManager"/>.
        /// </remarks>
        /// <param name="fontFamily">Font family name (e.g. "Arial")</param>
        /// <param name="sizeInPt">Size of the font, in 96 DPI points (e.g. 9)</param>
        /// <param name="style">Style of the font (e.g. <c>FontStyle.Normal</c>)</param>
        /// <param name="outlineSize">Size of the outline, in pixels (e.g. 2)</param>
        /// <returns>A <see cref="WindowTextFont"/> that can be used to draw text of the given font family,
        /// size and style with the given outline size.</returns>
        public WindowTextFont GetExact(string fontFamily, float sizeInPt, FontStyle style, int outlineSize)
        {
            var fonts = Fonts;
            var key = $"{fontFamily}:{sizeInPt:F}:{style}:{outlineSize}";
            if (!fonts.ContainsKey(key))
            {
                fonts = new Dictionary<string, WindowTextFont>(fonts);
                fonts.Add(key, new WindowTextFont(fontFamily, sizeInPt, style, outlineSize));
                Fonts = fonts;
            }
            return fonts[key];
        }

        public void Load(GraphicsDevice graphicsDevice)
        {
            var fonts = Fonts;
            foreach (var font in fonts.Values)
                font.Load(graphicsDevice);
        }
    }

    public sealed class WindowTextFont
    {
        private readonly Font Font;
        private readonly int OutlineSize;

        // THREAD SAFETY:
        //   All accesses must be done in local variables. No modifications to the objects are allowed except by
        //   assignment of a new instance (possibly cloned and then modified).
        private CharacterGroup Characters;

        internal WindowTextFont(string fontFamily, float sizeInPt, FontStyle style, int outlineSize)
        {
            Font = new Font(fontFamily, (int)Math.Round(sizeInPt * 96 / 72), style, GraphicsUnit.Pixel);
            OutlineSize = outlineSize;
            Characters = new CharacterGroup(Font, OutlineSize);
            if (Viewer3D.Viewer.Catalog != null)
            EnsureCharacterData(Viewer3D.Viewer.Catalog.GetString("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890 \",.-+|!$%&/()=?;:'_[]"));
        }

        /// <summary>
        /// Gets the line height of the font.
        /// </summary>
        public int Height => Font.Height;

        /// <summary>
        /// Measures the width of a given string.
        /// </summary>
        /// <param name="text">Text to measure</param>
        /// <returns>The length of the text in pixels.</returns>
        public int MeasureString(string text)
        {
            EnsureCharacterData(text);
            var characters = Characters;

            var chIndexes = new int[text.Length];
            for (var i = 0; i < text.Length; i++)
                chIndexes[i] = characters.IndexOfCharacter(text[i]);

            var x = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                x += characters.AbcWidths[chIndexes[i]].X;
                x += characters.AbcWidths[chIndexes[i]].Y;
                x += characters.AbcWidths[chIndexes[i]].Z;
            }
            return (int)x;
        }

        public void Draw(SpriteBatch spriteBatch, Point offset, string text, Color color)
        {
            Draw(spriteBatch, offset, 0, 0, text, LabelAlignment.Left, color, Color.Black);
        }

        private void Draw(SpriteBatch spriteBatch, Point position, float rotation, int width, string text, LabelAlignment align, Color color, Color outline)
        {
            EnsureCharacterData(text);
            var characters = Characters;

            var chIndexes = new int[text.Length];
            for (var i = 0; i < text.Length; i++)
                chIndexes[i] = characters.IndexOfCharacter(text[i]);

            var rotationScale = Matrix.CreateRotationZ(rotation) * Matrix.CreateTranslation(position.X - OutlineSize, position.Y - OutlineSize, 0);

            var current = Vector2.Zero;
            if (align != LabelAlignment.Left)
            {
                for (var i = 0; i < text.Length; i++)
                {
                    current.X += characters.AbcWidths[chIndexes[i]].X;
                    current.X += characters.AbcWidths[chIndexes[i]].Y;
                    current.X += characters.AbcWidths[chIndexes[i]].Z;
                }
                if (align == LabelAlignment.Center)
                    current.X = (int)((width - current.X) / 2);
                else
                    current.X = width - current.X;
            }

            var start = current;
            var texture = characters.Texture;
            if (texture == null)
                return;

            if (OutlineSize > 0)
            {
                var outlineOffset = characters.BoxesMaxBottom;
                current = start;
                for (var i = 0; i < text.Length; i++)
                {
                    var box = characters.Boxes[chIndexes[i]];
                    box.Y += outlineOffset;
                    if (text[i] > ' ')
                        spriteBatch.Draw(texture, Vector2.Transform(current, rotationScale), box, outline, rotation, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
                    current.X += characters.AbcWidths[chIndexes[i]].X;
                    current.X += characters.AbcWidths[chIndexes[i]].Y;
                    current.X += characters.AbcWidths[chIndexes[i]].Z;
                    if (text[i] == '\n')
                    {
                        current.X = start.X;
                        current.Y += Height;
                    }
                }
            }
            current = start;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] > ' ')
                    spriteBatch.Draw(texture, Vector2.Transform(current, rotationScale), characters.Boxes[chIndexes[i]], color, rotation, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
                current.X += characters.AbcWidths[chIndexes[i]].X;
                current.X += characters.AbcWidths[chIndexes[i]].Y;
                current.X += characters.AbcWidths[chIndexes[i]].Z;
                if (text[i] == '\n')
                {
                    current.X = start.X;
                    current.Y += Height;
                }
            }
        }

        public void Load(GraphicsDevice graphicsDevice)
        {
            Characters.Load(graphicsDevice);
        }

        private void EnsureCharacterData(string text)
        {
            var characters = Characters;

            foreach (var ch in text)
            {
                if (!characters.ContainsCharacter(ch))
                {
                    Characters = new CharacterGroup(text.ToCharArray(), characters);
                    break;
                }
            }
        }

        internal sealed class CharacterGroup
        {
            private const int BoxSpacing = 1;
            private const System.Windows.Forms.TextFormatFlags Flags = System.Windows.Forms.TextFormatFlags.NoPadding | System.Windows.Forms.TextFormatFlags.NoPrefix | System.Windows.Forms.TextFormatFlags.SingleLine | System.Windows.Forms.TextFormatFlags.Top;
            private readonly Font Font;
            private readonly int OutlineSize;
            private readonly char[] Characters;
            public readonly Rectangle[] Boxes;
            public readonly int BoxesMaxRight;
            public readonly int BoxesMaxBottom;
            public readonly Vector3[] AbcWidths;
            public Texture2D Texture { get; private set; }

            public CharacterGroup(Font font, int outlineSize)
            {
                Font = font;
                OutlineSize = outlineSize;
                Characters = Array.Empty<char>();
                Boxes = Array.Empty<Rectangle>();
                BoxesMaxRight = 0;
                BoxesMaxBottom = 0;
                AbcWidths = Array.Empty<Vector3>();
            }

            public CharacterGroup(char[] characters, CharacterGroup mergeGroup)
                : this(characters, mergeGroup.Font, mergeGroup.OutlineSize, mergeGroup.Characters, mergeGroup.Boxes, mergeGroup.AbcWidths)
            {
            }

            private CharacterGroup(char[] characters, Font mergeFont, int mergeOutlineSize, char[] mergeCharacters, Rectangle[] mergeBoxes, Vector3[] mergeAbcWidths)
            {
                Font = mergeFont;
                OutlineSize = mergeOutlineSize;
                Characters = characters.Union(mergeCharacters).OrderBy(c => c).ToArray();
                Boxes = new Rectangle[Characters.Length];
                AbcWidths = new Vector3[Characters.Length];

                // Boring device context for APIs.
                var hdc = NativeMethods.CreateCompatibleDC(IntPtr.Zero);
                NativeMethods.SelectObject(hdc, Font.ToHfont());
                try
                {
                    // Get character glyph indices to identify those not supported by this font.
                    var charactersGlyphs = new short[Characters.Length];
                    if (NativeMethods.GetGlyphIndices(hdc, new String(Characters), Characters.Length, charactersGlyphs, NativeMethods.GgiFlags.MarkNonexistingGlyphs) != Characters.Length) throw new Exception();

                    var mergeIndex = 0;
                    var spacing = BoxSpacing + OutlineSize;
                    var x = spacing;
                    var y = spacing;
                    var height = (int)Math.Ceiling(Font.GetHeight()) + 1;
                    for (var i = 0; i < Characters.Length; i++)
                    {
                        // Copy ABC widths from merge data or calculate ourselves.
                        if ((mergeIndex < mergeCharacters.Length) && (mergeCharacters[mergeIndex] == Characters[i]))
                        {
                            AbcWidths[i] = mergeAbcWidths[mergeIndex];
                            mergeIndex++;
                        }
                        else if (charactersGlyphs[i] != -1)
                        {
                            NativeStructs.AbcFloatWidth characterAbcWidth;
                            if (!NativeMethods.GetCharABCWidthsFloat(hdc, Characters[i], Characters[i], out characterAbcWidth)) throw new Exception();
                            AbcWidths[i] = new Vector3(characterAbcWidth.A, characterAbcWidth.B, characterAbcWidth.C);
                        }
                        else
                        {
                            // This is a bit of a cheat, but is used when the chosen font does not have the character itself but it will render anyway (e.g. through font fallback).
                            AbcWidths[i] = new Vector3(0, System.Windows.Forms.TextRenderer.MeasureText($" {Characters[i]} ", Font, System.Drawing.Size.Empty, Flags).Width - System.Windows.Forms.TextRenderer.MeasureText("  ", Font, System.Drawing.Size.Empty, Flags).Width, 0);
                        }
                        Boxes[i] = new Rectangle(x, y, (int)(Math.Max(0, AbcWidths[i].X) + AbcWidths[i].Y + Math.Max(0, AbcWidths[i].Z) + 2 * OutlineSize), height + 2 * OutlineSize);
                        x += Boxes[i].Width + BoxSpacing;
                        if (x >= 256)
                        {
                            x = BoxSpacing;
                            y += Boxes[i].Height + BoxSpacing;
                        }
                    }

                    // TODO: Copy boxes from the merge data.
                }
                finally
                {
                    // Cleanup.
                    NativeMethods.DeleteDC(hdc);
                }
                BoxesMaxRight = Boxes.Max(b => b.Right);
                BoxesMaxBottom = Boxes.Max(b => b.Bottom);
            }

            public bool ContainsCharacter(char character)
            {
                return Array.BinarySearch(Characters, character) >= 0;
            }

            public int IndexOfCharacter(char character)
            {
                return Array.BinarySearch(Characters, character);
            }

            private byte[] GetBitmapData(System.Drawing.Rectangle rectangle)
            {
                var bitmap = new System.Drawing.Bitmap(rectangle.Width, rectangle.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                var buffer = new byte[4 * rectangle.Width * rectangle.Height];
                using (var g = System.Drawing.Graphics.FromImage(bitmap))
                {
                    // Clear to black.
                    g.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.Black), rectangle);

                    // Draw the text using system text drawing (yay, ClearType).
                    for (var i = 0; i < Characters.Length; i++)
                        System.Windows.Forms.TextRenderer.DrawText(g, Characters[i].ToString(), Font, new System.Drawing.Point(Boxes[i].X + OutlineSize, Boxes[i].Y + OutlineSize), System.Drawing.Color.White, Flags);
                }
                var bits = bitmap.LockBits(rectangle, System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
                Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);
                bitmap.UnlockBits(bits);
                return buffer;
            }

            public void Load(GraphicsDevice graphicsDevice)
            {
                if (Texture != null || Characters.Length == 0)
                    return;

                var rectangle = new System.Drawing.Rectangle(0, 0, BoxesMaxRight, BoxesMaxBottom);
                var buffer = GetBitmapData(rectangle);

                for (var y = 0; y < rectangle.Height; y++)
                {
                    for (var x = 0; x < rectangle.Width; x++)
                    {
                        var offset = y * rectangle.Width * 4 + x * 4;
                        // alpha = (red + green + blue) / 3.
                        buffer[offset + 3] = (byte)((buffer[offset + 0] + buffer[offset + 1] + buffer[offset + 2]) / 3);
                        // red|green|blue = Color.White;
                        buffer[offset + 2] = 255;
                        buffer[offset + 1] = 255;
                        buffer[offset + 0] = 255;
                    }
                }

                if (OutlineSize > 0)
                {
                    var outlineBuffer = new byte[buffer.Length];
                    Array.Copy(buffer, outlineBuffer, buffer.Length);
                    for (var offsetX = -OutlineSize; offsetX <= OutlineSize; offsetX++)
                    {
                        for (var offsetY = -OutlineSize; offsetY <= OutlineSize; offsetY++)
                        {
                            if (Math.Sqrt(offsetX * offsetX + offsetY * offsetY) <= OutlineSize)
                            {
                                for (var x = OutlineSize; x < rectangle.Width - OutlineSize; x++)
                                {
                                    for (var y = OutlineSize; y < rectangle.Height - OutlineSize; y++)
                                    {
                                        var offset = y * rectangle.Width * 4 + x * 4;
                                        var outlineOffset = offset + offsetY * rectangle.Width * 4 + offsetX * 4;
                                        outlineBuffer[outlineOffset + 3] = (byte)Math.Min(255, outlineBuffer[outlineOffset + 3] + buffer[offset + 3]);
                                    }
                                }
                            }
                        }
                    }
                    var combinedBuffer = new byte[buffer.Length * 2];
                    Array.Copy(buffer, 0, combinedBuffer, 0, buffer.Length);
                    Array.Copy(outlineBuffer, 0, combinedBuffer, buffer.Length, buffer.Length);
                    buffer = combinedBuffer;
                    rectangle.Height *= 2;
                }

                var texture = new Texture2D(graphicsDevice, rectangle.Width, rectangle.Height, false, SurfaceFormat.Color); // Color = 32bppRgb
                texture.SetData(buffer);
                Texture = texture;
            }
        }
    }

}
