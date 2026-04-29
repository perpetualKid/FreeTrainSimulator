using System;
using System.Collections.Concurrent;

using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class MapTextTextureCache : IMapTextCache, IDisposable
    {
        private readonly TextTextureRenderer textTextureRenderer;
        private readonly ConcurrentDictionary<int, Texture2D> textures = new ConcurrentDictionary<int, Texture2D>();
        private bool disposed;

        public MapTextTextureCache(TextTextureRenderer textTextureRenderer)
        {
            this.textTextureRenderer = textTextureRenderer ?? throw new ArgumentNullException(nameof(textTextureRenderer));
        }

        public Texture2D GetTextTexture(string message, System.Drawing.Font font, OutlineRenderOptions outlineRenderOptions = null)
        {
            ThrowIfDisposed();

            int identifier = HashCode.Combine(font, message, outlineRenderOptions);
            return textures.GetOrAdd(identifier, _ => textTextureRenderer.RenderText(message, font, outlineRenderOptions));
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            foreach (Texture2D texture in textures.Values)
                texture?.Dispose();

            textures.Clear();
            disposed = true;
        }
    }
}
