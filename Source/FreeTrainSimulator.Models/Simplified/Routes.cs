using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

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

            using (SemaphoreSlim addItem = new SemaphoreSlim(1))
            {
                List<Route> result = new List<Route>();

                string routesDirectory = folder.ContentFolder.RoutesFolder;
                if (Directory.Exists(routesDirectory))
                {

                    ActionBlock<string> actionBlock = new ActionBlock<string>
                    (async routeDirectory =>
                    {
                        if (FolderStructure.Route(routeDirectory).IsValid)
                        {
                            try
                            {
                                Route route = new Route(routeDirectory);
                                await addItem.WaitAsync(token).ConfigureAwait(false);
                                result.Add(route);
                            }
                            finally
                            {
                                addItem.Release();
                            }
                        }
                    },
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token });

                    foreach (string routeDirectory in Directory.EnumerateDirectories(routesDirectory))
                        await actionBlock.SendAsync(routeDirectory).ConfigureAwait(false);

                    actionBlock.Complete();
                    await actionBlock.Completion.ConfigureAwait(false);
                }

                return result;
            }
        }
    }
}
