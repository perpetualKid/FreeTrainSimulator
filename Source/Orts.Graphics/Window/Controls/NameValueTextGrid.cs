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

        private readonly List<ValueTuple<Vector2, Texture2D, Color>> drawItems = new List<(Vector2, Texture2D, Color)>();
        public INameValueInformationProvider DebugInformationProvider { get; set; }
        private readonly System.Drawing.Font font;
        private readonly TextTextureResourceHolder textureHolder;

        public Color TextColor { get; set; } = Color.White;

        public float LineSpacing { get; set; } = 1.25f;

        public int ColumnWidth { get; set; } = defaultColumnSize;

        public NameValueTextGrid(WindowBase window, int x, int y) : base(window, x, y, 0, 0)
        {
            font = Window.Owner.TextFontDefault;
            textureHolder = new TextTextureResourceHolder(Window.Owner.Game);
        }

        internal override void Update(GameTime gameTime)
        {
            if (null == DebugInformationProvider)
                return;

            float lineOffset = 0;
            drawItems.Clear();
            int hashCode;
            foreach (string identifier in DebugInformationProvider.DebugInfo)
            {
                System.Drawing.Font currentFont = font;
                FormatOption formatOption = null;
                if ((DebugInformationProvider.FormattingOptions?.TryGetValue(identifier, out formatOption) ?? false) && formatOption != null)
                {
                    currentFont = FontManager.Scaled(Window.Owner.DefaultFont, formatOption.FontStyle)[Window.Owner.DefaultFontSize];
                }

                hashCode = HashCode.Combine(identifier, formatOption);
                Texture2D texture = textureHolder.PrepareResource(identifier, currentFont);
                drawItems.Add((new Vector2(0, lineOffset), texture, formatOption?.TextColor ?? TextColor));
                texture = textureHolder.PrepareResource(DebugInformationProvider.DebugInfo[identifier], currentFont);
                drawItems.Add((new Vector2(ColumnWidth * Window.Owner.DpiScaling, lineOffset), texture, formatOption?.TextColor ?? TextColor));
                lineOffset += font.Size * LineSpacing;
            }
            //
            base.Update(gameTime);
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            Vector2 locationVector = (Bounds.Location + offset).ToVector2();
            foreach ((Vector2 position, Texture2D texture, Color color) in drawItems)
            {
                spriteBatch.Draw(texture, position + locationVector, null, color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
            }
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
