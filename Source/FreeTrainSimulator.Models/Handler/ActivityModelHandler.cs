using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class ActivityModelHandler : ContentHandlerBase<ActivityModelCore>
    {
        public static Task<ActivityModelCore> GetCore(ActivityModelCore activityModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));
            return GetCore(activityModel.Id, activityModel.Parent, cancellationToken);
        }

        public static Task<ActivityModelCore> GetCore(string activityId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(activityId);

            if (!modelTaskCache.TryGetValue(key, out Task<ActivityModelCore> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(activityId, routeModel, cancellationToken);
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return modelTask;
        }

        public static ValueTask<ActivityModel> GetExtended(ActivityModelCore activityModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));
            return activityModel is ActivityModel activityModelExtended ? ValueTask.FromResult(activityModelExtended) : GetExtended(activityModel.Id, activityModel.Parent, cancellationToken);
        }

        public static async ValueTask<ActivityModel> GetExtended(string activityId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(activityId);

            if (!modelTaskCache.TryGetValue(key, out Task<ActivityModelCore> modelTask) || modelTask.IsFaulted ||
                await modelTask.ConfigureAwait(false) is not ActivityModel)
            {
                modelTaskCache[key] = modelTask = Cast(FromFile<ActivityModel, RouteModelCore>(activityId, routeModel, cancellationToken));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return await modelTask.ConfigureAwait(false) as ActivityModel;
        }

        public static Task<ImmutableArray<ActivityModelCore>> GetActivities(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<ImmutableArray<ActivityModelCore>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadActivities(routeModel, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<ImmutableArray<ActivityModelCore>> LoadActivities(RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            string activiesFolder = ModelFileResolver<ActivityModelCore>.FolderPath(routeModel);
            string pattern = ModelFileResolver<ActivityModelCore>.WildcardSavePattern;

            ConcurrentBag<ActivityModelCore> results = new ConcurrentBag<ActivityModelCore>();

            //load existing activit models, and compare if the corresponding folder still exists.
            if (Directory.Exists(activiesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(activiesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string activityId = Path.GetFileNameWithoutExtension(file);

                    if (activityId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        activityId = activityId[..^fileExtension.Length];

                    ActivityModelCore path = await GetCore(activityId, routeModel, token).ConfigureAwait(false);
                    if (null != path)
                        results.Add(path);
                }).ConfigureAwait(false);
            }
            return results.Concat(new ActivityModelCore[] { CommonModelInstances.ExploreMode, CommonModelInstances.ExploreActivityMode }).ToImmutableArray();
        }
    }
}
