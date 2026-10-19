using System;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapHostResources : IMapHostResources
    {
        private readonly MapTextTextureCache ownedTextCache;
        private readonly SpriteBatch spriteBatch;
        private readonly IDisposable hostEnvironment;

        public XnaMapHostResources(SpriteBatch spriteBatch, MapTextTextureCache ownedTextCache, IDisposable hostEnvironment)
        {
            this.spriteBatch = spriteBatch ?? throw new ArgumentNullException(nameof(spriteBatch));
            this.ownedTextCache = ownedTextCache ?? throw new ArgumentNullException(nameof(ownedTextCache));
            this.hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        }

        public void Dispose()
        {
            hostEnvironment.Dispose();
            ownedTextCache?.Dispose();
            spriteBatch?.Dispose();
        }
    }
}
