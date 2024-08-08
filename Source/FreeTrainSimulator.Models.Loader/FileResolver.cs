using System;
using System.Collections.Concurrent;
using System.IO;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Independent;
using FreeTrainSimulator.Models.Independent.Content;

using Orts.Formats.Msts;

using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace FreeTrainSimulator.Models.Loader
{
    public static class FileResolver
    {
        private const string RootPath = "Content";
        public static string ContentRoot { get; } = Path.GetFullPath(Path.Combine(RuntimeInfo.UserDataFolder, RootPath));

        private static readonly ConcurrentDictionary<string, ContentFolderResolver> contentResolvers = new ConcurrentDictionary<string, ContentFolderResolver>(StringComparer.OrdinalIgnoreCase);

        public static string ContentProfileFile(string contentProfile) => Path.Combine(ContentRoot, $"{contentProfile}{ModelFileResolver<ContentProfileModel>.FileExtension}");
        public static string ContentFolderFile(string contentProfile, string contentFolder) => Path.Combine(ContentProfileDirectory(contentProfile), $"{contentFolder}{ModelFileResolver<ContentProfileModel>.FileExtension}");

        public static string ContentProfileDirectory(string contentProfile) => Path.Combine(RuntimeInfo.UserDataFolder, RootPath, contentProfile);

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
            MstsContentFolder = FolderStructure.Content(contentFolderModel.ContentPath);
            //string test = ModelFileResolver<ContentFolderModel>.Folder(contentFolderModel);
        }
    }

    public class ModelFileResolver<T> where T : ModelBase<T>
    {
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

        public static string FilePath(T model)
        {
            string path = string.Empty;
            IFileResolve parent = model.Parent;
            while (parent != null)
            {
                path = Path.Combine((parent).FolderName, path);
                parent = parent.Parent;
            }
            return Path.GetFullPath(Path.Combine(FileResolver.ContentRoot, path, FileName(model) + FileExtension));
        }

        public static string FolderPath<TParent>(ModelBase<TParent> parent) where TParent : ModelBase<TParent>
        {
            string path = string.Empty;
            while (parent != null)
            {
                path = Path.Combine((parent as IFileResolve).FolderName, path);
                parent = parent.Parent as TParent;
            }
            return Path.GetFullPath(Path.Combine(FileResolver.ContentRoot, path));
        }
    }
}
