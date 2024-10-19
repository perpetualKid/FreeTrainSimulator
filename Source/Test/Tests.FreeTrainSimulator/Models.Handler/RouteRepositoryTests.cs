using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;

using FreeTrainSimulator.Models.Loader.Handler;
using FreeTrainSimulator.Models.Loader.Shim;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models.Handler
{
    [TestClass]
    public class RouteRepositoryTests
    {
        [TestMethod]
        public async ValueTask LoadRoutes()
        {
            FrozenSet<ProfileModel> profiles = await ProfileModelHandler.GetProfiles(CancellationToken.None).ConfigureAwait(false);
            ProfileModel profile = profiles.GetByName("Another Profile");
            //ProfileModel profile = await ProfileModelHandler.Setup("Another Profile", null, CancellationToken.None).ConfigureAwait(false);
            profile = await ProfileModelHandler.Setup("Another Profile", new List<(string, string)>() 
            { 
                ("Demo A", "C:\\Storage\\OR\\Demo Model 1")
            }, CancellationToken.None).ConfigureAwait(false);
            return;
            ProfileModel defaultModel = await ProfileModel.None.Get(CancellationToken.None);
            if (null != defaultModel)
            {
                FrozenSet<FolderModel> folders = await FolderModelHandler.GetFolders(defaultModel, CancellationToken.None).ConfigureAwait(false);
                FolderModel folderModel = folders?.Where(f => f.Name == "Demo Model 1").FirstOrDefault();
                if (folderModel != null)
                {
                    folderModel = await FolderModelHandler.GetCore(folderModel, CancellationToken.None).ConfigureAwait(false);
                    FrozenSet<RouteModelCore> routes = await RouteModelHandler.GetRoutes(folderModel, CancellationToken.None).ConfigureAwait(false);

                    RouteModelCore routeModel = routes.FirstOrDefault();

                    if (null != routeModel)
                    {
                        routeModel = await RouteModelHandler.GetCore(routeModel, CancellationToken.None).ConfigureAwait(false);
                        routeModel = await RouteModelHandler.GetCore(routeModel, CancellationToken.None).ConfigureAwait(false);

                        //FrozenSet<PathModelCore> paths = await PathModelHandler.ExpandPathModels(routeModel, CancellationToken.None).ConfigureAwait(false);

                        FrozenSet<PathModelCore> paths = await routeModel.GetRoutePaths(CancellationToken.None).ConfigureAwait(false);
                        PathModelCore pathModel = paths.FirstOrDefault();

                        if (null != pathModel)
                        {
                            pathModel = await PathModelHandler.GetExtended(pathModel.Id, pathModel.Parent, CancellationToken.None);
                        }
                    }

                    //routes = await RouteModelCoreHandler.GetRoutes(folderModel, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
    }
}
