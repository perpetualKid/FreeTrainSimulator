using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapShellHost
    {
        IMapHostControl HostControl { get; }
    }
}
