using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Shared
{
    public interface IMultiplayerClient
    {
        void OnReceiveMessage(MultiplayerMessage message);
    }
}
