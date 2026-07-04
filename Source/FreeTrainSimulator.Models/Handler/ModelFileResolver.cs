using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Models.Base;

namespace FreeTrainSimulator.Models.Handler
{
    /// <summary>
    /// Shared cache of <see cref="ModelResolverAttribute"/> instances, keyed by model type.
    /// Avoids repeated reflection to resolve file-storage metadata.
    /// </summary>
    internal static class ModelResolverCache
    {
        public static Dictionary<Type, ModelResolverAttribute> ModelResolvers { get; } = new Dictionary<Type, ModelResolverAttribute>();
    }

    /// <summary>
    /// Holds the root directory under which all models are persisted. Defaults to the user-data
    /// root (<see cref="RuntimeInfo.UserDataFolder"/>) and can be redirected to isolate model
    /// persistence from the real content store (for example, so tests never touch user content).
    /// </summary>
    internal static class ModelStore
    {
        private static string contentRoot = Path.GetFullPath(RuntimeInfo.UserDataFolder);

        internal static string ContentRoot => contentRoot;

        internal static void RedirectContentRoot(string root)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root, nameof(root));
            contentRoot = Path.GetFullPath(root);
        }
    }

    /// <summary>
    /// Resolves file system paths for model persistence. Combines the model's
    /// <see cref="ModelResolverAttribute"/> metadata (folder and extension) with the parent
    /// hierarchy to produce deterministic file and folder paths under the user-data root.
    /// </summary>
    /// <typeparam name="TModel">The model type whose storage paths are resolved.</typeparam>
    public static class ModelFileResolver<TModel> where TModel : ModelBase
    {
        private static string contentRoot => ModelStore.ContentRoot;

        private static class ModelTypeCache
        {
            private static readonly ModelResolverAttribute resolverAttribute = ResolveAttribute();
            private static ModelResolverAttribute ResolveAttribute()
            {
                Type modelType = typeof(TModel);
                return ModelResolverCache.ModelResolvers[modelType] = modelType.GetCustomAttributes(typeof(ModelResolverAttribute), false).Cast<ModelResolverAttribute>().FirstOrDefault() ?? new ModelResolverAttribute(string.Empty, $".{modelType.Name}.invalid");
            }

            internal static string FileExtension => resolverAttribute?.FileExtension;
            internal static string SubFolder => resolverAttribute?.Folder ?? string.Empty;

            public static string ParentFolder(ModelBase instance)
            {
                Type modelType = instance?.GetType();
                if (modelType != null)
                { 
                    if (!ModelResolverCache.ModelResolvers.TryGetValue(modelType, out ModelResolverAttribute modelResolver))
                    {
                        modelResolver = ModelResolverCache.ModelResolvers[modelType] = modelType.GetCustomAttributes(typeof(ModelResolverAttribute), false).Cast<ModelResolverAttribute>().FirstOrDefault() ?? ModelResolverAttribute.Empty;
                    }
                    return modelResolver.Folder;
                }
                return string.Empty;
            }
        }

        private static string FolderPathInternal(ModelBase parent)
        {
            return parent != null ? Path.Combine(FolderPathInternal(parent.Parent) ?? Path.Combine(contentRoot, ModelTypeCache.ParentFolder(parent)), FileName(parent)) : null;
        }

#pragma warning disable CA1000 // Do not declare static members on generic types
        public static string FileExtension => ModelTypeCache.FileExtension;
        public static string SubFolder => ModelTypeCache.SubFolder;
        public static string FileName(ModelBase instance) => instance?.Id;

        public static string FilePath<TOther>(string modelId, TOther parent) where TOther : ModelBase
        {
            return Path.GetFullPath(Path.Combine(FolderPathInternal(parent) ?? contentRoot, SubFolder, modelId + FileExtension));
        }

        public static string FilePath(TModel model)
        {
            return Path.GetFullPath(Path.Combine(FolderPathInternal(model?.Parent) ?? contentRoot, SubFolder, FileName(model) + FileExtension));
        }

        public static string FolderPath(ModelBase parent)
        {
            return Path.GetFullPath(Path.Combine(FolderPathInternal(parent) ?? contentRoot, SubFolder));
        }

        public static string WildcardPattern => $"*{FileExtension}.*";
        public static string WildcardSavePattern => $"*{FileExtension}{ContentHandlerBase<TModel>.SaveStateExtension}";

#pragma warning restore CA1000 // Do not declare static members on generic types
    }
}
