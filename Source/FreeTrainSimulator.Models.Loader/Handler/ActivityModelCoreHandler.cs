using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class ActivityModelCoreHandler : ContentHandlerBase<ActivityModelCore, ActivityModelCore>
    {
        public static async ValueTask<ActivityModelCore> Get(ActivityModelCore activityModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));
            return await Get(activityModel.Id, activityModel.Parent, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<ActivityModelCore> Get(string activityId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(activityId);
            bool renewed = false;

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<ActivityModelCore>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<ActivityModelCore>>(FromFile(activityId, routeModel, cancellationToken));
                renewed = true;
            }

            ActivityModelCore activityModel = await modelTask.Value.ConfigureAwait(false);

            if (activityModel.SetupRequired())
            {
                taskLazyCache[key] = new Lazy<Task<ActivityModelCore>>(() => ActivityModelHandler.Cast(ActivityModelHandler.Convert(activityModel, cancellationToken)));
                renewed = true;
            }

            if (renewed)
            {
                key = routeModel.Hierarchy();
                _ = taskSetCache.TryRemove(key, out _);
            }

            return activityModel;
        }

        public static async ValueTask<FrozenSet<ActivityModelCore>> GetActivities(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            if (!taskSetCache.TryGetValue(key, out Lazy<Task<FrozenSet<ActivityModelCore>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                modelSetTask = new Lazy<Task<FrozenSet<ActivityModelCore>>>(() => LoadRefresh(routeModel, cancellationToken));
            }

            FrozenSet<ActivityModelCore> result = await modelSetTask.Value.ConfigureAwait(false);
            taskSetCache[key] = modelSetTask;
            return result;
        }

        private static async Task<FrozenSet<ActivityModelCore>> LoadRefresh(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            string activiesFolder = ModelFileResolver<RouteModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<ActivityModelCore>.WildcardSavePattern;

            ConcurrentBag<ActivityModelCore> results = new ConcurrentBag<ActivityModelCore>();

            //load existing activity models, and compare if the corresponding activity still exists.
            if (Directory.Exists(activiesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(activiesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string activityId = Path.GetFileNameWithoutExtension(file);

                    if (activityId.EndsWith(fileExtension))
                        activityId = activityId[..^fileExtension.Length];

                    ActivityModelCore activity = await Get(activityId, routeModel, token).ConfigureAwait(false);
                    if (null != activity)
                        results.Add(activity);
                }).ConfigureAwait(false);
            }
            return results.Concat(new ActivityModelCore[] { ActivityModelHandler.Explorer, ActivityModelHandler.ExploreActivity }).ToFrozenSet();
        }
    }
}
