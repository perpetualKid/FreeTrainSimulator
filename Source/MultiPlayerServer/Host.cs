using System;
using System.Collections.Generic;
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

        private readonly Dictionary<string, TcpClient> onlinePlayers = new Dictionary<string, TcpClient>();
        private readonly byte[] initData = Encoding.Unicode.GetBytes("10: SERVER YOU");
        private readonly byte[] serverChallenge = Encoding.Unicode.GetBytes(" 21: SERVER WhoCanBeServer");
        private bool serverAppointed;
        private byte[] lostPlayer;
        private string currentServer;

        public Host(int port)
        {
            this.port = port;
        }

        public async void Run()
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
                    TcpClient tcpClient = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    Task t = Process(tcpClient);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw new InvalidOperationException("Invalid Program state, aborting.", ex);
                }
            }
        }

        private void Broadcast(string playerName, ReadOnlyMemory<byte> buffer)
        {
            Console.WriteLine(Encoding.Unicode.GetString(buffer.Span).Replace("\r", Environment.NewLine, StringComparison.OrdinalIgnoreCase));
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
                    catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException)
                    {
                        await RemovePlayer(playerName).ConfigureAwait(false);
                    }
                }
            });
        }

        private async Task SendMessage(string playerName, ReadOnlyMemory<byte> buffer)
        {
            Console.WriteLine(Encoding.Unicode.GetString(buffer.Span).Replace("\r", Environment.NewLine, StringComparison.OrdinalIgnoreCase));
            try
            {
                TcpClient client = onlinePlayers[playerName];
                NetworkStream clientStream = client.GetStream();
                await clientStream.WriteAsync(buffer).ConfigureAwait(false);
                await clientStream.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException)
            {
                await RemovePlayer(playerName).ConfigureAwait(false);
            }
        }

        private async Task Process(TcpClient tcpClient)
        {
            string playerName = Guid.NewGuid().ToString();
            onlinePlayers.Add(playerName, tcpClient);
            if (onlinePlayers.Count == 1)
            {
                currentServer = playerName;
                await SendMessage(playerName, initData).ConfigureAwait(false);
            }

            NetworkStream networkStream = tcpClient.GetStream();
            byte[] buffer = new byte[8192];

            int size;
            while (tcpClient.Connected)
            {
                try
                {
                    size = await networkStream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException)
                {
                    break;
                }
                if (size == 0)
                    break;
                byte[] sendBuffer = new byte[size];
                Array.Copy(buffer, sendBuffer, size);
                string message = Encoding.Unicode.GetString(sendBuffer);
                string[] messageDetails = message.Split(' ');
                if (messageDetails != null && messageDetails.Length > 3)
                {
                    if (messageDetails[2].Equals("PLAYER", StringComparison.OrdinalIgnoreCase))
                    {
                        onlinePlayers.Remove(playerName);
                        if (currentServer == playerName)
                            currentServer = playerName = message.Split(' ')[3];
                        else
                            playerName = message.Split(' ')[3];
                        onlinePlayers.Add(playerName, tcpClient);
                    }
                    if (messageDetails[2].Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        string quitPlayer = message.Split(' ')[3];
                        if (playerName == quitPlayer)
                            break;
                    }
                }
                Broadcast(playerName, sendBuffer.AsMemory(0, sendBuffer.Length));
            }
            await RemovePlayer(playerName).ConfigureAwait(false);
        }

        private async Task RemovePlayer(string playerName)
        {
            if (onlinePlayers.Remove(playerName))
            {
                string lostMessage = $"LOST { playerName}";
                lostPlayer = Encoding.Unicode.GetBytes($" {lostMessage.Length}: {lostMessage}");
                Broadcast(playerName, lostPlayer);
                if (currentServer == playerName)
                {
                    serverAppointed = false;
                    Broadcast(playerName, serverChallenge);
                    await Task.Delay(5000).ConfigureAwait(false);
                    if (!serverAppointed && onlinePlayers.Count > 0)
                    {
                        Broadcast(null, lostPlayer);
                        currentServer = onlinePlayers.Keys.First();
                        string appointmentMessage = $"SERVER {currentServer}";
                        lostPlayer = Encoding.Unicode.GetBytes($" {appointmentMessage.Length}: {appointmentMessage}");
                        Broadcast(null, lostPlayer);
                    }
                }
            }
        }
    }
}
