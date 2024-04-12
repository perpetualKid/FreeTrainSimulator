using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MultiPlayer.Shared
{
    public interface IMultiPlayerClient
    {
        void OnReceiveMessage(MultiPlayerMessage message);
    }
}
