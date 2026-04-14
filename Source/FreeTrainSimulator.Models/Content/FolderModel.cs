using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Represents a content installation folder that groups routes and related assets.
    /// Each folder maps to a physical directory containing MSTS-compatible content.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver(".folder")]
    public sealed partial record FolderModel : ModelBase
    {
        /// <inheritdoc/>
        public override ContentModel Parent => _parent as ContentModel;
        /// <summary>File-system path to the content installation directory.</summary>
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
