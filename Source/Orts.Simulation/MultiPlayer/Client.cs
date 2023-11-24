using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using GetText;

using Orts.Common;

namespace Orts.Simulation.MultiPlayer
{
    internal class Client : IDisposable
    {
        private readonly TcpClient tcpClient;
        private bool disposedValue;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        private static readonly Encoding encoding = Encoding.Unicode;
        private static readonly byte[] separatorToken = encoding.GetBytes(": ");
        private static readonly byte[] blankToken = encoding.GetBytes(" ");

        public bool Connected { get; set; }

        public Client(string serverAddress, int serverPort)
        {
            tcpClient = new TcpClient();

            if (!IPAddress.TryParse(serverAddress, out IPAddress address))
            {
                address = Dns.GetHostEntry(serverAddress)
                     .AddressList
                     .First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            }
            Task connectionTask = Connect(address, serverPort);

            IPEndPoint serverEndPoint = new IPEndPoint(address, serverPort);
        }

        public async Task Connect(IPAddress address, int port)
        {
            try
            {
                await tcpClient.ConnectAsync(address, port).ConfigureAwait(false);

                Pipe pipe = new Pipe();

                await Task.WhenAll(PipeFillAsync(tcpClient, pipe.Writer), PipeReadAsync(tcpClient, pipe.Reader)).ConfigureAwait(false);

                tcpClient.Close();
            }
            catch (SocketException socketException)
            {
                Trace.TraceError(socketException.Message);
                Connected = false;
            }

            Simulator.Instance.Confirmer?.Error(CatalogManager.Catalog.GetString("Connection to the server is lost, will play as single mode"));
            foreach (System.Collections.Generic.KeyValuePair<string, OnlinePlayer> p in MultiPlayerManager.OnlineTrains.Players)
            {
                MultiPlayerManager.Instance().AddRemovedPlayer(p.Value);
            }

            //no matter what, let player gain back the control of the player train
            if (Simulator.Instance.PlayerLocomotive?.Train != null)
            {
                Simulator.Instance.PlayerLocomotive.Train.TrainType = TrainType.Player;
                Simulator.Instance.PlayerLocomotive.Train.LeadLocomotive = Simulator.Instance.PlayerLocomotive;
            }
            Simulator.Instance.Confirmer?.Information(CatalogManager.Catalog.GetString("Alt-E to gain control of your train"));

            MultiPlayerManager.Stop();

        }

