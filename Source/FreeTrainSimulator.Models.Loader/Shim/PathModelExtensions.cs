using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class PathModelExtensions
    {
        public static ValueTask<PathModelCore> Get(this RouteModelCore routeModel, string pathId, CancellationToken cancellationToken) => PathModelHandler.GetCore(pathId, routeModel, cancellationToken);
        public static ValueTask<PathModel> GetExtended(this RouteModelCore routeModel, string pathId, CancellationToken cancellationToken) => PathModelHandler.GetExtended(pathId, routeModel, cancellationToken);
        public static ValueTask<PathModel> GetExtended(this PathModelCore pathModel, CancellationToken cancellationToken) => PathModelHandler.GetExtended(pathModel, cancellationToken);
        public static ValueTask<FrozenSet<PathModelCore>> GetRoutePaths(this RouteModelCore routeModel, CancellationToken cancellationToken) => PathModelHandler.GetPaths(routeModel, cancellationToken);
    }
}
