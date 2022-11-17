using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.DebugInfo;
using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls
{
    public class NameValueTextGrid : WindowControl
    {
        private readonly bool boundsSet;
        private const int defaultColumnSize = 100;
        private int columnWidth = defaultColumnSize;
        private Rectangle? clippingRectangleNameColumn;
        private Rectangle? clippingRectangleValueColumn;

        private Vector2 multiValueOffset;

        private List<(Vector2, Texture2D, Vector2, Texture2D[], Color)> drawItems = new List<(Vector2, Texture2D, Vector2, Texture2D[], Color)>();
        private List<(Vector2, Texture2D, Vector2, Texture2D[], Color)> prepareItems = new List<(Vector2, Texture2D, Vector2, Texture2D[], Color)>();
        private bool dataPrepared;

        private readonly System.Drawing.Font font;
        private readonly TextTextureResourceHolder textureHolder;

        public INameValueInformationProvider InformationProvider { get; set; }

        public Color TextColor { get; set; } = Color.White;

        public OutlineRenderOptions OutlineRenderOptions { get; set; }

        public float LineSpacing { get; set; } = 1.25f;

        public int Column { get; set; }

        public int Row { get; set; }

        public int NameColumnWidth
        {
            get { return columnWidth; }
            set
            {
                columnWidth = value;
                if (Bounds.Width > columnWidth)
                {
                    clippingRectangleNameColumn = new Rectangle(0, 0, (int)(columnWidth * Window.Owner.DpiScaling), (int)(font.Size * LineSpacing));
                    clippingRectangleValueColumn = new Rectangle(0, 0, (int)(Bounds.Width - columnWidth * Window.Owner.DpiScaling), (int)(font.Size * LineSpacing));
                }
            }
        }

        public int MultiValueColumnWidth
        {
            get { return (int)(multiValueOffset.X / Window.Owner.DpiScaling); }
            set
            {
                multiValueOffset.X = (int)(value * Window.Owner.DpiScaling);
                clippingRectangleValueColumn = new Rectangle(0, 0, (int)(value * Window.Owner.DpiScaling), (int)(font.Size * LineSpacing));
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
            NameColumnWidth = defaultColumnSize;
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

                int maxColumn = InformationProvider.MultiElementCount;

                foreach (string identifier in InformationProvider.DetailInfo.Keys)
                {
                    System.Drawing.Font currentFont = font;
                    FormatOption formatOption = null;
                    if ((InformationProvider.FormattingOptions?.TryGetValue(identifier, out formatOption) ?? false) && formatOption != null)
                    {
                        currentFont = FontManager.Scaled(Window.Owner.DefaultFontName, formatOption.FontStyle)[Window.Owner.DefaultFontSize];
                    }

                    string emptySpace = identifier;
                    if (emptySpace.StartsWith('.'))
                        emptySpace = null;
                    Texture2D texture = textureHolder.PrepareResource(emptySpace, currentFont, OutlineRenderOptions);
                    Texture2D[] textures;
                    if (multiValueOffset != Vector2.Zero)
                    {
                        if (InformationProvider.Next != null)
                        {
                            INameValueInformationProvider provider = InformationProvider;
                            textures = new Texture2D[InformationProvider.MultiElementCount];
                            int i = 0;
                            while (provider != null)
                            {
                                textures[i++] = textureHolder.PrepareResource(provider.DetailInfo[identifier], currentFont, OutlineRenderOptions);
                                provider = provider.Next;
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
                    prepareItems.Add((new Vector2(0, lineOffset), texture, new Vector2(NameColumnWidth * Window.Owner.DpiScaling, lineOffset), textures, formatOption?.TextColor ?? TextColor));
                    lineOffset += font.Size * LineSpacing;
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
                spriteBatch.Draw(texture, keyPosition + locationVector, clippingRectangleNameColumn, color);

                for (int j = column; j < textures.Length; j++)
                    spriteBatch.Draw(textures[j], valuePosition + locationVector + (j - column) * multiValueOffset, clippingRectangleValueColumn, color);

                Vector2 rowOffset = new Vector2(0, drawItems[row].Item1.Y);

                for (int i = row + 1; i < drawItems.Count; i++)
                {
                    (keyPosition, texture, valuePosition, textures, color) = drawItems[i];
                    if (!boundsSet || (valuePosition.Y - rowOffset.Y + texture.Height) < Bounds.Height)
                    {
                        spriteBatch.Draw(texture, keyPosition + locationVector - rowOffset, clippingRectangleNameColumn, color);
                        for (int j = column; j < textures.Length; j++)
                            spriteBatch.Draw(textures[j], valuePosition + locationVector - rowOffset + (j - column) * multiValueOffset, clippingRectangleValueColumn, color);
                    }
                }
                base.Draw(spriteBatch, offset);
            }
        }
    }
}