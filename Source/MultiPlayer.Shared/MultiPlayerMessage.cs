using System.Buffers;
using System.Text;

using MemoryPack;

namespace MultiPlayer.Shared
{
    [MemoryPackable()]
    public partial class MultiPlayerMessage
    {
        public MessageType MessageType { get; set; }

        public ReadOnlySequence<byte> Payload { get; set; }

        [MemoryPackIgnore]
        public string PayloadAsString
        {
            get => Encoding.UTF8.GetString(Payload);
            set => Payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(value));
        }
    }
}
