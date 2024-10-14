using System.Collections.Frozen;
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
            ProfileModel defaultModel = await ProfileModel.None.Get(CancellationToken.None);
            if (null != defaultModel)
            {
                 FolderModel folderModel = await FolderModelHandler.Get("Demo Model 1", defaultModel, CancellationToken.None).ConfigureAwait(false);
                if (folderModel != null)
                {
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
