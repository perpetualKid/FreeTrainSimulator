using System.Collections.Frozen;

using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable]
    public partial record FolderModel : ModelBase<FolderModel>
    {
        public string ContentPath { get; init; }

        static partial void StaticConstructor()
        {
            fileExtension = ".contentfolder";
        }

        [MemoryPackConstructor]
        private FolderModel() : base()
        { }

        public FolderModel(string name, string path, ProfileModel parent) : base(name, parent)
        {
            ContentPath = path;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
