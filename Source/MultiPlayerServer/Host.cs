using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Orts.MultiPlayerServer
{
    //whoever connects first, will become dispatcher(server) by sending a "SERVER YOU" message
    //if a clients sends a "SERVER MakeMeServer", this client should be appointed new server
    //track which clients are leaving - if the client was the server, send a new "SERVER WhoCanBeServer" announcement
    //if there is no response within xx seconds, appoint a new server by sending "SERVER YOU" to the first/last/random remaining client
    public class Host
    {
        private readonly int port;

        private static readonly Encoding encoding = Encoding.Unicode;
        private readonly Dictionary<string, TcpClient> onlinePlayers = new Dictionary<string, TcpClient>();
        private static readonly byte[] initData = encoding.GetBytes("10: SERVER YOU");
        private static readonly byte[] serverChallenge = encoding.GetBytes(" 21: SERVER WhoCanBeServer");
        private static readonly byte[] blankToken = encoding.GetBytes(" ");
        private static readonly byte[] playerToken = encoding.GetBytes("PLAYER ");
        private static readonly byte[] quitToken = encoding.GetBytes("QUIT ");
        private string currentServer;

        public Host(int port)
        {
            this.port = port;
        }

        public async Task Run()
        {
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                Console.WriteLine($"MultiPlayer Server is now running on port {port}");
                Console.WriteLine("For further information, bug reports or discussions, please visit");
                Console.WriteLine("\t\thttps://github.com/perpetualKid/ORTS-MG");
                Console.WriteLine("Hit <enter> to stop service");
                Console.WriteLine();
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                while (true)
                {
                    try
                    {
                        Pipe pipe = new Pipe();

                        TcpClient tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                        _ = PipeFillAsync(tcpClient, pipe.Writer);
                        _ = PipeReadAsync(tcpClient, pipe.Reader);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        throw new InvalidOperationException("Invalid Program state, aborting.", ex);
                    }
                }
            }
            catch (SocketException socketException)
            {
                Console.WriteLine(socketException.Message);
                throw;
            }
        }

        private async Task PipeFillAsync(TcpClient tcpClient, PipeWriter writer)
        {
            const int minimumBufferSize = 1024;
            _ = currentServer;
            NetworkStream networkStream = tcpClient.GetStream();

            while (tcpClient.Connected)
            {
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);

                int bytesRead = await networkStream.ReadAsync(memory).ConfigureAwait(false);
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

        private static string ReadPlayerName(ReadOnlySequence<byte> sequence)
        {
            Span<byte> playerSeparator = playerToken.AsSpan();
            Span<byte> blankSeparator = blankToken.AsSpan();

            SequenceReader<byte> reader = new SequenceReader<byte>(sequence);

            while (!reader.End)
            {
                if (reader.TryReadTo(out _, playerSeparator))
                {
                    if (reader.TryReadTo(out ReadOnlySequence<byte> playerName, blankSeparator))
                    {
                        return encoding.GetString(playerName.FirstSpan);
                    }
                }
                else
                {
                    break;
                }
            }
            return null;
        }

        private static string ReadQuitMessage(ReadOnlySequence<byte> sequence)
        {
            Span<byte> quitSeparator = quitToken.AsSpan();
            Span<byte> blankSeparator = blankToken.AsSpan();

            SequenceReader<byte> reader = new SequenceReader<byte>(sequence);

            while (!reader.End)
            {
                if (reader.TryReadTo(out _, quitSeparator))
                {
                    return encoding.GetString(reader.UnreadSpan);
                }
                else
                {
                    break;
                }
            }
            return null;
        }

        private async Task PipeReadAsync(TcpClient tcpClient, PipeReader reader)
        {
            string playerName = Guid.NewGuid().ToString();
            bool playerNameSet = false;
            string quitPlayer;
            onlinePlayers.Add(playerName, tcpClient);
            if (onlinePlayers.Count == 1)
            {
                currentServer = playerName;
                await SendMessage(playerName, initData).ConfigureAwait(false);
            }

            while (tcpClient.Client.Connected)
            {
                ReadResult result = await reader.ReadAsync().ConfigureAwait(false);

                ReadOnlySequence<byte> buffer = result.Buffer;

                if (!playerNameSet)
                {
                    string player = ReadPlayerName(buffer);

                    if (string.IsNullOrEmpty(player))
                        break;

                    onlinePlayers.Remove(playerName);
                    if (currentServer == playerName)
                        currentServer = playerName = player;
                    else
                        playerName = player;
                    onlinePlayers.Add(playerName, tcpClient);
                    playerNameSet = true;

                    if (currentServer != player)
                        await SendMessage(currentServer, buffer.First).ConfigureAwait(true);
                    reader.AdvanceTo(buffer.End, buffer.End);
                }
                else
                {
                    if (!string.IsNullOrEmpty(quitPlayer = ReadQuitMessage(buffer)))
                    {
                        if (playerName == quitPlayer)
                            break;
                    }

                    foreach (ReadOnlyMemory<byte> message in buffer)
                    {
                        Broadcast(playerName, message);
                    }
                    reader.AdvanceTo(buffer.End, buffer.End);
                }

                if (result.IsCompleted)
                {
                    break;
                }
            }

            await RemovePlayer(playerName).ConfigureAwait(false);

            await reader.CompleteAsync().ConfigureAwait(false);
        }

        private void Broadcast(string playerName, ReadOnlyMemory<byte> buffer)
        {
            Console.WriteLine(encoding.GetString(buffer.Span).Replace("\r", Environment.NewLine, StringComparison.OrdinalIgnoreCase));
            Parallel.ForEach(onlinePlayers.Keys, async player =>
            {
                if (player != playerName)
                {
                    try
                    {
                        TcpClient client = onlinePlayers[player];
                        NetworkStream clientStream = client.GetStream();
                        await clientStream.WriteAsync(buffer).ConfigureAwait(false);
                        await clientStream.FlushAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException || ex is InvalidOperationException)
                    {
                        await RemovePlayer(playerName).ConfigureAwait(false);
                    }
                }
            });
        }

        private async Task SendMessage(string playerName, ReadOnlyMemory<byte> buffer)
        {
            Console.WriteLine(encoding.GetString(buffer.Span).Replace("\r", Environment.NewLine, StringComparison.OrdinalIgnoreCase));
            try
            {
                TcpClient client = onlinePlayers[playerName];
                NetworkStream clientStream = client.GetStream();
                await clientStream.WriteAsync(buffer).ConfigureAwait(false);
                await clientStream.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException || ex is InvalidOperationException)
            {
                await RemovePlayer(playerName).ConfigureAwait(false);
            }
        }

        private async Task RemovePlayer(string playerName)
        {
            if (onlinePlayers.Remove(playerName))
            {
                string lostMessage = $"LOST { playerName}";
                byte[] lostPlayer = encoding.GetBytes($" {lostMessage.Length}: {lostMessage}");
                Broadcast(playerName, lostPlayer);
                if (currentServer == playerName)
                {
                    Broadcast(playerName, serverChallenge);
                    await Task.Delay(5000).ConfigureAwait(false);
                    if (onlinePlayers.Count > 0)
                    {
                        Broadcast(null, lostPlayer);
                        currentServer = onlinePlayers.Keys.First();
                        string appointmentMessage = $"SERVER {currentServer}";
                        lostPlayer = encoding.GetBytes($" {appointmentMessage.Length}: {appointmentMessage}");
                        Broadcast(null, lostPlayer);
                    }
                }
            }
        }
    }
}
