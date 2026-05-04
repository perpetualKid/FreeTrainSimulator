using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public interface IMapHostAdapterFactory
    {
        IMapHostAdapterBundle Create(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent);
    }
}
