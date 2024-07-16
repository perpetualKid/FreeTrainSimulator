using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Online;

using Grpc.Core;
using Grpc.Net.Client;

using MagicOnion.Client;
using MagicOnion.Serialization;
using MagicOnion.Serialization.MemoryPack;

using Orts.Simulation.Multiplayer.Messaging;

namespace Orts.Simulation.Multiplayer
{
    public class MultiPlayerClient : IMultiplayerClient, IDisposable, IAsyncDisposable
    {
        private bool disposed;
        private IMultiplayerHub connection;
        private Collection<MultiPlayerMessageContent> processingMessages = new Collection<MultiPlayerMessageContent>();
        private Collection<MultiPlayerMessageContent> incomingMessages = new Collection<MultiPlayerMessageContent>();

        public bool Connected { get; set; }

        public MultiPlayerClient()
        {
            MagicOnionSerializerProvider.Default = MemoryPackMagicOnionSerializerProvider.Instance;
        }

        public bool Connect(string server, int port)
        {
            return ConnectAsync(server, port).AsTask().Result;
        }

        public void Update(ElapsedTime elapsedTime)
        {
            (processingMessages, incomingMessages) = (incomingMessages, processingMessages);
            foreach (MultiPlayerMessageContent message in processingMessages)
            {
                message.HandleMessage();
            }
            processingMessages.Clear();
        }

        public async ValueTask<bool> ConnectAsync(string server, int port)
        {
            try
            {
                HttpClientHandler handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                GrpcChannel channel = GrpcChannel.ForAddress(new UriBuilder("http", server, port).ToString(), new GrpcChannelOptions { HttpHandler = handler, DisposeHttpClient = true });
                connection = await StreamingHubClient.ConnectAsync<IMultiplayerHub, IMultiplayerClient>(channel, this).ConfigureAwait(false);
                RegisterDisconnect();
                return true;
            }
            catch (RpcException)
            {
                return false;
            }
        }

        public void SendMessage(MultiPlayerMessageContent contentMessage)
        {
            //Task.Run(() => SendMessageAsync(contentMessage));
            SendMessageAsync(contentMessage).AsTask().Wait();
        }

        public async ValueTask SendMessageAsync(MultiPlayerMessageContent contentMessage)
        {
            MultiplayerMessage message = await MessageDecoder.EncodeMessage(contentMessage).ConfigureAwait(false);
            await connection.SendMessageAsync(message).ConfigureAwait(false);
        }

        public void OnReceiveMessage(MultiplayerMessage message)
        {
            ArgumentNullException.ThrowIfNull(message, nameof(message));
            incomingMessages.Add(MessageDecoder.DecodeMessage(message));
        }

        public void JoinGame(string user, string route, string accessCode)
        {
            JoinGameAsync(user, route, accessCode).AsTask().Wait();
        }

        public async ValueTask JoinGameAsync(string user, string route, string room)
        {
            await connection.JoinGameAsync(user, route, room).ConfigureAwait(false);
        }

        public void Stop()
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
