using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Xna;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapRenderingLifetime
    {
        BasicShapes GetBasicShapes();

        TextTextureRenderer GetTextTextureRenderer();
    }
}
