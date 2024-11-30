using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class TestActivityModelHandler : ContentHandlerBase<ActivityModelCore>
    {
        public static Task<FrozenSet<ActivityModelCore>> GetTestActivities(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            string key = profileModel.Hierarchy();

            if (!modelSetTaskCache.TryGetValue(key, out Task<FrozenSet<ActivityModelCore>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadActivities(profileModel, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<FrozenSet<ActivityModelCore>> LoadActivities(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            FrozenSet<FolderModel> folders = profileModel.ContentFolders;
            ConcurrentBag<ActivityModelCore> result = new ConcurrentBag<ActivityModelCore>();

            foreach (FolderModel folder in folders)
            {
                FolderModel folderInstance = await folder.Get(cancellationToken).ConfigureAwait(false);
                FrozenSet<RouteModelCore> routes = await folderInstance.GetRoutes(cancellationToken).ConfigureAwait(false);
                foreach (RouteModelCore route in routes)
                {
                    FrozenSet<ActivityModelCore> activities = await route.GetRouteActivities(cancellationToken).ConfigureAwait(false);

                    foreach (ActivityModelCore activity in activities)
                    {
                        if (activity != ActivityModelHandler.ExploreActivityMode && activity != ActivityModelHandler.ExploreMode)
                            result.Add(new TestActivityModel(activity));
                    }
                }
            }
            return result.ToFrozenSet();
        }
    }
}
