using System.Collections.Generic;
using System.IO;

using FreeTrainSimulator.Common.Info;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Base
{

    /// <summary>
    /// Abstract base class for all content models
    /// Implements basic file handling through <seealso cref="IFileResolve"/> interface
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract record ModelBase<T> : IFileResolve where T : ModelBase<T>
    {
        private string _version;

        #region internal handling
        private protected static string fileExtension;
        private protected static string subFolder;
        private string _directoryPath;
        private protected IFileResolve _parent;

        //does allow to override default target files
        private protected virtual string DirectoryName => Id;
        private protected virtual string FileName => Id;
        private protected virtual string DirectoryPath => _directoryPath;

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

        [MemoryPackIgnore]
        public bool RefreshRequired => VersionInfo.Compare(Version) > 0;

        public void RefreshModel()
        {
            _version = VersionInfo.Version;
        }

        public virtual void Initialize(string file, IFileResolve parent)
        {
            _directoryPath = Path.GetDirectoryName(file);
            this._parent = parent;
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
        public static T None => default (T);
#pragma warning restore CA1000 // Do not declare static members on generic types
        /// <summary>
        /// Unique Id of this instance within the parent entity, also need to be file-system compatible
        /// </summary>
        public string Id {  get; init; }
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
        public Dictionary<string, string> Tags { get; set; }

        protected ModelBase()
        {
        }

        protected ModelBase(string name, IFileResolve parent)
        {
            Id = name;
            Name = name;
            this._parent = parent;
        }
    }
}
