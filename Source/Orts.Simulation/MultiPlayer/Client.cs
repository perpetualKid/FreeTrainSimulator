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
            System.Text.Decoder decoder = encoding.GetDecoder();
            byte[] tempBytes = null;

            SequenceReader<byte> reader = new SequenceReader<byte>(sequence);

            while (!reader.End)
            {
                if (reader.TryReadTo(out ReadOnlySequence<byte> sizeIndicator, separatorSpan, true) && !sizeIndicator.IsEmpty)
                {
                    ReadOnlySpan<byte> span;
                    if (!sizeIndicator.IsSingleSegment) //if not single segment, need to get all the data in a temporary buffer. However, this is very unlikely to happen
                    {
                        tempBytes = ArrayPool<byte>.Shared.Rent(checked((int)sizeIndicator.Length));
                        sizeIndicator.CopyTo(tempBytes);
                        span = tempBytes;
                    }
                    else
                        span = sizeIndicator.FirstSpan;
                    int index = MemoryExtensions.LastIndexOf(span, blankSpan);
                    if (index > -1 || span.Length < 14) // looking either for a preceeding blank separator, or the begining of the data, but not expecting more than 6 digits ( (~1MB)
                    {
                        if (index < 0) //no blank separator upfront, so we just try from beginning
                            index = 0;

                        ReadOnlySpan<byte> lengthSpan = span[index..];
#pragma warning disable CA2014 // Do not use stackalloc in loops
                        Span<char> numberSpan = stackalloc char[decoder.GetCharCount(lengthSpan, true)];
#pragma warning restore CA2014 // Do not use stackalloc in loops
                        decoder.GetChars(lengthSpan, numberSpan, true);
                        int lengthStart;
                        for (lengthStart = numberSpan.Length; lengthStart > 0; lengthStart--)
                        {
                            if (!char.IsDigit(numberSpan[lengthStart - 1]))
                                break;
                        }
                        if (int.TryParse(numberSpan[lengthStart..], out int length) && reader.Remaining >= (length *= 2)) // found a length indicator and enough data is present
                        {
                            //reader.TryReadTo(out ReadOnlySequence<byte> messageType, blankSpan);
                            //ReadOnlySequence<byte> messageSequence = reader.Sequence.Slice(0, length);

                            if (!reader.Sequence.IsSingleSegment)
                                Debugger.Break();
                            ReadOnlySpan<byte> messageSpan = reader.UnreadSpan[..length]; // this is our message
                            int messageTypeIndex = messageSpan.IndexOf(blankSpan);
                            if (messageTypeIndex > 0)
                            {
                                ReadOnlySpan<byte> messageType = messageSpan[..messageTypeIndex];
                                ReadOnlySpan<byte> messageData = messageSpan[++messageTypeIndex..];

                                Message message = Message.Decode(encoding.GetString(messageSpan));
                                if (Connected || message is MSGRequired)
                                    message.HandleMsg();
                            }
                            reader.Advance(length);
                        }
                        else // message length header is indicating more data than currently available, going back to where the length indicator starts and wait for more
                        {
                            reader.Rewind(lengthSpan.Length + separatorSpan.Length);
                            break;
                        }
                    }
                    else
                    {
                        reader.Advance(sizeIndicator.Length);
                    }
                    if (tempBytes != null)
                        ArrayPool<byte>.Shared.Return(tempBytes);
                }
                else if (completed)
                {
                    reader.Advance(sequence.Length);
                }
                else
                {
                    break;
                }
            }

            return reader.Position;
        }

        private async Task PipeFillAsync(TcpClient tcpClient, PipeWriter writer)
        {
            const int minimumBufferSize = 128;
            NetworkStream networkStream = tcpClient.GetStream();

            while (tcpClient.Connected)
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
            while (tcpClient.Client.Connected)
            {
                ReadResult result = await reader.ReadAsync(cts.Token).ConfigureAwait(false);

                ReadOnlySequence<byte> buffer = result.Buffer;

                SequencePosition position = DecodeMessage(buffer, result.IsCompleted);
                reader.AdvanceTo(position, buffer.End);

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
}
