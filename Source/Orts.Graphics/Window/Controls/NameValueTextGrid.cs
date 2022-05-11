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

        private readonly List<ValueTuple<Vector2, Texture2D, Color>> drawItemsNameColumn = new List<(Vector2, Texture2D, Color)>();
        private readonly List<ValueTuple<Vector2, Texture2D, Color>> drawItemsValueColumn = new List<(Vector2, Texture2D, Color)>();
        public INameValueInformationProvider InformationProvider { get; set; }
        private readonly System.Drawing.Font font;
        private readonly TextTextureResourceHolder textureHolder;

        public Color TextColor { get; set; } = Color.White;

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

        public NameValueTextGrid(WindowBase window, int x, int y) : base(window, x, y, 0, 0)
        {
            font = Window.Owner.TextFontDefault;
            textureHolder = new TextTextureResourceHolder(Window.Owner.Game);
        }

        public NameValueTextGrid(WindowBase window, int x, int y, int width, int heigth) : base(window, x, y, width, heigth)
        {
            font = Window.Owner.TextFontDefault;
            textureHolder = new TextTextureResourceHolder(Window.Owner.Game);
            clippingRectangleNameColumn = new Rectangle(0, 0, (int)(columnWidth * window.Owner.DpiScaling), (int)(font.Size * LineSpacing));
            clippingRectangleValueColumn = new Rectangle(0, 0, (int)(Bounds.Width - columnWidth * window.Owner.DpiScaling), (int)(font.Size * LineSpacing));
        }

        internal override void Update(GameTime gameTime)
        {
            if (null == InformationProvider?.DebugInfo)
                return;

            float lineOffset = 0;
            drawItemsNameColumn.Clear();
            drawItemsValueColumn.Clear();
            int hashCode;
            foreach (string identifier in InformationProvider.DebugInfo.AllKeys)
            {
                System.Drawing.Font currentFont = font;
                FormatOption formatOption = null;
                if ((InformationProvider.FormattingOptions?.TryGetValue(identifier, out formatOption) ?? false) && formatOption != null)
                {
                    currentFont = FontManager.Scaled(Window.Owner.DefaultFont, formatOption.FontStyle)[Window.Owner.DefaultFontSize];
                }

                hashCode = HashCode.Combine(identifier, formatOption);
                Texture2D texture = textureHolder.PrepareResource(identifier, currentFont);
                drawItemsNameColumn.Add((new Vector2(0, lineOffset), texture, formatOption?.TextColor ?? TextColor));
                texture = textureHolder.PrepareResource(InformationProvider.DebugInfo[identifier], currentFont);
                drawItemsValueColumn.Add((new Vector2(ColumnWidth * Window.Owner.DpiScaling, lineOffset), texture, formatOption?.TextColor ?? TextColor));
                lineOffset += font.Size * LineSpacing;
            }
            //
            base.Update(gameTime);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            Vector2 locationVector = (Bounds.Location + offset).ToVector2();
            foreach ((Vector2 position, Texture2D texture, Color color) in drawItemsNameColumn)
            {
                spriteBatch.Draw(texture, position + locationVector, clippingRectangleNameColumn, color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
            }
            foreach ((Vector2 position, Texture2D texture, Color color) in drawItemsValueColumn)
            {
                spriteBatch.Draw(texture, position + locationVector, clippingRectangleValueColumn, color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
            }
            base.Draw(spriteBatch, offset);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                textureHolder?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
