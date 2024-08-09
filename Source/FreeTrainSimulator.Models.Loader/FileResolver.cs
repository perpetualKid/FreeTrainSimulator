using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Independent;
using FreeTrainSimulator.Models.Independent.Content;

using Orts.Formats.Msts;

namespace FreeTrainSimulator.Models.Loader
{
    public static class FileResolver
    {
        private const string RootPath = "Content";
        public static string ContentRoot { get; } = Path.GetFullPath(Path.Combine(RuntimeInfo.UserDataFolder, RootPath));

        private static readonly ConcurrentDictionary<string, ContentFolderResolver> contentResolvers = new ConcurrentDictionary<string, ContentFolderResolver>(StringComparer.OrdinalIgnoreCase);

        public static ContentFolderResolver ContentFolderResolver(ContentFolderModel contentFolder)
        {
            ArgumentNullException.ThrowIfNull(contentFolder, nameof(contentFolder));
            if (!contentResolvers.TryGetValue(contentFolder.Name, out ContentFolderResolver resolver))
            {
                resolver = new ContentFolderResolver(contentFolder);
                _ = contentResolvers.TryAdd(contentFolder.Name, resolver);
            }
            return resolver;
        }
    }

    public sealed class ContentFolderResolver
    {
        public ContentFolderModel ContentFolder { get; }

        public FolderStructure.ContentFolder MstsContentFolder { get; }

        public ContentFolderResolver(ContentFolderModel contentFolderModel)
        {
            ContentFolder = contentFolderModel;
            MstsContentFolder = FolderStructure.Content(contentFolderModel?.ContentPath);
        }
    }

    public static class ModelFileResolver<T> where T : ModelBase<T>
    {
        static ModelFileResolver()
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
        }

#pragma warning disable CA1000 // Do not declare static members on generic types
        private static string FileExtensionCore<U>() where U : IFileResolve => U.DefaultExtension;
        private static string FolderNameCore<U>(U instance) where U : IFileResolve => instance.FolderName;
        private static string FileNameCore<U>(U instance) where U : IFileResolve => instance.FileName;
        public static string FileExtension => FileExtensionCore<ModelBase<T>>();
        public static string FolderName<TParent>(ModelBase<TParent> instance) where TParent : ModelBase<TParent> => FolderNameCore(instance);
        public static string FileName<TParent>(ModelBase<TParent> instance) where TParent : ModelBase<TParent> => FileNameCore(instance);
        
        public static string FilePath<TParent>(string name, ModelBase<TParent> parent) where TParent : ModelBase<TParent>
        {
            return Path.Combine(FolderPath(parent), name + FileExtension);
        }

        public static string FilePath(T model, IFileResolve parent = null)
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            return Path.GetFullPath(Path.Combine(FolderPath(model.Parent ?? parent), FileName(model) + FileExtension));
        }

        public static string FolderPath<TParent>(ModelBase<TParent> parent) where TParent : ModelBase<TParent>
        {
            return FolderPath(parent as IFileResolve);
        }

        public static string FolderPath(IFileResolve parent)
        {
            return parent != null ? Path.Combine(FolderPath(parent.Parent), parent.FolderName) : FileResolver.ContentRoot;
        }

#pragma warning restore CA1000 // Do not declare static members on generic types
    }
}
