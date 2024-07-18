using System;

using FreeTrainSimulator.Online;

using MagicOnion;
using MagicOnion.Server;

namespace Multiplayer.Hub
{
    public class ConnectivityServices : ServiceBase<IConnectivity>, IConnectivity
    {
        public async UnaryResult<long> Connect()
        {
            return await UnaryResult.FromResult(TimeProvider.System.GetTimestamp());
        }
    }
}
