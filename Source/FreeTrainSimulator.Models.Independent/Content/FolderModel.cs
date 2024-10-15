using System.Collections.Frozen;

using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record FolderModel : ModelBase<FolderModel>
    {
        static partial void StaticConstructor()
        {
            fileExtension = ".folder";
        }

        public override ProfileModel Parent => _parent as ProfileModel;
        public string ContentPath { get; init; }

        [MemoryPackIgnore]
        public FrozenSet<RouteModelCore> Routes { get; private set; }

        [MemoryPackConstructor]
        private FolderModel() : base()
        { }

        public FolderModel(string name, string path, ProfileModel parent) : base(name, parent)
        {
            ContentPath = path;
        }
    }
}
