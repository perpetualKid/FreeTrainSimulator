using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapRenderingLifetime
    {
        BasicShapes GetBasicShapes();

        TextShape GetTextShape(SpriteBatch spriteBatch);

        TextTextureRenderer GetTextTextureRenderer();
    }
}
