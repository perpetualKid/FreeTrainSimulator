using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class TestActivityModelHandler : ContentHandlerBase<ActivityModelCore>
    {
        public static Task<FrozenSet<ActivityModelCore>> GetTestActivities(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            string key = contentModel.Hierarchy();

            if (!modelSetTaskCache.TryGetValue(key, out Task<FrozenSet<ActivityModelCore>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadActivities(contentModel, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<FrozenSet<ActivityModelCore>> LoadActivities(ContentModel contentModel, CancellationToken cancellationToken)
        {
            FrozenSet<FolderModel> folders = contentModel.ContentFolders;
            ConcurrentBag<ActivityModelCore> result = new ConcurrentBag<ActivityModelCore>();

            foreach (FolderModel folder in folders)
            {
                FrozenSet<RouteModelCore> routes = await folder.GetRoutes(cancellationToken).ConfigureAwait(false);
                foreach (RouteModelCore route in routes)
                {
                    FrozenSet<ActivityModelCore> activities = await route.GetRouteActivities(cancellationToken).ConfigureAwait(false);

                    foreach (ActivityModelCore activity in activities)
                    {
                        if (activity.ActivityType is not Common.ActivityType.ExploreActivity and not Common.ActivityType.Explorer)
                            result.Add(new TestActivityModel(activity));
                    }
                }
            }
            return result.ToFrozenSet();
        }
    }
}
