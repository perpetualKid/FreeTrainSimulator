using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record FolderModel : ModelBase<FolderModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".folder";
        }

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
