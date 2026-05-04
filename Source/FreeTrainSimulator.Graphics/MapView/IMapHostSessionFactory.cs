using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal interface IMapHostSessionFactory
    {
        IMapHostSession Create(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent);
    }
}
