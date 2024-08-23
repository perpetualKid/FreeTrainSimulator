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
        private string version;

        #region internal handling
        private protected static string fileExtension;
        private string directoryPath;
        private IFileResolve parent;
        private protected bool initializationRequired;
        private protected bool childsInitialized;

        private protected virtual string DirectoryName => Name;
        private protected virtual string FileName => Name;
        private protected virtual string DirectoryPath => directoryPath;

        #region IFileResolve implementation
        [MemoryPackIgnore]
        static string IFileResolve.DefaultExtension => fileExtension;
        [MemoryPackIgnore]
        string IFileResolve.DirectoryName => DirectoryName;
        [MemoryPackIgnore]
        string IFileResolve.FileName => FileName;
        [MemoryPackIgnore]
        string IFileResolve.DirectoryPath => DirectoryPath;
        [MemoryPackIgnore]
        IFileResolve IFileResolve.Container => parent;
        #endregion
        [MemoryPackIgnore]
        public bool RefreshRequired => VersionInfo.Compare(Version) > 0;
        [MemoryPackIgnore]
        public bool InitializationRequired => initializationRequired;
        [MemoryPackIgnore]
        public bool ChildsInitialized => childsInitialized;

        [MemoryPackIgnore]
        public bool Initialized => !string.IsNullOrEmpty(directoryPath);

        public void RefreshModel()
        {
            version = VersionInfo.Version;
        }

        public virtual void Initialize(string file, IFileResolve parent)
        {
            directoryPath = Path.GetDirectoryName(file);
            this.parent = parent;
        }

        public virtual void Reset() => initializationRequired = true;
        #endregion

        /// <summary>
        /// Unique Name of this instance within the parent entity
        /// </summary>
        public string Name { get; init; }
        /// <summary>
        /// Application Version when this instance was last time updated
        /// </summary>
        public string Version { get => version; init { version = value; } }
        /// <summary>
        /// Tag property to persist arbitrary additional information
        /// </summary>
        public string Tag { get; set; }

        protected ModelBase()
        {
        }

        protected ModelBase(string name, IFileResolve parent)
        {
            Name = name;
            this.parent = parent;
        }
    }
}
