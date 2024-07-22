using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Simplified
{
    public class Route : ContentBase
    {
        public string Name { get; private set; }
        public string RouteID { get; private set; }
        public string Description { get; private set; }
        public string Path { get; private set; }

        internal FolderStructure.ContentFolder.RouteFolder RouteFolder { get; private set; }

        internal Route(string path)
        {
            RouteFolder = FolderStructure.Route(path);
            string trkFilePath = RouteFolder.TrackFileName;
            try
            {
                RouteFile trkFile = new RouteFile(trkFilePath);
                Name = trkFile.Route.Name;
                RouteID = trkFile.Route.RouteID;
                Description = trkFile.Route.Description;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Name = $"<{catalog.GetString("load error:")} {System.IO.Path.GetFileName(path)}>";
            }
            if (string.IsNullOrEmpty(Name))
                Name = $"<{catalog.GetString("unnamed:")} {System.IO.Path.GetFileNameWithoutExtension(path)}>";
            if (string.IsNullOrEmpty(Description))
                Description = null;
            Path = path;
        }

        public override string ToString()
        {
            return Name;
        }

        public static async Task<IEnumerable<Route>> GetRoutes(Folder folder, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(folder);

            ConcurrentBag<Route> results = new ConcurrentBag<Route>();

            await Parallel.ForEachAsync(Directory.EnumerateDirectories(folder.ContentFolder.RoutesFolder), (routeDirectory, cancellationToken) =>
            {
                if (FolderStructure.Route(routeDirectory).Valid)
                {
                    Route route = new Route(routeDirectory);
                    results.Add(route);
                }
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

            return results.ToFrozenSet();
        }
    }
}
