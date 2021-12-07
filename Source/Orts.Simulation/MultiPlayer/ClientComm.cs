// COPYRIGHT 2012, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using GetText;

using Orts.Simulation;

namespace Orts.MultiPlayer
{
    public class ClientComm : IDisposable
    {
        private readonly TcpClient client;
        private readonly Decoder decoder = new Decoder();
        private bool disposedValue;

        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        public string UserName { get; }
        public string Code { get; }
        public bool Connected { get; set; }

        public void Stop()
        {
            try
            {
                cts.Cancel();
                client.Close();
            }
            catch (Exception ex) when (ex is SocketException || ex is System.IO.IOException)
            { }
        }

        public ClientComm(string serverAddress, int serverPort, string userName, string code)
        {
            client = new TcpClient();

            if (!IPAddress.TryParse(serverAddress, out IPAddress address))
            {
                address = Dns.GetHostEntry(serverAddress)
                     .AddressList
                     .First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            }
            Task connectionTask = Connection(address, serverPort);

            IPEndPoint serverEndPoint = new IPEndPoint(address, serverPort);

            UserName = userName;
            Code = code;
        }

        private async Task Connection(IPAddress address, int port)
        {
            byte[] buffer = new byte[8192];
            int bytesRead;
            string content;

            try
            {
                await client.ConnectAsync(address, port).ConfigureAwait(false);
                NetworkStream networkStream = client.GetStream();

                while (client.Connected && !cts.Token.IsCancellationRequested)
                {
                    bytesRead = await networkStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token).ConfigureAwait(false);
                    if (bytesRead == 0)
                        break;

                    decoder.PushMsg(Encoding.Unicode.GetString(buffer, 0, bytesRead));

                    while ((content = decoder.GetMsg()) != null)
                    {
                        try
                        {
                            Message message = Message.Decode(content);
                            if (Connected || message is MSGRequired)
                                message.HandleMsg();
                        }
                        catch (Exception ex) when (ex is InvalidDataException)
                        { }
                    }
                }
            }
            catch (Exception ex) when (ex is MultiPlayerException)
            {

            }
            catch (Exception ex) when (ex is SocketException || ex is IOException ||
                ex is OperationCanceledException || ex is TaskCanceledException)
            {
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

            MultiPlayerManager.Client = null;
            client.Close();
        }

        public async Task SendMessage(string message)
        {
            try
            {
                if (client.Connected && !cts.IsCancellationRequested)
                {
                    NetworkStream clientStream = client.GetStream();
                    byte[] buffer = Encoding.Unicode.GetBytes(message);
                    await clientStream.WriteAsync(buffer.AsMemory(0, buffer.Length), cts.Token).ConfigureAwait(false);
                    await clientStream.FlushAsync(cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException)
            {
            }

        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    client.Dispose();
                    cts?.Cancel();
                    cts?.Dispose();
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
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
