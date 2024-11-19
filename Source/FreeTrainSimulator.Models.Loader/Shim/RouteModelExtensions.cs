using System;
using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Handler;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class RouteModelExtensions
    {
        public static FolderStructure.ContentFolder.RouteFolder MstsRouteFolder(this RouteModelCore routeModel) => FileResolver.ContentRouteResolver(routeModel).MstsRouteFolder;

        public static ValueTask<RouteModel> Extend(this RouteModelCore routeModel, CancellationToken cancellationToken) => RouteModelHandler.GetExtended(routeModel, cancellationToken);
        public static ValueTask<FrozenSet<PathModelCore>> GetPaths(this RouteModelCore routeModel, CancellationToken cancellationToken) => routeModel.GetRoutePaths(cancellationToken);
        public static ValueTask<FrozenSet<ActivityModelCore>> GetActivities(this RouteModelCore routeModel, CancellationToken cancellationToken) => routeModel.GetRouteActivities(cancellationToken);

        public static async ValueTask<RouteModel> ToRouteModel(this FolderStructure.ContentFolder.RouteFolder routeFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeFolder, nameof(routeFolder));

            string contentFolderPath = routeFolder.ContentFolder.Folder;

            ProfileModel contentProfile = await ProfileModel.None.Get(cancellationToken).ConfigureAwait(false);
            FolderModel folder = await contentProfile.ContentFolders.
                Where((folder) => Path.GetRelativePath(folder.ContentPath, contentFolderPath) == ".").FirstOrDefault().
                Get(cancellationToken).ConfigureAwait(false);

            RouteModelCore routeModelCore = (await folder.GetRoutes(cancellationToken).ConfigureAwait(false)).Where(r => r.MstsRouteFolder() == routeFolder).FirstOrDefault() ??
                throw new FileNotFoundException($"Route not found. Abnormal termination.");

            return await RouteModelHandler.GetExtended(routeModelCore, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<PathModelCore> PathModel(this RouteModelCore routeModel, string pathName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentException.ThrowIfNullOrEmpty(pathName, nameof(pathName));

            return await PathModelHandler.GetCore(pathName, routeModel, cancellationToken).ConfigureAwait(false);
        }
    }
}
