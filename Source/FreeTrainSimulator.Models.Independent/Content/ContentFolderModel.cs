using MemoryPack;

namespace FreeTrainSimulator.Models.Independent.Content
{
    [MemoryPackable]
    public partial record ContentFolderModel : ModelBase<ContentFolderModel>
    {
        public string ContentPath { get; init; }

        static partial void StaticConstructor()
        {
            fileExtension = ".contentfolder";
        }

        [MemoryPackConstructor]
        private ContentFolderModel() : base()
        { }

        public ContentFolderModel(string name, string path) : base(name)
        {
            ContentPath = path;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
