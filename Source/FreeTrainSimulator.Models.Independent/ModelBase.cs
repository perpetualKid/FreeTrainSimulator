using System.IO;

using FreeTrainSimulator.Common.Info;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent
{
    public abstract record ModelBase<T> where T : ModelBase<T>
    {
        private static readonly string CurrentVersion = VersionInfo.Version;
        private protected static string fileExtension;

        public string Name { get; init; }
        [MemoryPackIgnore]
        public string FileName { get; init; }
        [MemoryPackIgnore]
        public string FilePath { get; init; }
        [MemoryPackIgnore]
        public static string FileExtension => fileExtension;
        public string Hash { get; init; }
        public string Version { get; set; }

        public bool ModelRefresh => VersionInfo.Compare(Version) > 0;
        public void ResetVersion()
        {
            if (VersionInfo.Compare(Version) > 0)
                Version = CurrentVersion;
        }

        protected ModelBase()
        {
        }

        protected ModelBase(string name)
        { 
            this.Name = name;
        }
    }
}
