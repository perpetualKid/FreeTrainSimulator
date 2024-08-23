using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Independent.Settings;
using FreeTrainSimulator.Models.Loader.Handler;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public static class ContentModelExtensions
    {
        public static bool SetupRequired<T>(this ModelBase<T> model) where T : ModelBase<T> => model == null || model.RefreshRequired;
    }

    public static class ProfileModelExtensions
    {
        public static ProfileModel Default(this ProfileModel _) => ProfileModelHandler.DefaultProfile;

        public static async ValueTask<ProfileModel> Get(this ProfileModel profileModel, CancellationToken cancellationToken)
        {
            return await ProfileModelHandler.Get(profileModel?.Name, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<FolderModel> FolderModel(this ProfileModel profileModel, string folderName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            ArgumentException.ThrowIfNullOrEmpty(folderName, nameof(folderName));

            return await FolderModelHandler.Get(folderName, profileModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<ProfileSelectionsModel> SelectionsModel(this ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            ProfileSelectionsModel selectionsModel = await ContentHandlerBase<ProfileSelectionsModel, ProfileSelectionsModel>.FromFile(profileModel.Name, profileModel, cancellationToken).ConfigureAwait(false);
            if (selectionsModel == null)
            {
                selectionsModel = new ProfileSelectionsModel() { Name = profileModel.Name };
                selectionsModel.Initialize(ModelFileResolver<ProfileSelectionsModel>.FilePath(selectionsModel, profileModel), profileModel);
            }
            return selectionsModel;
        }

        public static async ValueTask<ProfileSelectionsModel> UpdateSelectionsModel(this ProfileModel profileModel, ProfileSelectionsModel selectionsModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));
            ArgumentNullException.ThrowIfNull(selectionsModel, nameof(selectionsModel));

            return await ContentHandlerBase<ProfileSelectionsModel, ProfileSelectionsModel>.ToFile(selectionsModel, cancellationToken).ConfigureAwait(false);
        }


        public static async ValueTask<ProfileModel> Convert(this ProfileModel profileModel, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            return await ProfileModelHandler.Convert(profileModel?.Name, folders, cancellationToken).ConfigureAwait(false);
        }
    }

    public static class FolderModelExtensions
    {
        public static FolderStructure.ContentFolder MstsContentFolder(this FolderModel folderModel)
        {
            ContentFolderResolver resolver = FileResolver.ContentFolderResolver(folderModel);
            return resolver.MstsContentFolder;
        }

        public static async ValueTask<RouteModel> RouteModel(this FolderModel folderModel, string routeName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderModel, nameof(folderModel));
            ArgumentException.ThrowIfNullOrEmpty(routeName, nameof(routeName));

            return await RouteModelHandler.Get(routeName, folderModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<FolderModel> Load(this FolderModel folderModel, CancellationToken cancellationToken)
        {
            return folderModel != null && folderModel.SetupRequired() ? await FolderModelHandler.Load(folderModel, cancellationToken).ConfigureAwait(false) : folderModel;
        }

        public static async ValueTask<FolderModel> Convert(this FolderModel folderModel, CancellationToken cancellationToken)
        {
            return folderModel != null ? await FolderModelHandler.Convert(folderModel, cancellationToken).ConfigureAwait(false) : folderModel;
        }
    }

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
                routeModel = await RouteModelCoreHandler.ConvertPathModels(routeModel, cancellationToken).ConfigureAwait(false);
            }
            return routeModel;
        }

        public static async ValueTask<RouteModelCore> Expand(this RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            if (routeModel.SetupRequired())
                routeModel = await RouteModelHandler.Convert(routeModel.MstsRouteFolder(), (routeModel as IFileResolve).Container as FolderModel, cancellationToken).ConfigureAwait(false);
            routeModel = await RouteModelCoreHandler.ConvertPathModels(routeModel, cancellationToken).ConfigureAwait(false);
            return routeModel;

        }

        public static async ValueTask<RouteModel> ToRouteModel(this FolderStructure.ContentFolder.RouteFolder routeFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeFolder, nameof(routeFolder));

            string contentFolderPath = routeFolder.ContentFolder.Folder;

            ProfileModel contentProfile = await ProfileModelHandler.DefaultProfile.Get(cancellationToken).ConfigureAwait(false);
            FolderModel folder = await contentProfile.ContentFolders.
                Where((folder) => Path.GetRelativePath(folder.ContentPath, contentFolderPath) == ".").FirstOrDefault().
                Load(cancellationToken).ConfigureAwait(false);

            Debug.Assert(folder?.Routes != null);

            RouteModelCore routeModelCore = folder.Routes.Where(r => r.MstsRouteFolder() == routeFolder).FirstOrDefault() ??
                throw new FileNotFoundException($"Route not found. Abnormal termination");

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
    }

    public static class PathModelExtensions
    {
        public static async ValueTask<PathModel> Convert(this PathModel pathModel, CancellationToken cancellationToken)
        {
            return pathModel != null ? await PathModelHandler.Convert(pathModel.Name, (pathModel as IFileResolve).Container as RouteModel, cancellationToken).ConfigureAwait(false) : pathModel;
        }
    }
}
