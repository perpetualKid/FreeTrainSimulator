using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class PathModelHandler : ContentHandlerBase<PathModelCore>
    {
        internal const string SourceNameKey = "MstsSourcePath";

        // MSTS ships with 7 unfinished paths, which cannot be used as they reference tracks that do not exist.
        // MSTS checks for "broken path" before running the simulator and doesn't offer them in the list.
        // I.e. the first activity in Marias Pass is "Explore Longhale" which leads to a "Broken Path" message.
        // The message then confuses new users who have just started to play activities from MSTS,
        //private static readonly string[] brokenPaths = {
        //    @"ROUTES\USA1\PATHS\aftstrm(traffic03).pat",
        //    @"ROUTES\USA1\PATHS\aftstrmtraffic01.pat",
        //    @"ROUTES\USA1\PATHS\aiphwne2.pat",
        //    @"ROUTES\USA1\PATHS\aiwnphex.pat",
        //    @"ROUTES\USA1\PATHS\blizzard(traffic).pat",
        //    @"ROUTES\USA2\PATHS\longhale.pat",
        //    @"ROUTES\USA2\PATHS\long-haul west (blizzard).pat",
        //};

        public static ValueTask<PathModelCore> GetCore(PathModelCore pathModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));
            return GetCore(pathModel.Id, pathModel.Parent, cancellationToken);
        }

        public static async ValueTask<PathModelCore> GetCore(string pathId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(pathId);

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<PathModelCore>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<PathModelCore>>(FromFile(pathId, routeModel, cancellationToken));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            PathModelCore pathModel = await modelTask.Value.ConfigureAwait(false);

            if (pathModel?.RefreshRequired ?? false)
            {
                taskLazyCache[key] = new Lazy<Task<PathModelCore>>(() => Cast(Convert(pathModel, cancellationToken)));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return pathModel;
        }

        public static ValueTask<PathModel> GetExtended(PathModelCore pathModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));
            return pathModel is PathModel pathModelExtended ? ValueTask.FromResult(pathModelExtended) : GetExtended(pathModel.Id, pathModel.Parent, cancellationToken);
        }

        public static async ValueTask<PathModel> GetExtended(string pathId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(pathId);

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<PathModelCore>> modelTask) || !modelTask.IsValueCreated || 
                (modelTask.IsValueCreated && (modelTask.Value.IsFaulted || (await modelTask.Value.ConfigureAwait(false) is not PathModel))))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<PathModelCore>>(Cast(FromFile<PathModel, RouteModelCore>(pathId, routeModel, cancellationToken)));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            PathModel pathModel = await modelTask.Value.ConfigureAwait(false) as PathModel;

            if (pathModel?.RefreshRequired ?? false)
            {
                taskLazyCache[key] = new Lazy<Task<PathModelCore>>(() => Cast(Convert(pathModel, cancellationToken)));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return pathModel;
        }

        public static async ValueTask<FrozenSet<PathModelCore>> GetPaths(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !taskLazyCollectionCache.TryGetValue(key, out Lazy<Task<FrozenSet<PathModelCore>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<PathModelCore>>>(() => LoadPaths(routeModel, cancellationToken));
            }

            return await modelSetTask.Value.ConfigureAwait(false);
        }

        public static async Task<FrozenSet<PathModelCore>> ExpandPathModels(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            string pathsFolder = ModelFileResolver<PathModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<PathModelCore>.WildcardPattern;

            ConcurrentBag<PathModelCore> results = new ConcurrentBag<PathModelCore>();

            string sourceFolder = routeModel.MstsRouteFolder().PathsFolder;
            if (Directory.Exists(sourceFolder))
            {
                // load existing MSTS files
                ConcurrentDictionary<string, string> pathFiles = new ConcurrentDictionary<string, string>(Directory.EnumerateFiles(sourceFolder, "*.pat").
                    ToDictionary(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);

                await Parallel.ForEachAsync(pathFiles, cancellationToken, async (path, token) =>
                {
                    Lazy<Task<PathModelCore>> modelTask = new Lazy<Task<PathModelCore>>(Cast(Convert(path.Value, routeModel, cancellationToken)));

                    PathModelCore pathModel = await modelTask.Value.ConfigureAwait(false);
                    string key = pathModel.Hierarchy();
                    results.Add(pathModel);
                    taskLazyCache[key] = modelTask;
                }).ConfigureAwait(false);
            }
            FrozenSet<PathModelCore> result = results.ToFrozenSet();
            string key = routeModel.Hierarchy();
            Lazy<Task<FrozenSet<PathModelCore>>> modelSetTask;
            taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<PathModelCore>>>(Task.FromResult(result));
            collectionUpdateRequired.TryRemove(key, out _);
            return result;
        }

        private static async Task<FrozenSet<PathModelCore>> LoadPaths(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            string pathsFolder = ModelFileResolver<PathModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<PathModelCore>.WildcardSavePattern;

            ConcurrentBag<PathModelCore> results = new ConcurrentBag<PathModelCore>();

            //load existing path models, and compare if the corresponding folder still exists.
            if (Directory.Exists(pathsFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(pathsFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string pathId = Path.GetFileNameWithoutExtension(file);

                    if (pathId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        pathId = pathId[..^fileExtension.Length];

                    PathModelCore path = await GetCore(pathId, routeModel, token).ConfigureAwait(false);
                    if (null != path)
                        results.Add(path);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }

        private static Task<PathModel> Convert(PathModelCore pathModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(pathModel, nameof(pathModel));

            return Convert(pathModel.Parent.MstsRouteFolder().PathFile(pathModel.Tags[SourceNameKey]), pathModel.Parent, cancellationToken);
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
                    Tags = new Dictionary<string, string> { { SourceNameKey, Path.GetFileNameWithoutExtension(filePath) } },
                };
                //this is the case where a file may have been renamed but not the path id, ie. in case of copy cloning, so adopting the filename as path id
                if (string.IsNullOrEmpty(pathModel.Id) || (!string.Equals(pathModel.Tags[SourceNameKey].Trim(), pathModel.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    Trace.TraceWarning($"Path file {filePath} refers to path Id {pathModel.Id}. Renaming to {pathModel.Tags[SourceNameKey]}");
                    pathModel = pathModel with { Id = pathModel.Tags[SourceNameKey] };
                }
                await Create(pathModel, routeModel, cancellationToken).ConfigureAwait(false);
                return pathModel;
            }
            else
            {
                Trace.TraceWarning($"Path file {filePath} refers to non-existing file.");
                return null;
            }
        }
    }
}
