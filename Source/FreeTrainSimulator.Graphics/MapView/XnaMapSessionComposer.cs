using System;
using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    public sealed class XnaMapSessionComposer : IMapSessionComposer
    {
        private readonly IMapHostSessionFactory hostSessionFactory;

        public XnaMapSessionComposer()
            : this(new XnaMapHostSessionFactory())
        {
        }

        internal XnaMapSessionComposer(IMapHostSessionFactory hostSessionFactory)
        {
            this.hostSessionFactory = hostSessionFactory;
        }

        public IMapSession Compose(MapSessionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return new XnaMapSession(new ContentArea(request.Game, request.Content, request.MouseInput, hostSessionFactory));
        }
    }
}
