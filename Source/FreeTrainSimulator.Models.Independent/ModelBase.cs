using System.IO;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent
{
    /// <summary>
    /// Interface implemented by all <seealso cref="ModelBase{T}"/> instances to support additional file system lookup scenarios
    /// </summary>
    public interface IFileResolve
    {
        /// <summary>
        /// Default File Extension
        /// </summary>
        public static abstract string DefaultExtension { get; }
        /// <summary>
        /// Full Directory Path where the file instance of this <seealso cref="ModelBase{T}"/> instance is stored
        /// </summary>
        public abstract string FilePath { get; }
        /// <summary>
        /// Name of the Folder where the <see cref="FileName"/> file instance of this <seealso cref="ModelBase{T}"/> instance is stored
        /// </summary>
        public abstract string FolderName { get; }
        /// <summary>
        /// Name of the File in the <see cref="FolderName"/> folder where the file instance of this <seealso cref="ModelBase{T}"/> instance is stored
        /// </summary>
        public abstract string FileName { get; }
        /// <summary>
        /// Reference to the parent <seealso cref="ModelBase{T}"/> instance
        /// </summary>
        public IFileResolve Parent { get; }
    }

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

        [MemoryPackIgnore]
        static string IFileResolve.DefaultExtension => fileExtension;
        [MemoryPackIgnore]
        string IFileResolve.FolderName => Name;
        [MemoryPackIgnore]
        string IFileResolve.FileName => Name;
        [MemoryPackIgnore]
        string IFileResolve.FilePath => filePath;
        [MemoryPackIgnore]
        IFileResolve IFileResolve.Parent => parent;
        [MemoryPackIgnore]
        public bool RefreshRequired => VersionInfo.Compare(Version) > 0;

        public virtual ValueTask RefreshModel()
        {
            version = VersionInfo.Version;
            return ValueTask.CompletedTask;
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
