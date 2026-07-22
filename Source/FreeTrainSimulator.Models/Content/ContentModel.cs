using System.Collections.Immutable;
using System.Diagnostics;

using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Root model representing the top-level content configuration.
    /// Contains the collection of content installation folders and serves as the entry point
    /// for the model hierarchy. This model has no parent.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver("Content", ".content")]
    public sealed partial record ContentModel : ModelBase
    {
        /// <inheritdoc/>
        public override ModelBase Parent => null; // Content is root and does not implement a parent

        [MemoryPackIgnore]
        public const string MinimumVersion = "2.0.1-dev.482";

        [MemoryPackIgnore]
        public static ContentModel None { get; } = default(ContentModel);

        [MemoryPackConstructor]
        public ContentModel(ImmutableArray<FolderModel> contentFolders): base(string.Empty, null)
        {
            ContentFolders = contentFolders;
        }

        public ContentModel() : base(string.Empty, null)
        {
        }

        /// <summary>Collection of content installation folders available in this configuration.</summary>
        public ImmutableArray<FolderModel> ContentFolders { get; init; } = ImmutableArray<FolderModel>.Empty;

        public override void Initialize(ModelBase parent)
        {
            if (parent != null)
                Trace.TraceWarning($"Parent initialization for {nameof(ContentModel)} is not supported");

            foreach (FolderModel folder in ContentFolders)
            { 
                folder.Initialize(this);
            }
            base.Initialize(parent);
        }
    }
}
