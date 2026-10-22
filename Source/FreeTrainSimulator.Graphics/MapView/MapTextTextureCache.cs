using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class MapTextTextureCache : IMapTextCache, IDisposable
    {
        private readonly TextTextureRenderer textTextureRenderer;
        private readonly ConcurrentDictionary<int, Texture2D> textures = new ConcurrentDictionary<int, Texture2D>();
        private bool disposed;
        private ExceptionDispatchInfo releaseFailure;

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
            {
                releaseFailure?.Throw();
                return;
            }

            disposed = true;
            try
            {
                List<Exception> errors = null;
                foreach (Texture2D texture in textures.Values)
                {
                    // Empty results borrow the renderer's sentinel; only generated textures belong to this cache.
                    if (ReferenceEquals(texture, textTextureRenderer.EmptyTexture))
                        continue;

                    try
                    {
                        texture?.Dispose();
                    }
                    catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                    {
                        (errors ??= new List<Exception>()).Add(error);
                    }
                }

                textures.Clear();
                if (errors != null)
                {
                    releaseFailure = ExceptionDispatchInfo.Capture(new AggregateException(errors));
                    releaseFailure.Throw();
                }
            }
            catch (Exception error)
            {
                releaseFailure = ExceptionDispatchInfo.Capture(error);
                throw;
            }
        }
    }
}
