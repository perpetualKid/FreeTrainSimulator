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
    internal sealed class ActivityModelHandler : ContentHandlerBase<ActivityModelHeader>
    {
        public static Task<ActivityModelHeader> GetCore(ActivityModelHeader activityModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));
            return GetCore(activityModel.Id, activityModel.Parent, cancellationToken);
        }

        public static Task<ActivityModelHeader> GetCore(string activityId, RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(activityId);

            if (!modelTaskCache.TryGetValue(key, out Task<ActivityModelHeader> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(activityId, routeModel, cancellationToken);
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return modelTask;
        }

        public static ValueTask<ActivityModel> GetExtended(ActivityModelHeader activityModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(activityModel, nameof(activityModel));
            return activityModel is ActivityModel activityModelExtended ? ValueTask.FromResult(activityModelExtended) : GetExtended(activityModel.Id, activityModel.Parent, cancellationToken);
        }

        public static async ValueTask<ActivityModel> GetExtended(string activityId, RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(activityId);

            if (!modelTaskCache.TryGetValue(key, out Task<ActivityModelHeader> modelTask) || modelTask.IsFaulted ||
                await modelTask.ConfigureAwait(false) is not ActivityModel)
            {
                modelTaskCache[key] = modelTask = Cast(FromFile<ActivityModel, RouteModelHeader>(activityId, routeModel, cancellationToken));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return await modelTask.ConfigureAwait(false) as ActivityModel;
        }

        public static Task<ImmutableArray<ActivityModelHeader>> GetActivities(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<ImmutableArray<ActivityModelHeader>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadActivities(routeModel, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<ImmutableArray<ActivityModelHeader>> LoadActivities(RouteModelHeader routeModel, CancellationToken cancellationToken)
        {
            string activiesFolder = ModelFileResolver<ActivityModelHeader>.FolderPath(routeModel);
            string pattern = ModelFileResolver<ActivityModelHeader>.WildcardSavePattern;

            ConcurrentBag<ActivityModelHeader> results = new ConcurrentBag<ActivityModelHeader>();

            //load existing activit models, and compare if the corresponding folder still exists.
            if (Directory.Exists(activiesFolder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(activiesFolder, pattern), cancellationToken, async (file, token) =>
                {
                    string activityId = Path.GetFileNameWithoutExtension(file);

                    if (activityId.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        activityId = activityId[..^fileExtension.Length];

                    ActivityModelHeader path = await GetCore(activityId, routeModel, token).ConfigureAwait(false);
                    if (null != path)
                        results.Add(path);
                }).ConfigureAwait(false);
            }
            return results.Concat(new ActivityModelHeader[] { CommonModelInstances.ExploreMode, CommonModelInstances.ExploreActivityMode }).ToImmutableArray();
        }
    }
}
