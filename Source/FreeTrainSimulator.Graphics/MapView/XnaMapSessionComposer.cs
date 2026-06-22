using System;

using FreeTrainSimulator.Common.Input;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public sealed class XnaMapSessionComposer : IMapSessionComposer
    {
        private readonly Game game;
        private readonly MouseInputGameComponent mouseInputGameComponent;
        private readonly IMapHostSessionFactory hostSessionFactory;

        public XnaMapSessionComposer(Game game, MouseInputGameComponent mouseInputGameComponent)
            : this(game, mouseInputGameComponent, new XnaMapHostSessionFactory())
        {
        }

        internal XnaMapSessionComposer(Game game, MouseInputGameComponent mouseInputGameComponent, IMapHostSessionFactory hostSessionFactory)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            this.mouseInputGameComponent = mouseInputGameComponent ?? throw new ArgumentNullException(nameof(mouseInputGameComponent));
            this.hostSessionFactory = hostSessionFactory ?? throw new ArgumentNullException(nameof(hostSessionFactory));
        }

        public IMapSession Compose(MapSessionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return new XnaMapSession(new ContentArea(game, request.Content, mouseInputGameComponent, hostSessionFactory));
        }
    }
}
