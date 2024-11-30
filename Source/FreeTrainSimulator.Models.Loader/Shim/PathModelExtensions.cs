using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class PathModelExtensions
    {
        public static Task<PathModelCore> Get(this RouteModelCore routeModel, string pathId, CancellationToken cancellationToken) => PathModelHandler.GetCore(pathId, routeModel, cancellationToken);
        public static ValueTask<PathModel> GetExtended(this RouteModelCore routeModel, string pathId, CancellationToken cancellationToken) => PathModelHandler.GetExtended(pathId, routeModel, cancellationToken);
        public static ValueTask<PathModel> GetExtended(this PathModelCore pathModel, CancellationToken cancellationToken) => PathModelHandler.GetExtended(pathModel, cancellationToken);
        public static Task<FrozenSet<PathModelCore>> GetRoutePaths(this RouteModelCore routeModel, CancellationToken cancellationToken) => PathModelHandler.GetPaths(routeModel, cancellationToken);
        public static string SourceFile(this PathModelCore pathModel) => pathModel?.Parent.MstsRouteFolder().PathFile(pathModel.Tags[PathModelHandler.SourceNameKey]);
    }
}
