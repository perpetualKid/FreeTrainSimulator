using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Environment;
using FreeTrainSimulator.Models.Loader.Shim;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Models.Loader
{
    [TestClass]
    public class RouteLoaderTests
    {

        [Ignore]
        [TestMethod]
        public async Task LoadRouteTest()
        {
            string routeFolder = "C:\\Storage\\OR\\Demo Model 1\\ROUTES\\Monogame";
            RouteModel route = await RouteLoader.LoadRoute(routeFolder, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
