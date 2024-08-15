using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;
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
        public static ProfileModel Default(this ProfileModel _) => ContentProfileHandler.DefaultProfile;

        public static async ValueTask<ProfileModel> Get(this ProfileModel profileModel, CancellationToken cancellationToken)
        {
            return await ContentProfileHandler.Get(profileModel?.Name, cancellationToken).ConfigureAwait(true);
        }

        public static async ValueTask<ProfileModel> Convert(this ProfileModel profileModel, IEnumerable<(string, string)> folders, CancellationToken cancellationToken)
        {
            return await ContentProfileHandler.Convert(profileModel?.Name, folders, cancellationToken).ConfigureAwait(true);
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

            return await ContentRouteHandler.Get(routeName, folderModel, cancellationToken).ConfigureAwait(true);
        }

        public static async ValueTask<FolderModel> Load(this FolderModel folderModel, CancellationToken cancellationToken)
        {
            return folderModel != null ? await ContentFolderHandler.Load(folderModel, cancellationToken).ConfigureAwait(false) : folderModel;
        }

        public static async ValueTask<FolderModel> Convert(this FolderModel folderModel, CancellationToken cancellationToken)
        {
            return folderModel != null ? await ContentFolderHandler.Convert(folderModel, cancellationToken).ConfigureAwait(false) : folderModel;
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
            return routeModel is RouteModel routeModelExtended ? routeModelExtended : await ContentRouteHandler.Get(routeModel, cancellationToken).ConfigureAwait(false);
        }
    }
}
