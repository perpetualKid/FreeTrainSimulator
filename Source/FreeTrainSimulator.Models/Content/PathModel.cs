using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public partial record PathModel: PathModelHeader
    {
        public ImmutableArray<PathNode> PathNodes { get; init; } = ImmutableArray<PathNode>.Empty;

        [MemoryPackConstructor]
        public PathModel() : base()
        { }

        public PathModel(PathModelHeader pathModelHeader ) : base(pathModelHeader)
        { }
    }
}
