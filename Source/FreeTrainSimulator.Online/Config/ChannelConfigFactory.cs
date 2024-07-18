using System;
using System.Net.Http;

using Grpc.Net.Client;

namespace FreeTrainSimulator.Online.Config
{
    public static class ClientChannelConfigFactory
    {
        public static GrpcChannel HttpChannel(string server, int port, bool ignoreHttps = false)
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            GrpcChannel channel = GrpcChannel.ForAddress(
                new UriBuilder(ignoreHttps ? Uri.UriSchemeHttp : Uri.UriSchemeHttps, server, port).ToString(),
                new GrpcChannelOptions
                {
                    HttpHandler = ignoreHttps ? handler : null,
                    DisposeHttpClient = true
                });

            return channel;
        }
    }
}
