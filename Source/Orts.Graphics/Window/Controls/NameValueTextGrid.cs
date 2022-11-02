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
        private const int defaultColumnSize = 100;
        private int columnWidth = defaultColumnSize;
        private Rectangle? clippingRectangleNameColumn;
        private Rectangle? clippingRectangleValueColumn;

        private List<(Vector2, Texture2D, Color)> drawItemsNameColumn = new List<(Vector2, Texture2D, Color)>();
        private List<(Vector2, Texture2D, Color)> drawItemsValueColumn = new List<(Vector2, Texture2D, Color)>();

        private bool dataPrepared;
        private List<(Vector2, Texture2D, Color)> prepareNameColumn = new List<(Vector2, Texture2D, Color)>();
        private List<(Vector2, Texture2D, Color)> prepareValueColumn = new List<(Vector2, Texture2D, Color)>();

        public INameValueInformationProvider InformationProvider { get; set; }
        private readonly System.Drawing.Font font;
        private readonly TextTextureResourceHolder textureHolder;

        public Color TextColor { get; set; } = Color.White; 

        public OutlineRenderOptions OutlineRenderOptions { get; set; }

        public float LineSpacing { get; set; } = 1.25f;

        public int ColumnWidth
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

        public NameValueTextGrid(FormBase window, int x, int y, System.Drawing.Font font = null) : base(window, x, y, 0, 0)
        {
            this.font = font ?? Window.Owner.TextFontDefault;
            textureHolder = TextTextureResourceHolder.Instance(Window.Owner.Game);
        }

        public NameValueTextGrid(FormBase window, int x, int y, int width, int heigth, System.Drawing.Font font = null) : base(window, x, y, width, heigth)
        {
            this.font = font ?? Window.Owner.TextFontDefault;
            textureHolder = TextTextureResourceHolder.Instance(Window.Owner.Game);
            clippingRectangleNameColumn = new Rectangle(0, 0, (int)(columnWidth * window.Owner.DpiScaling), (int)(this.font.Size * LineSpacing));
            clippingRectangleValueColumn = new Rectangle(0, 0, (int)(Bounds.Width - columnWidth * window.Owner.DpiScaling), (int)(this.font.Size * LineSpacing));
        }

        internal override void Update(GameTime gameTime, bool shouldUpdate)
        {
            if (shouldUpdate)
            {
                float lineOffset = 0;
                prepareNameColumn.Clear();
                prepareValueColumn.Clear();

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

                    Texture2D texture = textureHolder.PrepareResource(identifier, currentFont, OutlineRenderOptions);
                    prepareNameColumn.Add((new Vector2(0, lineOffset), texture, formatOption?.TextColor ?? TextColor));
                    texture = textureHolder.PrepareResource(InformationProvider.DebugInfo[identifier], currentFont, OutlineRenderOptions);
                    prepareValueColumn.Add((new Vector2(ColumnWidth * Window.Owner.DpiScaling, lineOffset), texture, formatOption?.TextColor ?? TextColor));
                    lineOffset += font.Size * LineSpacing;
                }
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
            Vector2 locationVector = (Bounds.Location + offset).ToVector2();
            foreach ((Vector2 position, Texture2D texture, Color color) in drawItemsNameColumn)
            {
                spriteBatch.Draw(texture, position + locationVector, clippingRectangleNameColumn, color);
            }
            foreach ((Vector2 position, Texture2D texture, Color color) in drawItemsValueColumn)
            {
                spriteBatch.Draw(texture, position + locationVector, clippingRectangleValueColumn, color);
            }
            base.Draw(spriteBatch, offset);
        }
    }
}
