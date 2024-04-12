using System.IO.Pipelines;
using System.Threading.Tasks;

using MemoryPack;

using MultiPlayer.Shared;

namespace Orts.Simulation.MultiPlayer.Messaging
{
    public static class MessageDecoder
    {
        public static MultiPlayerMessageContent DecodeMessage(MultiPlayerMessage message)
        {
            return message.MessageType switch
            {
                MessageType.Legacy => new LegacyMessage { Payload = message.Payload },
                MessageType.Server => new ServerMessage() { Dispatcher = message.PayloadAsString },
                MessageType.Lost => new LostMessage() { User = message.PayloadAsString },
                MessageType.Chat => MemoryPackSerializer.Deserialize<ChatMessage>(message.Payload),
                _ => throw new ProtocolException($"Unknown Message type {message.MessageType}"),
            };
        }

        public static async Task<MultiPlayerMessage> EncodeMessage(MultiPlayerMessageContent message)
        {
            ReadResult resultBuffer;
            Pipe bufferPipe = new Pipe();
            MessageType messageType = MessageType.Unknown;

            switch (message)
            {
                case ServerMessage dispatcherMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, dispatcherMessage);
                    messageType = MessageType.Server;
                    break;
                case ChatMessage chatMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, chatMessage);
                    messageType = MessageType.Chat;
                    break;
            }

            _ = await bufferPipe.Writer.FlushAsync().ConfigureAwait(false);
            resultBuffer = await bufferPipe.Reader.ReadAsync().ConfigureAwait(false);
            return new MultiPlayerMessage() { MessageType = messageType, Payload = resultBuffer.Buffer };
        }
    }
}
