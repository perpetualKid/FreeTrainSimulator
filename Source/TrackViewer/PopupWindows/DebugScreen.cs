
using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.DebugInfo;
using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Xna;

namespace Orts.TrackViewer.PopupWindows
{
    public enum DebugScreenInformation
    {
        Common,
    }

    public class DebugScreen : OverlayWindowBase
    {
        private readonly List<ValueTuple<Vector2, Texture2D, Color>> drawItems = new List<(Vector2, Texture2D, Color)> ();
        private readonly Color color = Color.White;
        private readonly System.Drawing.Font font;

        private readonly TextTextureResourceHolder textureHolder;

        public EnumArray<IDebugInformationProvider, DebugScreenInformation> DebugScreens { get; } = new EnumArray<IDebugInformationProvider, DebugScreenInformation>();

        public DebugScreen(WindowManager owner, string caption) :
            base(owner, caption, Point.Zero, Point.Zero)
        {
            font = FontManager.Scaled("Segoe UI", System.Drawing.FontStyle.Regular)[14];
            textureHolder = new TextTextureResourceHolder(Owner.Game);
        }

        protected override void Update(GameTime gameTime)
        {
            int lineOffset = 30;
            drawItems.Clear();
            base.Update(gameTime);
            foreach (IDebugInformationProvider provider in DebugScreens)
            {
                int hashCode;
                foreach (string identifier in provider.DebugInfo)
                {
                    if (provider.FormattingOptions.TryGetValue(identifier, out FormatOption formatOption))
                    {
                        hashCode = HashCode.Combine(identifier, formatOption);
                    }
                    else
                    {
                        hashCode = HashCode.Combine(identifier, formatOption);
                        Texture2D texture = textureHolder.PrepareResource(identifier, font);
                        drawItems.Add((new Vector2(10, lineOffset), texture, color));
                        texture = textureHolder.PrepareResource(provider.DebugInfo[identifier], font); 
                        drawItems.Add((new Vector2(100, lineOffset), texture, color));
                        lineOffset += 20;
                    }
                }
                //
            }
        }

        protected override void Draw(SpriteBatch spriteBatch)
        {
            if (null == spriteBatch)
                return;
            foreach ((Vector2 position, Texture2D texture, Color color) item in drawItems)
            {
                spriteBatch.Draw(item.texture, item.position, null, item.color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
            }
            base.Draw(spriteBatch);
        }
    }
}
