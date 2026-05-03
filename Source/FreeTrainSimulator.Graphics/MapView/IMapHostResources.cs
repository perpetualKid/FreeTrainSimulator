using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapHostResources
    {
        SpriteBatch SpriteBatch { get; }

        void Dispose();
    }
}
