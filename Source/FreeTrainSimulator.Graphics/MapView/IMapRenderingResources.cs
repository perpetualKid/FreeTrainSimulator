using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView.Shapes;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapRenderingResources
    {
        SpriteBatch SpriteBatch { get; }

        BasicShapes BasicShapes { get; }

        TextShape TextShape { get; }
    }
}
