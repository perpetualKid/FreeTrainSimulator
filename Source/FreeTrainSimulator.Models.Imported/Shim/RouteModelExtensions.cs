using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Imported.Shim
{
    public static class RouteModelExtensions
    {
        public static FolderStructure.ContentFolder.RouteFolder MstsRouteFolder(this RouteModelCore routeModel) => FileResolver.ContentRouteResolver(routeModel).MstsRouteFolder;

        public static Task<ImmutableArray<SavePointModel>> GetSavePoints(this RouteModelCore routeModel, string activityPrefix, CancellationToken cancellationToken) => SavePointModelHandler.GetSavePoints(routeModel, activityPrefix, cancellationToken);
        public static Task<ImmutableArray<SavePointModel>> RefreshSavePoints(this RouteModelCore routeModel, string activityPrefix, CancellationToken cancellationToken) => SavePointModelHandler.ExpandSavePointModels(routeModel, activityPrefix, cancellationToken);

        public static string SourceFile(this RouteModelCore routeModel) => Path.Combine(routeModel?.MstsRouteFolder().CurrentFolder, routeModel.MstsRouteFolder().TrackFileName);
        public static string SourceFolder(this RouteModelCore routeModel) => routeModel?.MstsRouteFolder().CurrentFolder;

        public static async ValueTask<RouteModel> ToRouteModel(this FolderStructure.ContentFolder.RouteFolder routeFolder, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeFolder, nameof(routeFolder));

            string contentFolderPath = routeFolder.ContentFolder.Folder;

            ContentModel contentModel = await ((ContentModel)null).Get(cancellationToken).ConfigureAwait(false);
            FolderModel folder = contentModel.ContentFolders.
                Where((folder) => Path.GetRelativePath(folder.ContentPath, contentFolderPath) == ".").FirstOrDefault();

            RouteModelCore routeModelCore = (await folder.GetRoutes(cancellationToken).ConfigureAwait(false)).Where(r => r.MstsRouteFolder() == routeFolder).FirstOrDefault() ??
                throw new FileNotFoundException($"Route not found. Abnormal termination.");

            return await routeModelCore.Extend(cancellationToken).ConfigureAwait(false);
        }
    }
}
