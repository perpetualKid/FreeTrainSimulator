using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class TestActivityModelHandler : ContentHandlerBase<ActivityModelCore>
    {
        public static Task<ImmutableArray<ActivityModelCore>> GetTestActivities(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            string key = contentModel.Hierarchy();

            if (!modelSetTaskCache.TryGetValue(key, out Task<ImmutableArray<ActivityModelCore>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadActivities(contentModel, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<ImmutableArray<ActivityModelCore>> LoadActivities(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ImmutableArray<FolderModel> folders = contentModel.ContentFolders;
            ConcurrentBag<ActivityModelCore> result = new ConcurrentBag<ActivityModelCore>();

            foreach (FolderModel folder in folders)
            {
                ImmutableArray<RouteModelCore> routes = await folder.GetRoutes(cancellationToken).ConfigureAwait(false);
                foreach (RouteModelCore route in routes)
                {
                    ImmutableArray<ActivityModelCore> activities = await route.GetRouteActivities(cancellationToken).ConfigureAwait(false);

                    foreach (ActivityModelCore activity in activities)
                    {
                        if (activity.ActivityType is not Common.ActivityType.ExploreActivity and not Common.ActivityType.Explorer)
                            result.Add(new TestActivityModel(activity));
                    }
                }
            }
            return result.ToImmutableArray();
        }
    }
}
