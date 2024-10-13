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
    internal sealed class PathModelCoreHandler : ContentHandlerBase<PathModelCore, PathModelCore>
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

        public static async ValueTask<PathModelCore> Get(PathModelCore pathModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));
            return await Get(pathModel.Id, pathModel.Parent, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<PathModelCore> Get(string pathId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(pathId);
            bool renewed = false;

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<PathModelCore>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<PathModelCore>>(FromFile(pathId, routeModel, cancellationToken));
                renewed = true;
            }

            PathModelCore pathModel = await modelTask.Value.ConfigureAwait(false);

            if (pathModel.SetupRequired())
            {
                taskLazyCache[key] = new Lazy<Task<PathModelCore>>(() => Cast(Convert(pathModel, cancellationToken)));
                renewed = true;
            }

            if (renewed)
            {
                key = routeModel.Hierarchy();
                _ = taskSetCache.TryRemove(key, out _);
            }

            return pathModel;
        }

        public static async ValueTask<PathModelCore> Extend(PathModelCore pathModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));

            return pathModel is PathModel ? pathModel : await GetExtended(pathModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<PathModelCore> GetExtended(PathModelCore pathModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));
            return await GetExtended(pathModel.Id, pathModel.Parent, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<PathModel> GetExtended(string pathId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(pathId);
            bool renewed = false;

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<PathModelCore>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted) || (await (modelTask.Value.ConfigureAwait(false)) is not PathModel))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<PathModelCore>>(Cast(FromFile<PathModel, RouteModelCore>(pathId, routeModel, cancellationToken)));
                renewed = true;
            }

            PathModel pathModel = await modelTask.Value.ConfigureAwait(false) as PathModel;

            if (pathModel.SetupRequired())
            {
                taskLazyCache[key] = new Lazy<Task<PathModelCore>>(() => Cast(Convert(pathModel, cancellationToken)));
                renewed = true;
            }

            if (renewed)
            {
                key = routeModel.Hierarchy();
                _ = taskSetCache.TryRemove(key, out _);
            }

            return pathModel;
        }

        public static async ValueTask<FrozenSet<PathModelCore>> GetPaths(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();
            if (!taskSetCache.TryGetValue(key, out Lazy<Task<FrozenSet<PathModelCore>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                modelSetTask = new Lazy<Task<FrozenSet<PathModelCore>>>(() => LoadRefresh(routeModel, cancellationToken));
            }

            FrozenSet<PathModelCore> result = await modelSetTask.Value.ConfigureAwait(false);
            taskSetCache[key] = modelSetTask;
            return result;
        }

        public static async Task ExpandPathModels(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            string pathsFolder = ModelFileResolver<PathModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<PathModelCore>.WildcardPattern;

            ConcurrentBag<PathModelCore> results = new ConcurrentBag<PathModelCore>();

            // load existing MSTS files
            ConcurrentDictionary<string, string> pathFiles = new ConcurrentDictionary<string, string>(Directory.EnumerateFiles(routeModel.MstsRouteFolder().PathsFolder, "*.pat").
                ToDictionary(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

            //load existing path models, and compare if the corresponding path file folder still exists.
            if (Directory.Exists(pathsFolder))
            {
                FrozenSet<PathModelCore> existingPaths = await GetPaths(routeModel, cancellationToken).ConfigureAwait(false);
                foreach (PathModelCore pathModel in existingPaths)
                {
                    if (pathFiles.Remove(pathModel?.Tag, out string filePath)) //
                    {
                        results.Add(pathModel);
                    }
                }
            }

            //for any new MSTS path (remaining in the preloaded dictionary), Create a path model
            await Parallel.ForEachAsync(pathFiles, cancellationToken, async (path, token) =>
            {
                Lazy<Task<PathModelCore>> pathModelTask = new Lazy<Task<PathModelCore>>(Cast(Convert(path.Value, routeModel, cancellationToken)));

                string key = (await pathModelTask.Value.ConfigureAwait(false)).Hierarchy();
                taskLazyCache[key] = pathModelTask;
            }).ConfigureAwait(false);

            //return results.ToFrozenSet();
        }

        private static async Task<FrozenSet<PathModelCore>> LoadRefresh(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            string pathsFolder = ModelFileResolver<RouteModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<PathModelCore>.WildcardSavePattern;

            ConcurrentBag<PathModelCore> results = new ConcurrentBag<PathModelCore>();

            //load existing path models, and compare if the corresponding folder still exists.
            if (Directory.Exists(pathsFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(pathsFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string pathId = Path.GetFileNameWithoutExtension(file);

                    if (pathId.EndsWith(fileExtension))
                        pathId = pathId[..^fileExtension.Length];

                    PathModelCore path = await Get(pathId, routeModel, token).ConfigureAwait(false);
                    if (null != path)
                        results.Add(path);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }

        private static async Task<PathModel> Convert(PathModelCore pathModel, CancellationToken cancellationToken)
        {
            return await Convert(pathModel.Id, pathModel.Parent, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<PathModel> Convert(string filePath, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            if (File.Exists(filePath))
            {
                PathFile patFile = new PathFile(filePath);

                PathModel pathModel = new PathModel()
                {
                    Name = string.IsNullOrEmpty(patFile.Name) ? $"unnamed (@ {Path.GetFileNameWithoutExtension(filePath)})" : patFile.Name.Trim(),
                    Id = patFile.PathID.Trim(),
                    PlayerPath = patFile.PlayerPath,
                    Start = string.IsNullOrEmpty(patFile.Start) ? $"unnamed (@ {Path.GetFileNameWithoutExtension(filePath)})" : patFile.Start.Trim(),
                    End = string.IsNullOrEmpty(patFile.End) ? $"unnamed (@ {Path.GetFileNameWithoutExtension(filePath)})" : patFile.End.Trim(),
                    Tag = Path.GetFileNameWithoutExtension(filePath),
                };
                //this is the case where a file may have been renamed but not the path id, ie. in case of copy cloning, so adopting the filename as path id
                if (string.IsNullOrEmpty(pathModel.Id) || (pathModel.Tag.Length > pathModel.Id.Length && pathModel.Tag.Contains(pathModel.Id)))
                {
                    pathModel = pathModel with { Id = pathModel.Tag };
                }
                await Create(pathModel, routeModel, cancellationToken).ConfigureAwait(false);
                return pathModel;
            }
            return null;
        }
    }
}
