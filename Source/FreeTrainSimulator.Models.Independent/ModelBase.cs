using System.IO;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent
{
    public interface IFileResolve
    {
        public static abstract string DefaultExtension { get; }
        public abstract string FilePath { get; }
        public abstract string FolderName { get; }
        public abstract string FileName { get; }
        public IFileResolve Parent { get; }
    }

    public abstract record ModelBase<T> : IFileResolve where T : ModelBase<T>
    {
        private string version;

        #region internal handling
        private protected static string fileExtension;

        [MemoryPackIgnore]
        static string IFileResolve.DefaultExtension => fileExtension;
        [MemoryPackIgnore]
        string IFileResolve.FolderName => Name;
        [MemoryPackIgnore]
        string IFileResolve.FileName => Name;
        public virtual ValueTask RefreshModel()
        {
            version = VersionInfo.Version;
            return ValueTask.CompletedTask;
        }

        public virtual bool Initialize(string file, IFileResolve parent)
        {
            FileName = Path.GetFileName(file);
            FilePath = Path.GetDirectoryName(file);
            Parent = parent;
            return VersionInfo.Compare(Version) > 0;
        }

        [MemoryPackIgnore]
        public string FileName { get; private protected set; }
        [MemoryPackIgnore]
        public string FilePath { get; private protected set; }
        [MemoryPackIgnore]
        public IFileResolve Parent { get; private protected set; }
        #endregion

        public string Name { get; init; }
        public string Version { get => version; init { version = value; } }

        protected ModelBase()
        {
        }

        protected ModelBase(string name, IFileResolve parent)
        {
            Name = name;
            Parent = parent;
        }
    }
}
