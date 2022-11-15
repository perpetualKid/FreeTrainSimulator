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
        private Rectangle? clippingRectangleMultiValueColumn;

        private string[] keys;
        private Vector2 multiValueOffset;

        private List<(Vector2, Texture2D, Color)> drawItemsNameColumn = new List<(Vector2, Texture2D, Color)>();
        private List<(Vector2, Texture2D[], Color)> drawItemsValueColumn = new List<(Vector2, Texture2D[], Color)>();

        private bool dataPrepared;
        private List<(Vector2, Texture2D, Color)> prepareNameColumn = new List<(Vector2, Texture2D, Color)>();
        private List<(Vector2, Texture2D[], Color)> prepareValueColumn = new List<(Vector2, Texture2D[], Color)>();

        public INameValueInformationProvider InformationProvider { get; set; }
        private readonly System.Drawing.Font font;
        private readonly TextTextureResourceHolder textureHolder;

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
                clippingRectangleMultiValueColumn = new Rectangle(0, 0, (int)(value * Window.Owner.DpiScaling), (int)(font.Size * LineSpacing));
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
                prepareNameColumn.Clear();
                prepareValueColumn.Clear();
                int maxColumn = 0;

                if (null == InformationProvider?.DebugInfo)
                    return;

                foreach (string identifier in InformationProvider.DebugInfo.AllKeys)
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
                    prepareNameColumn.Add((new Vector2(0, lineOffset), texture, formatOption?.TextColor ?? TextColor));
                    if (multiValueOffset != Vector2.Zero)
                    {
                        string[] multiValues = InformationProvider.DebugInfo[identifier]?.Split('\t');
                        Texture2D[] textures = new Texture2D[multiValues?.Length ?? 0];
                        if (multiValues != null)
                        {
                            for (int i = 0; i < multiValues.Length; i++)
                                textures[i] = textureHolder.PrepareResource(multiValues[i], currentFont, OutlineRenderOptions);
                            if (maxColumn < multiValues.Length)
                                maxColumn = multiValues.Length;
                        }
                        prepareValueColumn.Add((new Vector2(NameColumnWidth * Window.Owner.DpiScaling, lineOffset), textures, formatOption?.TextColor ?? TextColor));
                    }
                    else
                    {
                        texture = textureHolder.PrepareResource(InformationProvider.DebugInfo[identifier], currentFont, OutlineRenderOptions);
                        prepareValueColumn.Add((new Vector2(NameColumnWidth * Window.Owner.DpiScaling, lineOffset), new Texture2D[] { texture }, formatOption?.TextColor ?? TextColor));
                    }
                    lineOffset += font.Size * LineSpacing;
                }
                Column = Math.Clamp(Column, 0, maxColumn);
                Row = Math.Min(Row, drawItemsNameColumn.Count - 2); //basically Clamping, but Math.Clamp fails if Max < Min
                Row = Math.Max(Row, 0);
                dataPrepared = true;
            }
            base.Update(gameTime, shouldUpdate);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            if (dataPrepared)
            {
                (drawItemsNameColumn, prepareNameColumn) = (prepareNameColumn, drawItemsNameColumn);
                (drawItemsValueColumn, prepareValueColumn) = (prepareValueColumn, drawItemsValueColumn);
                dataPrepared = false;
            }

            int column = Math.Max(Column, 0);
            int row = Math.Min(Row, drawItemsNameColumn.Count - 2);
            row = Math.Max(row, 0);

            Vector2 locationVector = (Bounds.Location + offset).ToVector2();
            if (drawItemsNameColumn.Count > 0)
            {
                //header
                (Vector2 position, Texture2D texture, Color color) = drawItemsNameColumn[0];
                spriteBatch.Draw(texture, position + locationVector, clippingRectangleNameColumn, color);

                (position, Texture2D[] textures, color) = drawItemsValueColumn[0];
                for (int j = column; j < textures.Length; j++)
                    spriteBatch.Draw(textures[j], position + locationVector + (j - column) * multiValueOffset, clippingRectangleMultiValueColumn, color);

                Vector2 rowOffset = new Vector2(0, drawItemsNameColumn[row].Item1.Y);

                for (int i = row + 1; i < drawItemsNameColumn.Count; i++)
                {
                    (position, texture, color) = drawItemsNameColumn[i];
                    if (!boundsSet || (locationVector.Y + position.Y - rowOffset.Y + texture.Height) < Bounds.Bottom)
                        spriteBatch.Draw(texture, position + locationVector - rowOffset, clippingRectangleNameColumn, color);

                    (position, textures, color) = drawItemsValueColumn[i];
                    if (!boundsSet || (locationVector.Y + position.Y - rowOffset.Y + textures[0].Height) < Bounds.Bottom)
                    {
                        for (int j = column; j < textures.Length; j++)
                            spriteBatch.Draw(textures[j], position + locationVector - rowOffset + (j - column) * multiValueOffset, clippingRectangleMultiValueColumn, color);
                    }
                }
            }
            base.Draw(spriteBatch, offset);
        }
    }
}
