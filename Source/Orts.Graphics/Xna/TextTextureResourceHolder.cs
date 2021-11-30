using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Orts.Graphics.Xna
{
    public class TextTextureResourceHolder : ResourceGameComponent<Texture2D>
    {
        private readonly System.Drawing.Bitmap measureBitmap = new System.Drawing.Bitmap(1, 1);
        private readonly System.Drawing.Graphics measureGraphics;

        public TextTextureResourceHolder(Game game) : base(game)
        {
            measureGraphics = System.Drawing.Graphics.FromImage(measureBitmap);
            SweepInterval = 60;
        }

        public Texture2D PrepareResource(string text, System.Drawing.Font font)
        {
            int identifier = HashCode.Combine(text, font);
            if (!currentResources.TryGetValue(identifier, out Texture2D texture))
            {
                if (!previousResources.TryGetValue(identifier, out texture))
                {
                    texture = TextTextureRenderer.Resize(text, font, Game.GraphicsDevice, measureGraphics);
                    TextTextureRenderer.RenderText(text, font, texture);
                    currentResources.Add(identifier, texture);
                }
                else
                {
                    currentResources.Add(identifier, texture);
                    previousResources.Remove(identifier);
                }
            }
            return texture;
        }
    }
}
