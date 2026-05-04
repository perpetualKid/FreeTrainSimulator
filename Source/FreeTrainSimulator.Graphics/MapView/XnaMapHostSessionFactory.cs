using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapHostSessionFactory : IMapHostSessionFactory
    {
        public IMapHostSession Create(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent)
        {
            return new XnaMapHostSession(new XnaMapAdapterBundle(game, content, mouseInputGameComponent));
        }
    }
}
