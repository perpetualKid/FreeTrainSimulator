using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Online.Config;

using Grpc.Net.Client;

using MagicOnion.Client;
using MagicOnion.Serialization;
using MagicOnion.Serialization.MemoryPack;

namespace FreeTrainSimulator.Online.Client
{
    public class ConnectivityClient
    {
        private readonly IConnectivity client;

        static ConnectivityClient()
        {
            MagicOnionSerializerProvider.Default = MemoryPackMagicOnionSerializerProvider.Instance;
        }

        public ConnectivityClient(string server, int port, CancellationToken cancellationToken, bool ignoreHttps = false)
        {
            if (string.IsNullOrEmpty(server))
                throw new ArgumentException("Server name is null or empty.", nameof(server));

            GrpcChannel channel = ClientChannelConfigFactory.HttpChannel(server, port, ignoreHttps);

            client = MagicOnionClient.Create<IConnectivity>(channel).WithCancellationToken(cancellationToken);
        }

        public ConnectivityClient(GrpcChannel channel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(channel, nameof(channel));

            MagicOnionSerializerProvider.Default = MemoryPackMagicOnionSerializerProvider.Instance;

            client = MagicOnionClient.Create<IConnectivity>(channel).WithCancellationToken(cancellationToken);
        }

        public async ValueTask<bool> Ping()
        {
            try
            {
                _ = await client.Connect();
                return true;
            }
            catch (Exception ex) when (ex is Exception)
            {
                return false;
            }
        }

        public async ValueTask<(TimeSpan roundTrip, TimeSpan serverDelta)> TestLatency()
        {
            try
            {
                long start = TimeProvider.System.GetTimestamp();
                long serverTime = await client.Connect();
                long end = TimeProvider.System.GetTimestamp();

                TimeSpan roundtrip = TimeProvider.System.GetElapsedTime(start, end);
                TimeSpan serverDelta = TimeProvider.System.GetElapsedTime(serverTime, (end + start) / 2);

                return (roundtrip, serverDelta);
            }
            catch (Exception ex) when (ex is Exception)
            {
                return (TimeSpan.Zero, TimeSpan.Zero);
            }
        }

    }
}
