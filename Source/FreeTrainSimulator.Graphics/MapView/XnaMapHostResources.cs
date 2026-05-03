using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapHostResources : IMapHostResources
    {
        private readonly MapTextTextureCache ownedTextCache;

        public SpriteBatch SpriteBatch { get; }

        public XnaMapHostResources(SpriteBatch spriteBatch, MapTextTextureCache ownedTextCache)
        {
            SpriteBatch = spriteBatch;
            this.ownedTextCache = ownedTextCache;
        }

        public void Dispose()
        {
            ownedTextCache?.Dispose();
            SpriteBatch?.Dispose();
        }
    }
}
