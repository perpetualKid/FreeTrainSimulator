using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapHostResources
    {
        SpriteBatch SpriteBatch { get; }

        void Dispose();
    }
}
