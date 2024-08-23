using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;

using FreeTrainSimulator.Models.Independent.Content;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class ActivityModelCoreHandler: ContentHandlerBase<ActivityModelCore,  ActivityModelCore>
    {
        public static async ValueTask<FrozenSet<ActivityModelCore>> GetActivities(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            string activiesFolder = ModelFileResolver<RouteModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<ActivityModelCore>.WildcardSavePattern;

            ConcurrentBag<ActivityModelCore> results = new ConcurrentBag<ActivityModelCore>();

            //load existing activity models, and compare if the corresponding activity still exists.
            if (Directory.Exists(activiesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(activiesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    ActivityModelCore path = await FromFile(file, routeModel, token, false).ConfigureAwait(false);
                    if (null != path)
                        results.Add(path);
                }).ConfigureAwait(false);
            }
            return results.ToFrozenSet();
        }

    }
}
