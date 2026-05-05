using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IXnaMapShellHost : IMapShellHost
    {
        DrawableGameComponent Component { get; }
    }
}
