using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("", ".folder")]
    public sealed partial record FolderModel : ModelBase
    {
        public override ContentModel Parent => _parent as ContentModel;
        public string ContentPath { get; init; }

        [MemoryPackConstructor]
        private FolderModel() : base()
        { }

        public FolderModel(string name, string path, ContentModel parent) : base(name, parent)
        {
            ContentPath = path;
        }
    }
}
