using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record FolderModel : ModelBase, IFileResolve
    {
        static string IFileResolve.SubFolder => string.Empty;
        static string IFileResolve.DefaultExtension => ".folder";

        public override ProfileModel Parent => _parent as ProfileModel;
        public string ContentPath { get; init; }

        [MemoryPackConstructor]
        private FolderModel() : base()
        { }

        public FolderModel(string name, string path, ProfileModel parent) : base(name, parent)
        {
            ContentPath = path;
        }
    }
}
