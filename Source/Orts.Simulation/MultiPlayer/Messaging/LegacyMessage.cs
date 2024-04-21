using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;

using MemoryPack;

namespace Orts.Simulation.Multiplayer.Messaging
{

    [MemoryPackable]
    public sealed partial class LegacyMessage : MultiPlayerMessageContent
    {
        private static readonly Encoding encoding = Encoding.UTF8;
        private static readonly byte[] blankToken = encoding.GetBytes(" ");
        private static readonly byte[] separatorToken = encoding.GetBytes(": ");

        public ReadOnlySequence<byte> Payload { get; set; }

        public override void HandleMessage()
        {
            ReadOnlySpan<byte> blankSpan = blankToken.AsSpan();
            ReadOnlySpan<byte> messageSpan = Payload.FirstSpan;
            ReadOnlySpan<byte> separatorSpan = separatorToken.AsSpan();

            int messageSizeIndex = messageSpan.IndexOf(separatorSpan);
            int start = messageSizeIndex + separatorToken.Length;
            int messageTypeIndex = messageSpan[start..].IndexOf(blankSpan);
            if (messageTypeIndex > 0)
            {
                ReadOnlySpan<byte> messageType = messageSpan[start..(start + messageTypeIndex)];
                messageTypeIndex += start;
                messageTypeIndex += blankToken.Length;
                ReadOnlySpan<byte> messageData = messageSpan[messageTypeIndex..];

                Message message = Message.Decode(messageType, messageData);
                Task.Run(message.HandleMsg);
                

            }
        }
    }
}
