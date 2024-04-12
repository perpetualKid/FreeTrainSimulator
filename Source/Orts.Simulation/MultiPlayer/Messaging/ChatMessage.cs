using System.Threading.Tasks;

using MemoryPack;

namespace Orts.Simulation.MultiPlayer.Messaging
{
    [MemoryPackable]
    public partial class ChatMessage : MultiPlayerMessageContent
    {
        public string Text { get; set; }

        public override void HandleMessage()
        {
        }
    }
}
