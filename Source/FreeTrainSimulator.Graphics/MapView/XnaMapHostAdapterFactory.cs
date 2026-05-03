using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapHostAdapterFactory : IMapHostAdapterFactory
    {
        public IMapHostAdapterBundle Create(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent)
        {
            return new XnaMapAdapterBundle(game, content, mouseInputGameComponent);
        }
    }
}
