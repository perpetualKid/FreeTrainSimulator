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
        private string filePath;
        private IFileResolve parent;
        private protected bool initializeNonPersistent;

        [MemoryPackIgnore]
#pragma warning disable CA1033 // Interface methods should be callable by child types
        static string IFileResolve.DefaultExtension => fileExtension;
        [MemoryPackIgnore]
        string IFileResolve.FolderName => Name;
        [MemoryPackIgnore]
        string IFileResolve.FileName => Name;
        [MemoryPackIgnore]
        string IFileResolve.FilePath => filePath;
        [MemoryPackIgnore]
        IFileResolve IFileResolve.Container => parent;
#pragma warning restore CA1033 // Interface methods should be callable by child types
        [MemoryPackIgnore]
        public bool RefreshRequired => VersionInfo.Compare(Version) > 0 || initializeNonPersistent;
        [MemoryPackIgnore]
        public bool Initialized => !string.IsNullOrEmpty(filePath);

        public void RefreshModel()
        {
            version = VersionInfo.Version;
        }

        public virtual void Initialize(string file, IFileResolve parent)
        {
            filePath = Path.GetDirectoryName(file);
            this.parent = parent;
        }
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
