using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class PathModelCoreHandler : ContentHandlerBase<PathModelCore, PathModelCore>
    {
        public static async ValueTask<FrozenSet<PathModelCore>> GetPaths(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            string pathsFolder = ModelFileResolver<RouteModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<PathModelCore>.WildcardSavePattern;

            ConcurrentBag<PathModelCore> results = new ConcurrentBag<PathModelCore>();

            //load existing path models, and compare if the corresponding folder still exists.
            if (Directory.Exists(pathsFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(pathsFolder, pattern), cancellationToken, async (file, token) =>
                {
                    PathModelCore path = await FromFile(file, routeModel, token, false).ConfigureAwait(false);
                    if (null != path)
                        results.Add(path);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }
    }
}
