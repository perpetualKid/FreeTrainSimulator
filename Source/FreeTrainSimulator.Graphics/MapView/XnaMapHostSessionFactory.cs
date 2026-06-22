using System;

using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class XnaMapHostSessionFactory : IMapHostSessionFactory
    {
        public IMapHostSession Create(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent)
        {
            ArgumentNullException.ThrowIfNull(game);
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(mouseInputGameComponent);

            return new XnaMapHostSession(new XnaMapAdapterBundle(game, content, mouseInputGameComponent));
        }
    }
}
