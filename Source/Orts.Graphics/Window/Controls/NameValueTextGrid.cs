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
        private Rectangle? clippingRectangleMultiValueColumn;

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
        }

        public NameValueTextGrid(FormBase window, int x, int y, int width, int heigth, System.Drawing.Font font = null) : base(window, x, y, width, heigth)
        {
            this.font = font ?? Window.Owner.TextFontDefault;
            textureHolder = TextTextureResourceHolder.Instance(Window.Owner.Game);
            NameColumnWidth = defaultColumnSize;
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

                    string emptySpace = identifier;
                    if (emptySpace.StartsWith('.'))
                        emptySpace = null;
                    Texture2D texture = textureHolder.PrepareResource(emptySpace, currentFont, OutlineRenderOptions);
                    prepareNameColumn.Add((new Vector2(0, lineOffset), texture, formatOption?.TextColor ?? TextColor));
                    if (multiValueOffset != Vector2.Zero)
                    {
                        string[] multiValues = InformationProvider.DebugInfo[identifier]?.Split('\t');
                        if (multiValues != null)
                        {
                            Texture2D[] textures = new Texture2D[multiValues.Length];
                            for (int i = 0; i < multiValues.Length; i++)
                                textures[i] = textureHolder.PrepareResource(multiValues[i], currentFont, OutlineRenderOptions);
                            prepareValueColumn.Add((new Vector2(NameColumnWidth * Window.Owner.DpiScaling, lineOffset), textures, formatOption?.TextColor ?? TextColor));
                        }
                    }
                    else
                    {
                        texture = textureHolder.PrepareResource(InformationProvider.DebugInfo[identifier], currentFont, OutlineRenderOptions);
                        prepareValueColumn.Add((new Vector2(NameColumnWidth * Window.Owner.DpiScaling, lineOffset), new Texture2D[] { texture }, formatOption?.TextColor ?? TextColor));
                    }
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
            foreach ((Vector2 position, Texture2D[] textures, Color color) in drawItemsValueColumn)
            {
                if (textures.Length > 1)
                    for (int i = Column; i < textures.Length; i++)
                        spriteBatch.Draw(textures[i], position + locationVector + (i - Column) * multiValueOffset, clippingRectangleMultiValueColumn, color);
                else
                    spriteBatch.Draw(textures[0], position + locationVector, clippingRectangleValueColumn, color);
            }
            base.Draw(spriteBatch, offset);
        }
    }
}
