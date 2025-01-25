// COPYRIGHT 2020 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team. 


using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

using EmbedIO.Net;

using Orts.ActivityRunner.Viewer3D.WebServices;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class WebServerProcess : IDisposable
    {
        private readonly Thread thread;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private bool disposedValue;
        private readonly int portNumber;

        public WebServerProcess(GameHost game)
        {
            ArgumentNullException.ThrowIfNull(game);

            if (!game.UserSettings.WebServer)
                return;
            portNumber = game.UserSettings.WebServerPort;
            thread = new Thread(WebServerThread);
        }

        public void Start()
        {
            thread?.Start();
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
        }

        private void WebServerThread()
        {
            string contentPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Content\\Web");
            EndPointManager.UseIpv6 = true;
            try
            {
                using (EmbedIO.WebServer server = WebServer.CreateWebServer($"http://*:{portNumber}", contentPath))
                    server.RunAsync(cancellationTokenSource.Token).Wait();
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is SocketException)
                {
                    Trace.TraceWarning($"Port {portNumber} is already in use. Continuing without webserver.");
                }
                else
                {
                    throw;
                }
            }
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cancellationTokenSource?.Cancel();
                    cancellationTokenSource.Dispose();
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
