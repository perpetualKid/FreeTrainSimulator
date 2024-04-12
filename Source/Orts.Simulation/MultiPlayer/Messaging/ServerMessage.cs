using System;
using System.Text;
using System.Threading.Tasks;

using MemoryPack;

namespace Orts.Simulation.MultiPlayer.Messaging
{

    [MemoryPackable]
    public partial class ServerMessage: MultiPlayerMessageContent
    {
        public string Dispatcher {  get; set; }

        public override void HandleMessage()
        {
            Task.Run(() => new MSGServer(new ReadOnlySpan<byte>(Encoding.UTF8.GetBytes(Dispatcher))).HandleMsg());            
        }
    }
}