        public async Task SendMessage(Message message)
        {
            char[] originalMessage = ArrayPool<char>.Shared.Rent(message.EstimatedMessageSize);
            using (IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(encoding.GetMaxByteCount(message.EstimatedMessageSize)))
            {
                int bytesWritten = encoding.GetBytes(message.Serialize(originalMessage), owner.Memory.Span);
                try
                {
                    if (tcpClient.Connected && !cts.IsCancellationRequested)
                    {
                        NetworkStream clientStream = tcpClient.GetStream();
                        await clientStream.WriteAsync(owner.Memory[..bytesWritten], cts.Token).ConfigureAwait(false);
                        await clientStream.FlushAsync(cts.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException || ex is InvalidOperationException)
                {
                    Trace.TraceError($"Error sending Multiplayer Message: {ex.Message}");
                    await cts.CancelAsync().ConfigureAwait(false);
                }
                ArrayPool<char>.Shared.Return(originalMessage);
            }
        }

        public async Task SendMessage(string message)
        {
            try
            {
                if (tcpClient.Connected && !cts.IsCancellationRequested)
                {
                    NetworkStream clientStream = tcpClient.GetStream();
                    byte[] buffer = encoding.GetBytes(message);
                    await clientStream.WriteAsync(buffer.AsMemory(0, buffer.Length), cts.Token).ConfigureAwait(false);
                    await clientStream.FlushAsync(cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException || ex is InvalidOperationException)
            {
                Trace.TraceError($"Error sending Multiplayer Message: {ex.Message}");
                await cts.CancelAsync().ConfigureAwait(false);
            }
        }

        public void Stop()
        {
            try
            {
                cts.Cancel();
                tcpClient.Close();
            }
            catch (Exception ex) when (ex is SocketException || ex is System.IO.IOException)
            { }
        }

        private SequencePosition DecodeMessage(ReadOnlySequence<byte> sequence, bool completed)
        {
            ReadOnlySpan<byte> separatorSpan = separatorToken.AsSpan();
            ReadOnlySpan<byte> blankSpan = blankToken.AsSpan();
            Decoder decoder = encoding.GetDecoder();

            SequenceReader<byte> reader = new SequenceReader<byte>(sequence);

            while (!reader.End && reader.TryReadTo(out ReadOnlySequence<byte> sizeIndicator, separatorSpan, true) && !sizeIndicator.IsEmpty)
            {
                string sizeText = sizeIndicator.GetString(encoding);
                int lengthStart;

                for (lengthStart = sizeText.Length; lengthStart > 0; lengthStart--)
                {
                    if (!char.IsDigit(sizeText[lengthStart - 1]))
                        break;
                }

                byte[] oversized = null;
                ReadOnlySpan<byte> GetSpanInternal(ReadOnlySequence<byte> payload)
                {
                    // linearize
                    oversized = ArrayPool<byte>.Shared.Rent(checked((int)payload.Length));
                    payload.CopyTo(oversized);
                    return oversized.AsSpan(0, (int)payload.Length);
                }

                if (int.TryParse(sizeText[lengthStart..], out int length)) // found a length indicator
                {
                    if (reader.Remaining >= (length *= 2) || completed) // enough data is present?
                    {
                        // this will be our message
                        ReadOnlySpan<byte> messageSpan;
                        if (!completed)
                        {
                            ReadOnlySequence<byte> contentSequence = reader.Sequence.Slice(reader.Consumed, length);
                            messageSpan = (contentSequence.IsSingleSegment ? contentSequence.FirstSpan : GetSpanInternal(contentSequence));
                        }
                        else
                        {
                            ReadOnlySequence<byte> contentSequence = reader.Sequence.Slice(reader.Consumed);
                            messageSpan = (contentSequence.IsSingleSegment ? contentSequence.FirstSpan : GetSpanInternal(contentSequence));
                        }

//                        string messageString = encoding.GetString(messageSpan);
                        int messageTypeIndex = messageSpan.IndexOf(blankSpan);
                        if (messageTypeIndex > 0)
                        {
                            ReadOnlySpan<byte> messageType = messageSpan[..messageTypeIndex];
                            messageTypeIndex += blankToken.Length;
                            ReadOnlySpan<byte> messageData = messageSpan[messageTypeIndex..];

                            Message message = Message.Decode(messageType, messageData);
                            if (Connected || message is MSGRequired)
                                message.HandleMsg();

                        }
                        if (oversized != null)
                            ArrayPool<byte>.Shared.Return(oversized);

                        reader.Advance(length);
                    }
                    else // message length header is indicating more data than currently available, going back to where the length indicator starts and wait for more
                    {
                        reader.Rewind(sizeIndicator.Length + separatorSpan.Length);
                        break;
                    }
                }
                else // no valid size indicator, skipping ahead
                {
                    reader.Advance(sizeIndicator.Length);
                }
            }

            return reader.Position;
        }

        private async Task PipeFillAsync(TcpClient tcpClient, PipeWriter writer)
        {
            const int minimumBufferSize = 512;
            NetworkStream networkStream = tcpClient.GetStream();

            while (tcpClient.Connected && !cts.Token.IsCancellationRequested)
            {
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                int bytesRead = await networkStream.ReadAsync(memory, cts.Token).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }
                writer.Advance(bytesRead);

                FlushResult result = await writer.FlushAsync().ConfigureAwait(false);

                if (result.IsCompleted)
                {
                    break;
                }
            }
            await writer.CompleteAsync().ConfigureAwait(false);
        }

        private async Task PipeReadAsync(TcpClient tcpClient, PipeReader reader)
        {
            while (tcpClient.Client.Connected && !cts.Token.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(cts.Token).ConfigureAwait(false);

                ReadOnlySequence<byte> buffer = result.Buffer;

                SequencePosition position = DecodeMessage(buffer, result.IsCompleted || result.IsCanceled);
                reader.AdvanceTo(position);

                if (result.IsCompleted || result.IsCanceled)
                {
                    break;
                }
            }

            await reader.CompleteAsync().ConfigureAwait(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    if (!cts.IsCancellationRequested)
                        cts.Cancel();
                    cts.Dispose();
                    tcpClient?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public static class ReadOnlySequenceExtensions
    {
        public static string GetString(in this ReadOnlySequence<byte> payload, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;

            return payload.IsSingleSegment ? encoding.GetString(payload.FirstSpan)
                : GetStringInternal(payload, encoding);

            static string GetStringInternal(in ReadOnlySequence<byte> payload, Encoding encoding)
            {
                // linearize
                int length = checked((int)payload.Length);
                byte[] oversized = ArrayPool<byte>.Shared.Rent(length);
                try
                {
                    payload.CopyTo(oversized);
                    return encoding.GetString(oversized, 0, length);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(oversized);
                }
            }
        }
    }
}
