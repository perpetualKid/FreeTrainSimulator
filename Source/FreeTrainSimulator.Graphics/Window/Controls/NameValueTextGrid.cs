using System;
using System.Collections.Generic;

using FreeTrainSimulator.Common.DebugInfo;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls
{
    public class NameValueTextGrid : WindowControl
    {
        private readonly bool boundsSet;
        private const int defaultColumnSize = 100;

        private Rectangle?[] columnClippingRectangles = new Rectangle?[2];

        private bool multiColumn;

        private List<(Vector2, Texture2D, Vector2, Texture2D[], Color)> drawItems = new List<(Vector2, Texture2D, Vector2, Texture2D[], Color)>();
        private List<(Vector2, Texture2D, Vector2, Texture2D[], Color)> prepareItems = new List<(Vector2, Texture2D, Vector2, Texture2D[], Color)>();
        private bool dataPrepared;

        private readonly System.Drawing.Font font;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly TextTextureResourceHolder textureHolder;
#pragma warning restore CA2213 // Disposable fields should be disposed

        public INameValueInformationProvider InformationProvider { get; set; }

        public Color TextColor { get; set; } = Color.White;

        public OutlineRenderOptions OutlineRenderOptions { get; set; }

        public float LineSpacing { get; set; } = 1.1f;

        public int Column { get; set; }

        public int Row { get; set; }

#pragma warning disable CA1819 // Properties should not return arrays
        public int[] ColumnWidth
#pragma warning restore CA1819 // Properties should not return arrays
        {
            get
            {
                if (columnClippingRectangles == null)
                    return Array.Empty<int>();
                int[] width = new int[columnClippingRectangles.Length];
                for (int i = 0; i < columnClippingRectangles.Length; i++)
                    width[i] = (int)(columnClippingRectangles[i].Value.Width / Window.Owner.DpiScaling);
                return width;
            }
            set
            {
                if (value != null)
                {
                    multiColumn = true;
                    int heigth = (int)Math.Ceiling((font.Height * LineSpacing));
                    columnClippingRectangles = new Rectangle?[value.Length];
                    for (int i = 0; i < value.Length; i++)
                    {
                        if (value[i] > 0)
                            columnClippingRectangles[i] = new Rectangle(0, 0, (int)Math.Ceiling((value[i] * Window.Owner.DpiScaling)), heigth);
                    }
                }
            }
        }

        public NameValueTextGrid(FormBase window, int x, int y, System.Drawing.Font font = null) : base(window, x, y, 0, 0)
        {
            this.font = font ?? Window.Owner.TextFontDefault;
            textureHolder = TextTextureResourceHolder.Instance(Window.Owner.Game);
            boundsSet = Bounds.Height > 0 && Bounds.Width > 0;
        }

        public NameValueTextGrid(FormBase window, int x, int y, int width, int heigth, System.Drawing.Font font = null) : base(window, x, y, width, heigth)
        {
            this.font = font ?? Window.Owner.TextFontDefault;
            textureHolder = TextTextureResourceHolder.Instance(Window.Owner.Game);
            boundsSet = Bounds.Height > 0 && Bounds.Width > 0;
        }

        internal override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (shouldUpdate)
            {
                float lineOffset = 0;
                prepareItems.Clear();

                if (null == InformationProvider?.DetailInfo)
                    return;

                int maxColumn = (InformationProvider as DetailInfoBase)?.MultiColumnCount ?? 0;

                foreach (string identifier in InformationProvider.DetailInfo.Keys)
                {
                    System.Drawing.Font currentFont = font;
                    FormatOption formatOption = null;
                    if ((InformationProvider.FormattingOptions?.TryGetValue(identifier, out formatOption) ?? false) && formatOption != null)
                    {
                        currentFont = FontManager.Scaled(Window.Owner.FontName, formatOption.FontStyle)[Window.Owner.FontSize];
                    }

                    string emptySpace = identifier;
                    if (emptySpace.StartsWith('.'))
                        emptySpace = null;
                    Texture2D texture = textureHolder.PrepareResource(emptySpace, currentFont, OutlineRenderOptions);
                    Texture2D[] textures;
                    if (multiColumn)
                    {
                        if (InformationProvider is DetailInfoBase detailInfo && detailInfo.NextColumn != null)
                        {
                            textures = new Texture2D[detailInfo.MultiColumnCount];
                            int i = 0;
                            while (detailInfo != null)
                            {
                                textures[i++] = textureHolder.PrepareResource(detailInfo.DetailInfo[identifier], currentFont, OutlineRenderOptions);
                                detailInfo = detailInfo.NextColumn;
                            }
                        }
                        else
                        {
                            string[] multiValues = InformationProvider.DetailInfo[identifier]?.Split('\t');
                            textures = new Texture2D[multiValues?.Length ?? 0];
                            if (multiValues != null)
                            {
                                {
                                    for (int i = 0; i < multiValues.Length; i++)
                                        textures[i] = textureHolder.PrepareResource(multiValues[i], currentFont, OutlineRenderOptions);
                                    if (maxColumn < multiValues.Length)
                                        maxColumn = multiValues.Length;
                                }
                            }
                        }
                    }
                    else
                    {
                        textures = new Texture2D[1] { textureHolder.PrepareResource(InformationProvider.DetailInfo[identifier], currentFont, OutlineRenderOptions) };
                    }
                    prepareItems.Add((new Vector2(0, lineOffset), texture, new Vector2(columnClippingRectangles[0]?.Width ?? defaultColumnSize * Window.Owner.DpiScaling, lineOffset), textures, formatOption?.TextColor ?? TextColor));
                    lineOffset += (int)Math.Ceiling(font.Height * LineSpacing);
                }
                Column = Math.Clamp(Column, 0, maxColumn);
                Row = Math.Min(Row, drawItems.Count - 2); //basically Clamping, but Math.Clamp fails if Max < Min
                Row = Math.Max(Row, 0);
                dataPrepared = true;
            }
            base.Update(gameTime, shouldUpdate);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            if (dataPrepared)
            {
                (drawItems, prepareItems) = (prepareItems, drawItems);
                dataPrepared = false;
            }

            int column = Math.Max(Column, 0);
            int row = Math.Min(Row, drawItems.Count - 2);
            row = Math.Max(row, 0);

            Vector2 locationVector = (Bounds.Location + offset).ToVector2();
            if (drawItems.Count > 0)
            {
                //header
                (Vector2 keyPosition, Texture2D texture, Vector2 valuePosition, Texture2D[] textures, Color color) = drawItems[0];
                if (null != texture && texture != textureHolder.EmptyTexture)
                    spriteBatch.Draw(texture, keyPosition + locationVector, columnClippingRectangles[0], color);

                int columnOffset = 0;
                for (int j = column; j < textures.Length; j++)
                {
                    Rectangle? columnClipping = columnClippingRectangles[j + 1 >= columnClippingRectangles.Length ? ^1 : j + 1];
                    if (null != textures[j] && textures[j] != textureHolder.EmptyTexture)
                        spriteBatch.Draw(textures[j], valuePosition + locationVector + new Vector2(columnOffset, 0), columnClipping, color);
                    columnOffset += columnClipping?.Width ?? 0;
                }

                Vector2 rowOffset = new Vector2(0, drawItems[row].Item1.Y);

                for (int i = row + 1; i < drawItems.Count; i++)
                {
                    columnOffset = 0;
                    (keyPosition, texture, valuePosition, textures, color) = drawItems[i];
                    if (!boundsSet || (valuePosition.Y - rowOffset.Y + texture.Height) < Bounds.Height)
                    {
                        if (null != texture && texture != textureHolder.EmptyTexture)
                            spriteBatch.Draw(texture, keyPosition + locationVector - rowOffset, columnClippingRectangles[0], color);
                        for (int j = column; j < textures.Length; j++)
                        {
                            Rectangle? columnClipping = columnClippingRectangles[j + 1 >= columnClippingRectangles.Length ? ^1 : j + 1];
                            if (null != textures[j] && textures[j] != textureHolder.EmptyTexture)
                                spriteBatch.Draw(textures[j], valuePosition + locationVector - rowOffset + new Vector2(columnOffset, 0), columnClipping, color);
                            columnOffset += columnClipping?.Width ?? 0;
                        }
                    }
                }
                base.Draw(spriteBatch, offset);
            }
        }
    }
}