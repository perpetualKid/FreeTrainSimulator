using System;
using System.Collections.Frozen;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Independent.Settings;
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
            return routeModel is RouteModel routeModelExtended ? routeModelExtended : await RouteModelHandler.Extend(routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<RouteModelCore> Convert(this RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            if (routeModel != null)
            {
                routeModel = await RouteModelHandler.Convert(routeModel.MstsRouteFolder(), (routeModel as IFileResolve).Container as FolderModel, cancellationToken).ConfigureAwait(false);
                FrozenSet<PathModelCore> pathModels = await PathModelHandler.ConvertPathModels(routeModel, cancellationToken).ConfigureAwait(false);
                FrozenSet<ActivityModelCore> activityModels = await ActivityModelHandler.ConvertActivityModels(routeModel, cancellationToken).ConfigureAwait(false);
                routeModel = routeModel with { TrainPaths = pathModels, RouteActivities = activityModels };
            }
            return routeModel;
        }

        public static async ValueTask Expand(this RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            routeModel.ResetChildModels(await PathModelHandler.ConvertPathModels(routeModel, cancellationToken).ConfigureAwait(false),
                await ActivityModelHandler.ConvertActivityModels(routeModel, cancellationToken).ConfigureAwait(false));
        }

        public static async ValueTask<RouteModel> ToRouteModel(this FolderStructure.ContentFolder.RouteFolder routeFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeFolder, nameof(routeFolder));

            string contentFolderPath = routeFolder.ContentFolder.Folder;

            ProfileModel contentProfile = await ProfileModel.Null.Get(cancellationToken).ConfigureAwait(false);
            FolderModel folder = await contentProfile.ContentFolders.
                Where((folder) => Path.GetRelativePath(folder.ContentPath, contentFolderPath) == ".").FirstOrDefault().
                Load(cancellationToken).ConfigureAwait(false);

            Debug.Assert(folder?.Routes != null);

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

        public static async ValueTask<RouteModelCore> Load(this RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            return routeModel != null && routeModel.SetupRequired() ? await RouteModelCoreHandler.Load(routeModel, cancellationToken).ConfigureAwait(false) : routeModel;
        }

        public static async ValueTask<PathModel> PathModel(this RouteModelCore routeModel, string pathName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentException.ThrowIfNullOrEmpty(pathName, nameof(pathName));

            return await PathModelHandler.Get(pathName, routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<ActivityModel> ActivityModel(this RouteModelCore routeModel, string activityName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            ArgumentException.ThrowIfNullOrEmpty(activityName, nameof(activityName));

            return await ActivityModelHandler.Get(activityName, routeModel, cancellationToken).ConfigureAwait(false);
        }
    }
}
