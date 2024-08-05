using System.IO;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Info;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent
{
    public abstract record ModelBase<T> where T : ModelBase<T>
    {
        private protected static string fileExtension;
        private string version;
        private string fileName;
        private string filePath;

        public string Name { get; init; }

        #region internal handling
        [MemoryPackIgnore]
        public string FileName => fileName;
        [MemoryPackIgnore]
        public string FilePath => filePath;
        [MemoryPackIgnore]
        public static string FileExtension => fileExtension;
        public string Version { get => version; init { version = value; } }

        public virtual ValueTask RefreshModel()
        { 
            version = VersionInfo.Version;
            return ValueTask.CompletedTask;
        }

        public virtual bool Initialize(string file)
        {
            fileName = Path.GetFileName(file);
            filePath = Path.GetDirectoryName(file);
            return VersionInfo.Compare(Version) > 0;
        }
        #endregion

        protected ModelBase()
        {
        }

        protected ModelBase(string name)
        { 
            Name = name;
        }
    }
}
