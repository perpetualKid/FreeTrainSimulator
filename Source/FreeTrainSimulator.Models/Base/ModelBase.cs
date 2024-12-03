using System.Collections.Generic;
using System.IO;
using System.Text;

using FreeTrainSimulator.Common.Info;

using MemoryPack;

namespace FreeTrainSimulator.Models.Base
{

    /// <summary>
    /// Abstract base class for all content models
    /// Implements basic file handling through <seealso cref="IFileResolve"/> interface
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract record ModelBase<T> : IFileResolve where T : ModelBase<T>
    {
        private const char HierarchySeparator = '/';
        private string _hierarchy;
        private string _version;
        private IFileResolve _parent;
        private string _directoryPath;

        #region internal handling
#pragma warning disable CA2211 // Non-constant fields should not be visible
        [MemoryPackIgnore]
        protected static string fileExtension;
        [MemoryPackIgnore]
        protected static string subFolder;
#pragma warning restore CA2211 // Non-constant fields should not be visible

        //does allow to override default target files
        protected virtual string DirectoryName => Id;
        protected virtual string FileName => Id;
        protected virtual string DirectoryPath => _directoryPath;

        #region IFileResolve implementation
        [MemoryPackIgnore]
#pragma warning disable CA1033 // Interface methods should be callable by child types
        static string IFileResolve.DefaultExtension => fileExtension;
        static string IFileResolve.SubFolder => subFolder;
        [MemoryPackIgnore]
        string IFileResolve.DirectoryName => DirectoryName;
        [MemoryPackIgnore]
        string IFileResolve.FileName => FileName;
        [MemoryPackIgnore]
        string IFileResolve.DirectoryPath => DirectoryPath;
        [MemoryPackIgnore]
        IFileResolve IFileResolve.Container => _parent;
#pragma warning restore CA1033 // Interface methods should be callable by child types
        #endregion

        public void RefreshModel()
        {
            _version = VersionInfo.Version;
        }

        public virtual void Initialize(string file, IFileResolve parent)
        {
            _directoryPath = Path.GetDirectoryName(file);
            _parent = parent;
        }
        #endregion

        /// <summary>
        /// strongly typed reference to <seealso cref="IFileResolve.Container" of this instance/>
        /// </summary>
        [MemoryPackIgnore]
        public abstract IFileResolve Parent { get; }
        /// <summary>
        /// None-Instance (null) of the current model
        /// </summary>
#pragma warning disable CA1000 // Do not declare static members on generic types
        public static T None => default(T);
#pragma warning restore CA1000 // Do not declare static members on generic types
        /// <summary>
        /// Unique Id of this instance within the parent entity, also need to be file-system compatible
        /// </summary>
        public string Id { get; init; }
        /// <summary>
        /// Name of this instance within the parent entity, may not be unique nor file-system compatible
        /// </summary>
        public string Name { get; init; }
        /// <summary>
        /// Application Version when this instance was last time updated
        /// </summary>
        public string Version { get => _version; init { _version = value; } }
        /// <summary>
        /// Tag property to persist arbitrary additional information
        /// </summary>
        public Dictionary<string, string> Tags { get; init; }

        protected ModelBase()
        {
        }

        protected ModelBase(string name, IFileResolve parent)
        {
            Id = name;
            Name = name;
            _parent = parent;
        }

        #region Hierarchy
        public string Hierarchy()
        {
            if (string.IsNullOrEmpty(_hierarchy))
            {
                StringBuilder builder = new StringBuilder();
                BuildHiearchy(this, builder);
                _hierarchy = builder.ToString();
            }
            return _hierarchy;
        }

        public string Hierarchy(string modelName)
        {
            return string.Concat(Hierarchy(), HierarchySeparator, modelName);
        }

        private static void BuildHiearchy(IFileResolve model, StringBuilder builder)
        {
            if (model.Container is IFileResolve fileResolve)
            {
                BuildHiearchy(fileResolve, builder);
                _ = builder.Append(HierarchySeparator);
            }
            _ = builder.Append(model.FileName);
        }

        #endregion
    }
}
