using System;
using System.Collections.Concurrent;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler.TrainSimulator;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Imported.Shim
{
    public static class FileResolver
    {
        private static readonly ConcurrentDictionary<string, ContentFolderResolver> folderResolvers = new ConcurrentDictionary<string, ContentFolderResolver>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, ContentRouteResolver> routeResolvers = new ConcurrentDictionary<string, ContentRouteResolver>(StringComparer.OrdinalIgnoreCase);

        public static ContentFolderResolver ContentFolderResolver(FolderModel contentFolder)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));
            if (!folderResolvers.TryGetValue(contentFolder.Name, out ContentFolderResolver resolver))
            {
                resolver = new ContentFolderResolver(contentFolder);
                _ = folderResolvers.TryAdd(contentFolder.Name, resolver);
            }
            return resolver;
        }

        public static ContentRouteResolver ContentRouteResolver(RouteModelCore routeModel)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy();

            if (!routeResolvers.TryGetValue(key, out ContentRouteResolver resolver))
            {
                resolver = new ContentRouteResolver(routeModel);
                _ = routeResolvers.TryAdd(key, resolver);
            }
            return resolver;
        }
    }

    public sealed class ContentFolderResolver
    {
        public FolderModel ContentFolder { get; }

        public FolderStructure.ContentFolder MstsContentFolder { get; }

        public ContentFolderResolver(FolderModel contentFolderModel)
        {
            ContentFolder = contentFolderModel;
            MstsContentFolder = FolderStructure.Content(contentFolderModel?.ContentPath);
        }
    }

    public sealed class ContentRouteResolver
    {
        public FolderModel ContentFolder { get; }
        public RouteModelCore RouteModel { get; }

        public FolderStructure.ContentFolder.RouteFolder MstsRouteFolder { get; }

        public ContentRouteResolver(RouteModelCore routeModel)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            RouteModel = routeModel;
            MstsRouteFolder = routeModel.Parent.MstsContentFolder().Route(routeModel.Tags[RouteModelImportHandler.SourceNameKey]);
        }
    }
}
