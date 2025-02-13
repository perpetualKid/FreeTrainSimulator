using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Models.Handler
{
    internal sealed class TestActivityModelHandler : ContentHandlerBase<ActivityModelHeader>
    {
        public static Task<ImmutableArray<ActivityModelHeader>> GetTestActivities(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(contentModel, nameof(contentModel));

            string key = contentModel.Hierarchy();

            if (!modelSetTaskCache.TryGetValue(key, out Task<ImmutableArray<ActivityModelHeader>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadActivities(contentModel, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<ImmutableArray<ActivityModelHeader>> LoadActivities(ContentModel contentModel, CancellationToken cancellationToken)
        {
            ImmutableArray<FolderModel> folders = contentModel.ContentFolders;
            ConcurrentBag<ActivityModelHeader> result = new ConcurrentBag<ActivityModelHeader>();

            foreach (FolderModel folder in folders)
            {
                ImmutableArray<RouteModelHeader> routes = await folder.GetRoutes(cancellationToken).ConfigureAwait(false);
                foreach (RouteModelHeader route in routes)
                {
                    ImmutableArray<ActivityModelHeader> activities = await route.GetRouteActivities(cancellationToken).ConfigureAwait(false);

                    foreach (ActivityModelHeader activity in activities)
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
