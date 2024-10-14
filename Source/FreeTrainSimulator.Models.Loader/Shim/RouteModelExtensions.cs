using System;
using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Handler;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class RouteModelExtensions
    {
        public static FolderStructure.ContentFolder.RouteFolder MstsRouteFolder(this RouteModelCore routeModel)
        {
            ContentRouteResolver resolver = FileResolver.ContentRouteResolver(routeModel);
            return resolver.MstsRouteFolder;
        }

        public static async ValueTask<RouteModel> Extend(this RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            return routeModel is RouteModel routeModelExtended ? routeModelExtended : await RouteModelHandler.Get(routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<RouteModelCore> Convert(this RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            if (routeModel != null)
            {
                routeModel = await RouteModelHandler.Convert(routeModel.MstsRouteFolder(), (routeModel as IFileResolve).Container as FolderModel, cancellationToken).ConfigureAwait(false);
            }
            return routeModel;
        }

        public static async ValueTask Expand(this RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            //routeModel.ResetChildModels(await PathModelHandler.ConvertPathModels(routeModel, cancellationToken).ConfigureAwait(false),
            //    await ActivityModelHandler.ConvertActivityModels(routeModel, cancellationToken).ConfigureAwait(false));
        }

        public static async ValueTask<RouteModel> ToRouteModel(this FolderStructure.ContentFolder.RouteFolder routeFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeFolder, nameof(routeFolder));

            string contentFolderPath = routeFolder.ContentFolder.Folder;

            ProfileModel contentProfile = await ProfileModel.Null.Get(cancellationToken).ConfigureAwait(false);
            FolderModel folder = await contentProfile.ContentFolders.
                Where((folder) => Path.GetRelativePath(folder.ContentPath, contentFolderPath) == ".").FirstOrDefault().
                Load(cancellationToken).ConfigureAwait(false);

            RouteModelCore routeModelCore = folder.Routes.Where(r => r.MstsRouteFolder() == routeFolder).FirstOrDefault() ??
                throw new FileNotFoundException($"Route not found. Abnormal termination.");

            if (routeModelCore is RouteModel fullRouteModel && !fullRouteModel.SetupRequired())
            {
                return fullRouteModel;
            }

            RouteModel routeModel = await routeModelCore.Extend(cancellationToken).ConfigureAwait(false);
            if (routeModel.SetupRequired())
                routeModel = await routeModel.Convert(cancellationToken).ConfigureAwait(false) as RouteModel;

            folder.SetRoutes(folder.Routes.Where((r) => r != routeModelCore).Append(routeModel)); //Replacing the existing route model in the parent folder, with this new instance

            return routeModel;
        }

        public static async ValueTask<FrozenSet<PathModelCore>> Paths(this RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            return await PathModelHandler.GetPaths(routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<FrozenSet<ActivityModelCore>> Activities(this RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            return await ActivityModelCoreHandler.GetActivities(routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<PathModelCore> PathModel(this RouteModelCore routeModel, string pathName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentException.ThrowIfNullOrEmpty(pathName, nameof(pathName));

            return await PathModelHandler.Get(pathName, routeModel, cancellationToken).ConfigureAwait(false);
        }
    }
}
