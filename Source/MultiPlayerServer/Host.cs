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

        private readonly Dictionary<string, TcpClient> tcpClients = new Dictionary<string, TcpClient>();
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
                    throw;
                }
            }
        }

        private void Broadcast(TcpClient sender, ReadOnlyMemory<byte> buffer)
        {
            Console.WriteLine(Encoding.Unicode.GetString(buffer.Span).Replace("\r", Environment.NewLine, StringComparison.OrdinalIgnoreCase));
            Parallel.ForEach(tcpClients.Values, async client =>
            {
                if (client != sender)
                {
                    NetworkStream clientStream = client.GetStream();
                    await clientStream.WriteAsync(buffer).ConfigureAwait(false);
                    await clientStream.FlushAsync().ConfigureAwait(false);
                }
            });
        }

        private static async Task SendMessage(TcpClient client, ReadOnlyMemory<byte> buffer)
        {
            Console.WriteLine(Encoding.Unicode.GetString(buffer.Span).Replace("\r", Environment.NewLine, StringComparison.OrdinalIgnoreCase));
            NetworkStream clientStream = client.GetStream();
            await clientStream.WriteAsync(buffer).ConfigureAwait(false);
            await clientStream.FlushAsync().ConfigureAwait(false);
        }

        private async Task Process(TcpClient tcpClient)
        {
            NetworkStream networkStream = tcpClient.GetStream();
            byte[] buffer = new byte[8192];

            int size = await networkStream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
            byte[] sendBuffer = new byte[size];
            Array.Copy(buffer, sendBuffer, size);
            string playerMessage = Encoding.Unicode.GetString(sendBuffer);
            string[] playerDetails = playerMessage.Split(' ');
            if (playerDetails == null || playerDetails.Length < 3 || !playerDetails[2].Equals("PLAYER", StringComparison.OrdinalIgnoreCase))
                return;
            string playerName = playerMessage.Split(' ')[3];
            Broadcast(tcpClient, sendBuffer);

            if (tcpClients.Count == 0)
            {
                currentServer = playerName;
                await SendMessage(tcpClient, initData).ConfigureAwait(false);
            }
            tcpClients.Add(playerName, tcpClient);

            while (tcpClient.Connected)
            {
                size = await networkStream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
                if (size == 0)
                    break;
                sendBuffer = new byte[size];
                Array.Copy(buffer, sendBuffer, size);
                Broadcast(tcpClient, sendBuffer.AsMemory(9, sendBuffer.Length));
            }
            tcpClients.Remove(playerName);
            string lostMessage = $"LOST { playerName}";
            lostPlayer = Encoding.Unicode.GetBytes($" {lostMessage.Length}: {lostMessage}");
            Broadcast(tcpClient, lostPlayer);
            if (currentServer == playerName)
            {
                serverAppointed = false;
                Broadcast(tcpClient, serverChallenge);
                await Task.Delay(5000).ConfigureAwait(false);
                if (!serverAppointed)
                {
                    Broadcast(null, lostPlayer);
                    currentServer = tcpClients.Keys.First();
                    string appointmentMessage = $"SERVER {currentServer}";
                    lostPlayer = Encoding.Unicode.GetBytes($" {appointmentMessage.Length}: {appointmentMessage}");
                    Broadcast(null, lostPlayer);
                }
            }
        }
    }
}
