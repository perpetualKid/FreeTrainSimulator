using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;

namespace FreeTrainSimulator.Models.Shim
{
    public static class PathModelExtensions
    {
        public static Task<PathModelHeader> Get(this RouteModelHeader routeModel, string pathId, CancellationToken cancellationToken) => PathModelHandler.GetCore(pathId, routeModel, cancellationToken);
        public static ValueTask<PathModel> GetExtended(this RouteModelHeader routeModel, string pathId, CancellationToken cancellationToken) => PathModelHandler.GetExtended(pathId, routeModel, cancellationToken);
        public static ValueTask<PathModel> GetExtended(this PathModelHeader pathModel, CancellationToken cancellationToken) => PathModelHandler.GetExtended(pathModel, cancellationToken);
        public static Task<ImmutableArray<PathModelHeader>> GetRoutePaths(this RouteModelHeader routeModel, CancellationToken cancellationToken) => PathModelHandler.GetPaths(routeModel, cancellationToken);
    }
}
