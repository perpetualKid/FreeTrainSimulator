using System;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapHostResources : IMapHostResources
    {
        private readonly MapTextTextureCache ownedTextCache;
        private readonly SpriteBatch spriteBatch;

        public XnaMapHostResources(SpriteBatch spriteBatch, MapTextTextureCache ownedTextCache)
        {
            this.spriteBatch = spriteBatch ?? throw new ArgumentNullException(nameof(spriteBatch));
            this.ownedTextCache = ownedTextCache ?? throw new ArgumentNullException(nameof(ownedTextCache));
        }

        public void Dispose()
        {
            ownedTextCache?.Dispose();
            spriteBatch?.Dispose();
        }
    }
}
