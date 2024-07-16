using System.IO.Pipelines;
using System.Threading.Tasks;

using FreeTrainSimulator.Online;

using MemoryPack;

namespace Orts.Simulation.Multiplayer.Messaging
{
    internal static class MessageDecoder
    {
        public static MultiPlayerMessageContent DecodeMessage(MultiplayerMessage message)
        {
            return message.MessageType switch
            {
                MessageType.Server => new ServerMessage() { Dispatcher = message.PayloadAsString },
                MessageType.Lost => new LostMessage() { User = message.PayloadAsString },
                MessageType.Chat => MemoryPackSerializer.Deserialize<ChatMessage>(message.Payload),
                MessageType.Aider => MemoryPackSerializer.Deserialize<AiderMessage>(message.Payload),
                MessageType.Quit => MemoryPackSerializer.Deserialize<QuitMessage>(message.Payload),
                MessageType.TimeCheck => MemoryPackSerializer.Deserialize<TimeCheckMessage>(message.Payload),
                MessageType.TrainEvent => MemoryPackSerializer.Deserialize<TrainEventMessage>(message.Payload),
                MessageType.Weather => MemoryPackSerializer.Deserialize<WeatherMessage>(message.Payload),
                MessageType.Control => MemoryPackSerializer.Deserialize<ControlMessage>(message.Payload),
                MessageType.TrainControl => MemoryPackSerializer.Deserialize<TrainControlMessage>(message.Payload),
                MessageType.SignalReset => MemoryPackSerializer.Deserialize<SignalResetMessage>(message.Payload),
                MessageType.Exhaust => MemoryPackSerializer.Deserialize<ExhaustMessage>(message.Payload),
                MessageType.Move => MemoryPackSerializer.Deserialize<MoveMessage>(message.Payload),
                MessageType.RemoveTrain => MemoryPackSerializer.Deserialize<RemoveTrainMessage>(message.Payload),
                MessageType.SwitchStates => MemoryPackSerializer.Deserialize<SwitchStateMessage>(message.Payload),
                MessageType.SignalStates => MemoryPackSerializer.Deserialize<SignalStateMessage>(message.Payload),
                MessageType.SignalChange => MemoryPackSerializer.Deserialize<SignalChangeMessage>(message.Payload),
                MessageType.SwitchChange => MemoryPackSerializer.Deserialize<SwitchChangeMessage>(message.Payload),
                MessageType.LocomotiveInfo => MemoryPackSerializer.Deserialize<LocomotiveStateMessage>(message.Payload),
                MessageType.LocomotiveChange => MemoryPackSerializer.Deserialize<LocomotiveChangeMessage>(message.Payload),
                MessageType.MovingTable => MemoryPackSerializer.Deserialize<MovingTableMessage>(message.Payload),
                MessageType.PlayerTrainChange => MemoryPackSerializer.Deserialize<PlayerTrainChangeMessage>(message.Payload),
                MessageType.PlayerState => MemoryPackSerializer.Deserialize<PlayerStateMessage>(message.Payload),
                MessageType.TrainState => MemoryPackSerializer.Deserialize<TrainStateMessage>(message.Payload),
                MessageType.TrainFlip => MemoryPackSerializer.Deserialize<TrainFlipMessage>(message.Payload),
                MessageType.TrainRequest => MemoryPackSerializer.Deserialize<TrainRequestMessage>(message.Payload),
                MessageType.TrainUpdate => MemoryPackSerializer.Deserialize<TrainUpdateMessage>(message.Payload),
                MessageType.TrainCouple => MemoryPackSerializer.Deserialize<TrainCoupleMessage>(message.Payload),
                MessageType.TrainUncouple => MemoryPackSerializer.Deserialize<TrainUncoupleMessage>(message.Payload),
                _ => throw new ProtocolException($"Unknown Message type {message.MessageType}"),
            };
        }

        public static async Task<MultiplayerMessage> EncodeMessage(MultiPlayerMessageContent message)
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
                case AiderMessage aiderMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, aiderMessage);
                    messageType = MessageType.Aider;
                    break;
                case QuitMessage quitMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, quitMessage);
                    messageType = MessageType.Quit;
                    break;
                case TimeCheckMessage timeCheckMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, timeCheckMessage);
                    messageType = MessageType.TimeCheck;
                    break;
                case TrainEventMessage trainEventMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, trainEventMessage);
                    messageType = MessageType.TrainEvent;
                    break;
                case WeatherMessage weatherMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, weatherMessage);
                    messageType = MessageType.Weather;
                    break;
                case ControlMessage controlMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, controlMessage);
                    messageType = MessageType.Control;
                    break;
                case TrainControlMessage trainControlMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, trainControlMessage);
                    messageType = MessageType.TrainControl;
                    break;
                case SignalResetMessage signalResetMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, signalResetMessage);
                    messageType = MessageType.SignalReset;
                    break;
                case ExhaustMessage exhaustMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, exhaustMessage);
                    messageType = MessageType.Exhaust;
                    break;
                case MoveMessage moveMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, moveMessage);
                    messageType = MessageType.Move;
                    break;
                case RemoveTrainMessage removeTrainMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, removeTrainMessage);
                    messageType = MessageType.RemoveTrain;
                    break;
                case SwitchStateMessage switchStateMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, switchStateMessage);
                    messageType = MessageType.SwitchStates;
                    break;
                case SignalStateMessage signalStateMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, signalStateMessage);
                    messageType = MessageType.SignalStates;
                    break;
                case SignalChangeMessage signalChangeMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, signalChangeMessage);
                    messageType = MessageType.SignalChange;
                    break;
                case SwitchChangeMessage switchChangeMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, switchChangeMessage);
                    messageType = MessageType.SwitchChange;
                    break;
                case LocomotiveStateMessage locomotiveStateMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, locomotiveStateMessage);
                    messageType = MessageType.LocomotiveInfo;
                    break;
                case LocomotiveChangeMessage locomotiveChangeMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, locomotiveChangeMessage);
                    messageType = MessageType.LocomotiveChange;
                    break;
                case MovingTableMessage movingTableMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, movingTableMessage);
                    messageType = MessageType.MovingTable;
                    break;
                case PlayerTrainChangeMessage playerTrainChangeMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, playerTrainChangeMessage);
                    messageType = MessageType.PlayerTrainChange;
                    break;
                case PlayerStateMessage playerStateMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, playerStateMessage);
                    messageType = MessageType.PlayerState;
                    break;
                case TrainStateMessage trainStateMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, trainStateMessage);
                    messageType = MessageType.TrainState;
                    break;
                case TrainFlipMessage trainFlipMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, trainFlipMessage);
                    messageType = MessageType.TrainFlip;
                    break;
                case TrainRequestMessage trainRequestMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, trainRequestMessage);
                    messageType = MessageType.TrainRequest;
                    break;
                case TrainUpdateMessage trainUpdateMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, trainUpdateMessage);
                    messageType = MessageType.TrainUpdate;
                    break;
                case TrainCoupleMessage trainCoupleMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, trainCoupleMessage);
                    messageType = MessageType.TrainCouple;
                    break;
                case TrainUncoupleMessage trainUncoupleMessage:
                    MemoryPackSerializer.Serialize(bufferPipe.Writer, trainUncoupleMessage);
                    messageType = MessageType.TrainUncouple;
                    break;
            }

            _ = await bufferPipe.Writer.FlushAsync().ConfigureAwait(false);
            resultBuffer = await bufferPipe.Reader.ReadAsync().ConfigureAwait(false);
            return new MultiplayerMessage() { MessageType = messageType, Payload = resultBuffer.Buffer };
        }
    }
}
