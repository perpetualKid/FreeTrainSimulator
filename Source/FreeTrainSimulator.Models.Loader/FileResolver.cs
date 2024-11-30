using System;
using System.Collections.Concurrent;
using System.IO;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Base;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Loader.Handler;
using FreeTrainSimulator.Models.Loader.Shim;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Loader
{
    public static class FileResolver
    {
        private const string RootPath = "Content";
        public static string ContentRoot { get; } = Path.GetFullPath(Path.Combine(RuntimeInfo.UserDataFolder, RootPath));

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
            MstsRouteFolder = routeModel.Parent.MstsContentFolder().Route(routeModel.Tags[RouteModelHandler.SourceNameKey]);
        }
    }


    public static class ModelFileResolver<T> where T : ModelBase<T>
    {
        static ModelFileResolver()
        {
            // ensure the static constructor has been executed
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
        }

#pragma warning disable CA1000 // Do not declare static members on generic types
        private static string FileExtensionCore<U>() where U : IFileResolve => U.DefaultExtension;
        private static string SubFolderCore<U>() where U : IFileResolve => U.SubFolder;
        private static string DirectoryNameCore<U>(U instance) where U : IFileResolve => instance?.DirectoryName;
        private static string FileNameCore<U>(U instance) where U : IFileResolve => instance?.FileName;
        public static string FileExtension => FileExtensionCore<ModelBase<T>>();
        public static string SubFolder => SubFolderCore<ModelBase<T>>() ?? string.Empty;
        public static string DirectoryName<TContainer>(ModelBase<TContainer> instance) where TContainer : ModelBase<TContainer> => DirectoryNameCore(instance);
        public static string FileName<TContainer>(ModelBase<TContainer> instance) where TContainer : ModelBase<TContainer> => FileNameCore(instance);

        public static string FilePath<TContainer>(string name, ModelBase<TContainer> container) where TContainer : ModelBase<TContainer>
        {
            return Path.Combine(FolderPath(container), name + FileExtension);
        }

        public static string FilePath(T model, IFileResolve container = null)
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            return Path.GetFullPath(Path.Combine(FolderPath(model.Parent ?? container), SubFolder, FileName(model) + FileExtension));
        }

        public static string FolderPath<TContainer>(ModelBase<TContainer> container) where TContainer : ModelBase<TContainer>
        {
            return Path.Combine(FolderPath(container as IFileResolve), SubFolder);
        }

        private static string FolderPath(IFileResolve container)
        {
            return container != null ? Path.Combine(FolderPath(container.Container), container.DirectoryName) : FileResolver.ContentRoot;
        }

        public static string WildcardPattern => $"*{FileExtension}.*";
        public static string WildcardSavePattern => $"*{FileExtension}{ContentHandlerBase<T>.SaveStateExtension}";

#pragma warning restore CA1000 // Do not declare static members on generic types
    }
}
