using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Online.Client;
using FreeTrainSimulator.Online.Config;

using Grpc.Net.Client;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Online
{
    [TestClass]
    public class ConnectivityClientTests
    {
        [TestMethod]
        public void ConnectivityClientTest()
        {
            ConnectivityClient client;
            _ = Assert.ThrowsException<ArgumentNullException>(() => client = new ConnectivityClient(null, CancellationToken.None));
        }

        [TestMethod]
        public async Task ChannelHttpTest()
        {
            GrpcChannel channel = ClientChannelConfigFactory.HttpChannel("localhost", 30000, true);
            ConnectivityClient client = new ConnectivityClient(channel, CancellationToken.None);
            Assert.IsFalse(await client.Ping());
        }

        [TestMethod]
        public async Task ChannelHttpLatency()
        {
            GrpcChannel channel = ClientChannelConfigFactory.HttpChannel("localhost", 30000, true);
            ConnectivityClient client = new ConnectivityClient(channel, CancellationToken.None);
            (TimeSpan roundTrip, TimeSpan serverDelta) = await client.TestLatency();
        }
    }
}
