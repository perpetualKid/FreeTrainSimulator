using System;
using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class TestActivityModelHandler : ContentHandlerBase<TestActivityModel, ActivityModelCore>
    {
        public static async ValueTask<FrozenSet<TestActivityModel>> GetTestActivities(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            if (profileModel.SetupRequired())
            {
                throw new InvalidOperationException("Profile Folders not initialized. Abnormal termination.");
            }

            foreach (FolderModel folderModel in profileModel.ContentFolders)
            {

                _ = await folderModel.Load(CancellationToken.None).ConfigureAwait(false);
                foreach (RouteModelCore routeModel in folderModel.Routes)
                {
                    _ = routeModel.Load(CancellationToken.None).ConfigureAwait(false);
                    foreach (ActivityModelCore activityModel in routeModel.RouteActivities)
                        Console.WriteLine(activityModel.Name);
                }
            }

            return FrozenSet<TestActivityModel>.Empty;
        }
    }
}
