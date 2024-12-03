using System;
using System.IO;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Base;

namespace FreeTrainSimulator.Models.Handler
{
    public static class ModelFileResolver<TModel> where TModel : ModelBase<TModel>
    {
        private const string RootPath = "Content";
        private static readonly string contentRoot = Path.GetFullPath(Path.Combine(RuntimeInfo.UserDataFolder, RootPath));
        
        static ModelFileResolver()
        {
            // ensure the static constructor has been executed
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TModel).TypeHandle);
        }

#pragma warning disable CA1000 // Do not declare static members on generic types
        private static string FileExtensionCore<TOther>() where TOther : IFileResolve => TOther.DefaultExtension;
        private static string SubFolderCore<TOther>() where TOther : IFileResolve => TOther.SubFolder;
        private static string DirectoryNameCore<TOther>(TOther instance) where TOther : IFileResolve => instance?.DirectoryName;
        private static string FileNameCore<TOther>(TOther instance) where TOther : IFileResolve => instance?.FileName;
        public static string FileExtension => FileExtensionCore<ModelBase<TModel>>();
        public static string SubFolder => SubFolderCore<ModelBase<TModel>>() ?? string.Empty;
        public static string DirectoryName<TOther>(ModelBase<TOther> instance) where TOther : ModelBase<TOther> => DirectoryNameCore(instance);
        public static string FileName<TOther>(ModelBase<TOther> instance) where TOther : ModelBase<TOther> => FileNameCore(instance);

        public static string FilePath<TOther>(string name, ModelBase<TOther> parent) where TOther : ModelBase<TOther>
        {
            return Path.Combine(FolderPath(parent), name + FileExtension);
        }

        public static string FilePath(TModel model, IFileResolve parent = null)
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            return Path.GetFullPath(Path.Combine(FolderPath(model.Parent ?? parent), SubFolder, FileName(model) + FileExtension));
        }

        public static string FolderPath<TOther>(ModelBase<TOther> parent) where TOther : ModelBase<TOther>
        {
            return Path.Combine(FolderPath(parent as IFileResolve), SubFolder);
        }

        private static string FolderPath(IFileResolve TOther)
        {
            return TOther != null ? Path.Combine(FolderPath(TOther.Container), TOther.DirectoryName) : contentRoot;
        }

        public static string WildcardPattern => $"*{FileExtension}.*";
        public static string WildcardSavePattern => $"*{FileExtension}{ContentHandlerBase<TModel>.SaveStateExtension}";

#pragma warning restore CA1000 // Do not declare static members on generic types
    }
}
