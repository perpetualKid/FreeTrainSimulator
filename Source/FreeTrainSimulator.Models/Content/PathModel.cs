using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Full path model extending <see cref="PathModelHeader"/> with the ordered sequence of
    /// path nodes that define the train's route through the track network.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record PathModel: PathModelHeader
    {
        /// <summary>Ordered collection of nodes that define the path through the track network.</summary>
        public ImmutableArray<PathNode> PathNodes { get; init; } = ImmutableArray<PathNode>.Empty;

        [MemoryPackConstructor]
        public PathModel() : base()
        { }

        public PathModel(PathModelHeader pathModelHeader ) : base(pathModelHeader)
        { }
    }
}
