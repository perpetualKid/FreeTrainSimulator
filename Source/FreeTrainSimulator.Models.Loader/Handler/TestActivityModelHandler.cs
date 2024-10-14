using System;
using System.Collections.Frozen;
using System.Linq;
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

            if (null != profileModel)
            {
                FolderModel folderModel = await FolderModelHandler.Get("Demo Model 1", profileModel, CancellationToken.None).ConfigureAwait(false);
                if (folderModel != null)
                {
                    FrozenSet<RouteModelCore> routes = await RouteModelCoreHandler.GetRoutes(folderModel, CancellationToken.None).ConfigureAwait(false);

                    RouteModelCore routeModel = routes.FirstOrDefault();
                    if (null != routeModel)
                    {
                        routeModel = await RouteModelCoreHandler.GetCore(routeModel, CancellationToken.None).ConfigureAwait(false);
                        routeModel = await RouteModelCoreHandler.GetCore(routeModel, CancellationToken.None).ConfigureAwait(false);
                    }

                    routes = await RouteModelCoreHandler.GetRoutes(folderModel, CancellationToken.None).ConfigureAwait(false);
                }
            }
            return FrozenSet<TestActivityModel>.Empty;
        }
    }
}
