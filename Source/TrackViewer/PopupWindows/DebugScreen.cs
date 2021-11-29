using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics;
using Orts.Graphics.Window;
using Orts.Graphics.Xna;

namespace Orts.TrackViewer.PopupWindows
{
    public class DebugScreen : OverlayWindowBase
    {
        private readonly Texture2D textTexture;
        private readonly Color color = Color.DeepPink;
        private readonly Vector2 position = new Vector2(100, 100);

        public DebugScreen(WindowManager owner, string caption) : 
            base(owner, caption, Point.Zero, Point.Zero)
        {
            textTexture = TextTextureRenderer.Resize("0000000000", FontManager.Instance("Segoe UI", System.Drawing.FontStyle.Italic)[24], owner.GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        protected override void Draw(SpriteBatch spriteBatch)
        {
            spriteBatch.Begin();
            spriteBatch.Draw(textTexture, position, null, color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
            spriteBatch.End();
            base.Draw(spriteBatch);
        }
    }
}
