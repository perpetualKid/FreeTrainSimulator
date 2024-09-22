using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class PathModelHandler : ContentHandlerBase<PathModel, PathModelCore>
    {
        // MSTS ships with 7 unfinished paths, which cannot be used as they reference tracks that do not exist.
        // MSTS checks for "broken path" before running the simulator and doesn't offer them in the list.
        // ORTS checks for "broken path" when the simulator runs and does offer them in the list.
        // The first activity in Marias Pass is "Explore Longhale" which leads to a "Broken Path" message.
        // The message then confuses users new to ORTS who have just installed it along with MSTS,
        // see https://bugs.launchpad.net/or/+bug/1345172 and https://bugs.launchpad.net/or/+bug/128547
        //private static readonly string[] brokenPaths = {
        //    @"ROUTES\USA1\PATHS\aftstrm(traffic03).pat",
        //    @"ROUTES\USA1\PATHS\aftstrmtraffic01.pat",
        //    @"ROUTES\USA1\PATHS\aiphwne2.pat",
        //    @"ROUTES\USA1\PATHS\aiwnphex.pat",
        //    @"ROUTES\USA1\PATHS\blizzard(traffic).pat",
        //    @"ROUTES\USA2\PATHS\longhale.pat",
        //    @"ROUTES\USA2\PATHS\long-haul west (blizzard).pat",
        //};

        public static async ValueTask<PathModel> Get(string name, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            return await FromFile(name, routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<PathModel> Convert(string filePath, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            if (File.Exists(filePath))
            {
                PathFile patFile = new PathFile(filePath);

                PathModel pathModel = new PathModel()
                {
                    Name = string.IsNullOrEmpty(patFile.Name) ? $"unnamed (@ {Path.GetFileNameWithoutExtension(filePath)})" : patFile.Name,
                    Id = patFile.PathID,
                    PlayerPath = patFile.PlayerPath,
                    Start = string.IsNullOrEmpty(patFile.Start) ? $"unnamed (@ {Path.GetFileNameWithoutExtension(filePath)})" : patFile.Start,
                    End = string.IsNullOrEmpty(patFile.End) ? $"unnamed (@ {Path.GetFileNameWithoutExtension(filePath)})" : patFile.End,
                    Tag = Path.GetFileNameWithoutExtension(filePath),
                };
                await Create(pathModel, routeModel, cancellationToken).ConfigureAwait(false);
                return pathModel;
            }
            return null;
        }

        public static async ValueTask<FrozenSet<PathModelCore>> ConvertPathModels(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            string pathsFolder = ModelFileResolver<PathModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<PathModelCore>.WildcardPattern;

            ConcurrentBag<PathModelCore> results = new ConcurrentBag<PathModelCore>();
            // preload existing MSTS files
            ConcurrentDictionary<string, string> pathFiles = new ConcurrentDictionary<string, string>(Directory.EnumerateFiles(routeModel.MstsRouteFolder().PathsFolder, "*.pat").
                ToDictionary(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

            //load existing path models, and compare if the corresponding folder still exists.
            if (Directory.Exists(pathsFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(pathsFolder, pattern), cancellationToken, async (file, token) =>
                {
                    PathModelCore pathModel = await PathModelCoreHandler.FromFile(file, routeModel, token, false).ConfigureAwait(false);
                    if (pathModel != null && pathFiles.Remove(pathModel.Tag, out string filePath)) //
                    {
                        if (pathModel.SetupRequired())
                            pathModel = await Convert(filePath, routeModel, token).ConfigureAwait(false);
                        results.Add(pathModel);
                    }
                }).ConfigureAwait(false);
            }

            //for any new MSTS path (remaining in the preloaded dictionary), Create a path model
            await Parallel.ForEachAsync(pathFiles, cancellationToken, async (path, token) =>
            {
                PathModelCore pathModel = await Convert(path.Value, routeModel, token).ConfigureAwait(false);
                if (null != pathModel)
                {
                    results.Add(pathModel);
                }
            }).ConfigureAwait(false);

            return results.ToFrozenSet();
        }
    }
}
