using System;
using System.Net.Http;
using System.Threading.Tasks;

using Grpc.Net.Client;

using MagicOnion.Client;
using MagicOnion.Serialization;
using MagicOnion.Serialization.MemoryPack;

using MultiPlayer.Shared;

using Orts.Simulation.MultiPlayer.Messaging;

namespace Orts.Simulation.MultiPlayer
{
    public class MultiPlayerClient : IMultiPlayerClient, IDisposable, IAsyncDisposable
    {
        private bool disposed;
        private IMultiPlayerHub connection;

        public bool Connected { get; set; }

        public MultiPlayerClient()
        {
            MagicOnionSerializerProvider.Default = MemoryPackMagicOnionSerializerProvider.Instance;
        }

        public void Connect(string server, int port, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            ConnectAsync(server, port).AsTask().Wait();
        }

        public async ValueTask ConnectAsync(string server, int port)
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            GrpcChannel channel = GrpcChannel.ForAddress(new UriBuilder("http", server, port).ToString(), new GrpcChannelOptions { HttpHandler = handler, DisposeHttpClient = true });
            connection = await StreamingHubClient.ConnectAsync<IMultiPlayerHub, IMultiPlayerClient>(channel, this).ConfigureAwait(false);
            RegisterDisconnect();
        }

        public void SendLegacyMessage(string payload, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            SendLegacyMessageAsync(payload).AsTask().Wait();
        }

        public async ValueTask SendLegacyMessageAsync(string payload)
        {
            MultiPlayerMessage message = new MultiPlayerMessage() { MessageType = MessageType.Legacy, PayloadAsString = payload };
            await connection.SendMessageAsync(message).ConfigureAwait(false);
        }

        public async ValueTask SendMessageAsync(MultiPlayerMessageContent contentMessage)
        {
            MultiPlayerMessage message = await MessageDecoder.EncodeMessage(contentMessage).ConfigureAwait(false);
            await connection.SendMessageAsync(message).ConfigureAwait(false);
        }

        public void OnReceiveMessage(MultiPlayerMessage message)
        {
            MultiPlayerMessageContent messageContent = MessageDecoder.DecodeMessage(message);
            messageContent.HandleMessage();
        }

        public void JoinGame(string user, string route, string accessCode, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            JoinGameAsync(user, route, accessCode).AsTask().Wait();
        }

        public async ValueTask JoinGameAsync(string user, string route, string accessCode)
        {
            await connection.JoinGameAsync(user, route, accessCode).ConfigureAwait(false);
        }

        public void Stop([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            StopAsync().AsTask().Wait();
        }

        public async ValueTask StopAsync()
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            await connection.WaitForDisconnect().ConfigureAwait(false);
        }

        private async void RegisterDisconnect()
        {
            await connection.WaitForDisconnect().ConfigureAwait(false);
            //
        }

        #region IDisposable Implementation
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (connection is IDisposable disposable)
                    {
                        disposable.Dispose();
                        connection = null;
                    }
                }
            }
        }
        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (connection != null)
                await connection.DisposeAsync().ConfigureAwait(false);

            connection = null;
            disposed = true;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            // Perform async cleanup.
            await DisposeAsyncCore().ConfigureAwait(true);

            // Dispose of unmanaged resources.
            Dispose(false);

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
