using System;
using System.Threading.Tasks;

using MagicOnion.Serialization;
using MagicOnion.Serialization.MemoryPack;
using MagicOnion.Server.Hubs;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Multiplayer.Shared;

namespace Multiplayer.Hub
{
    internal sealed class Program
    {
        private static async Task Main(string[] args)
        {
            Console.Title = ThisAssembly.AssemblyName;

            int port = 30000;
            if (args.Length > 0 && !int.TryParse(args[0], out port))
                port = 30000;

            MagicOnionSerializerProvider.Default = MemoryPackMagicOnionSerializerProvider.Instance;

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            _ = builder.WebHost.ConfigureKestrel(options =>
            {
                // Accept HTTP/2 only to allow insecure HTTP/2 connections
                options.ConfigureEndpointDefaults(endpointOptions =>
                {
                    endpointOptions.Protocols = HttpProtocols.Http2;
                });
            }).ConfigureLogging((discard, logging) =>
            {
                _ = logging.SetMinimumLevel(LogLevel.Warning);

            });
            _ = builder.Services.AddGrpc();  // MagicOnion depends on ASP.NET Core gRPC service.
            MagicOnion.Server.IMagicOnionServerBuilder server = builder.Services.AddMagicOnion();

            WebApplication app = builder.Build();
            _ = app.MapMagicOnionService();
            _ = app.Services.GetService<StreamingHubBase<IMultiplayerHub, IMultiplayerClient>>();
            Task webApplicationTask = app.RunAsync($"http://*:{port}");

            Console.WriteLine($"Multiplayer Server v {ThisAssembly.AssemblyInformationalVersion} is now running on port {port}");
            foreach (var url in app.Urls)
                Console.WriteLine($"\t{url}");
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            Console.WriteLine("For further information, bug reports or discussions, please visit");
            Console.WriteLine("\thttps://github.com/perpetualKid/FreeTrainSimulator");
            Console.WriteLine("Use Ctrl+C to stop the service");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            Console.WriteLine();

            await webApplicationTask.ConfigureAwait(false);
        }
    }
}
