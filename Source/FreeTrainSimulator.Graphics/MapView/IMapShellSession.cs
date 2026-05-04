using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapShellSession : IMapSession
    {
        IMapShellHost ShellHost { get; }
    }
}
