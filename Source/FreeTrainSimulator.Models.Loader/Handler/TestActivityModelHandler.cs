using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class TestActivityModelHandler : ContentHandlerBase<ActivityModelCore>
    {
        public static async ValueTask<FrozenSet<TestActivityModel>> GetTestActivities(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            FrozenSet<FolderModel> folders = profileModel.ContentFolders;
            ConcurrentBag<TestActivityModel> result = new ConcurrentBag<TestActivityModel>();

            foreach(FolderModel folder in folders)
            {
                FrozenSet<RouteModelCore> routes = await folder.Routes(cancellationToken).ConfigureAwait(false);
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
