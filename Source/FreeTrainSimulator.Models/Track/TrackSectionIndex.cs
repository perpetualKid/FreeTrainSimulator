using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackSectionIndex
    {
        public ImmutableArray<int> TrackSections { get; init; } = ImmutableArray<int>.Empty; // section indices in order
        public TrackShapeOffset ShapeOffset { get; init; }
    }
}
