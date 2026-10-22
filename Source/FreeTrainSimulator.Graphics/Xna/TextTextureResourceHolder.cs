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

        /// <summary>Prepares a cache-owned texture without transferring disposal responsibility to the caller.</summary>
        public void PrepareResource(string text, System.Drawing.Font font) => PrepareResource(text, font, null);

        /// <summary>Prepares a cache-owned texture; empty results continue to borrow the renderer's sentinel.</summary>
        public void PrepareResource(string text, System.Drawing.Font font, OutlineRenderOptions outline)
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
                    Texture2D acquired = null;
                    try
                    {
                        acquired = textRenderer.RenderText(text, font, outline);
                        // Empty text borrows the renderer's sentinel; never sweep or dispose it here.
                        if (ReferenceEquals(acquired, textRenderer.EmptyTexture))
                        {
                            acquired = null;
                            return;
                        }
                        if (currentResources.TryAdd(identifier, acquired))
                            acquired = null;
                    }
                    finally
                    {
                        acquired?.Dispose();
                    }
                }
            }
        }

        /// <summary>Gets a borrowed cached texture, preparing it if needed. The caller must not dispose it.</summary>
        public Texture2D this[string text, System.Drawing.Font font] => this[text, font, null];

        /// <summary>Gets a borrowed cached texture, preparing it if needed. The caller must not dispose it.</summary>
        public Texture2D this[string text, System.Drawing.Font font, OutlineRenderOptions outline]
        {
            get
            {
                PrepareResource(text, font, outline);
                return currentResources.TryGetValue(HashCode.Combine(text, font, outline), out Texture2D texture)
                    ? texture : textRenderer.EmptyTexture;
            }
        }

        /// <summary>Measures the rendered size without allocating or borrowing a device texture.</summary>
        public System.Drawing.Size Measure(string text, System.Drawing.Font font) => Measure(text, font, null);

        /// <summary>Measures the rendered size, including the sentinel dimensions for empty results.</summary>
        public System.Drawing.Size Measure(string text, System.Drawing.Font font, OutlineRenderOptions outline)
        {
            System.Drawing.Size size = textRenderer.Measure(text, font, outline);
            return size.Width == 0 || size.Height == 0
                ? new System.Drawing.Size(EmptyTexture.Width, EmptyTexture.Height) : size;
        }
    }
}
