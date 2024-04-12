using System.Threading.Tasks;

using MemoryPack;

namespace Orts.Simulation.MultiPlayer.Messaging
{
    [MemoryPackable]
    public partial class LostMessage : MultiPlayerMessageContent
    {
        public string User { get; set; }

        public override void HandleMessage()
        {
            Task.Run(() => new MSGLost(User).HandleMsg());
        }
    }
}
