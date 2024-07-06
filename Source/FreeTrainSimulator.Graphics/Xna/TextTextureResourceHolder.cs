using System;
using System.Diagnostics;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Xna
{
    public class TextTextureResourceHolder : ResourceGameComponent<Texture2D>
    {
        private readonly TextTextureRenderer textRenderer;

        public Texture2D EmptyTexture => textRenderer.EmptyTexture;

        private TextTextureResourceHolder(Game game, int sweepInterval) : base(game)
        {
            SweepInterval = sweepInterval;
            textRenderer = TextTextureRenderer.Instance(game) ?? throw new InvalidOperationException("TextTextureRenderer not found");
        }

        public static TextTextureResourceHolder Instance(Game game)
        {
            ArgumentNullException.ThrowIfNull(game);

            TextTextureResourceHolder instance;
            if ((instance = game.Components.OfType<TextTextureResourceHolder>().FirstOrDefault()) == null)
                instance = new TextTextureResourceHolder(game, 30);
            return instance;
        }

        public Texture2D PrepareResource(string text, System.Drawing.Font font, OutlineRenderOptions outline = null)
        {
            int identifier = HashCode.Combine(text, font, outline);
            if (!currentResources.TryGetValue(identifier, out Texture2D texture))
            {
                if (previousResources.TryRemove(identifier, out texture))
                {
                    if (!currentResources.TryAdd(identifier, texture))
                        Trace.TraceInformation($"Texture Resource '{text}' already added.");
                }
                else
                {
                    texture = textRenderer.RenderText(text, font, outline);
                    if (!currentResources.TryAdd(identifier, texture))
                    {
                        texture.Dispose();
                        if (!currentResources.TryGetValue(identifier, out texture))
                            Trace.TraceError($"Texture Resource '{text}' not found. Retrying.");
                    }
                }
            }
            return texture;
        }
    }
}
