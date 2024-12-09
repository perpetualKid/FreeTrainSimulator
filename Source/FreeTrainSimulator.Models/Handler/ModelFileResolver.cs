using System;
using System.IO;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Base;

namespace FreeTrainSimulator.Models.Handler
{
    public static class ModelFileResolver<TModel> where TModel : ModelBase, IFileResolve
    {
        private const string RootPath = "Content";
        private static readonly string contentRoot = Path.GetFullPath(Path.Combine(RuntimeInfo.UserDataFolder, RootPath));
        
        static ModelFileResolver()
        {
            // ensure the static constructor has been executed
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TModel).TypeHandle);
        }

#pragma warning disable CA1000 // Do not declare static members on generic types
        public static string FileExtension => TModel.DefaultExtension;
        public static string SubFolder => TModel.SubFolder ?? string.Empty;
        public static string FileName(ModelBase instance) => instance?.Id;

        public static string FilePath<TOther>(string modelId, ModelBase parent) where TOther : ModelBase
        {
            return Path.GetFullPath(Path.Combine(FolderPath(parent), SubFolder, modelId + FileExtension));
        }

        public static string FilePath(TModel model)
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            return Path.GetFullPath(Path.Combine(FolderPath(model.Parent), SubFolder, FileName(model) + FileExtension));
        }

        public static string FolderPath<TOther>(ModelBase parent) where TOther : ModelBase
        {
            return Path.Combine(FolderPath(parent), SubFolder);
        }

        private static string FolderPath(ModelBase parent)
        {
            return parent != null ? Path.Combine(FolderPath(parent.Parent), parent.Id) : contentRoot;
        }

        public static string WildcardPattern => $"*{FileExtension}.*";
        public static string WildcardSavePattern => $"*{FileExtension}{ContentHandlerBase<TModel>.SaveStateExtension}";

#pragma warning restore CA1000 // Do not declare static members on generic types
    }
}
