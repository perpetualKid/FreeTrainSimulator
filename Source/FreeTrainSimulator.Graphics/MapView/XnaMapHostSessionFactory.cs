using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public sealed class XnaMapHostSessionFactory : IMapHostSessionFactory
    {
        private readonly IMapHostAdapterFactory adapterFactory;

        public XnaMapHostSessionFactory()
            : this(new XnaMapHostAdapterFactory())
        {
        }

        public XnaMapHostSessionFactory(IMapHostAdapterFactory adapterFactory)
        {
            this.adapterFactory = adapterFactory;
        }

        public IMapHostSession Create(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent)
        {
            return new XnaMapHostSession(adapterFactory.Create(game, content, mouseInputGameComponent));
        }
    }
}
