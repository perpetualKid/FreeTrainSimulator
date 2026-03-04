using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record DynamicTrackSection
    {
        public int DynamicSectionIndex { get; init; }
        public ImmutableArray<int> TrackSections { get; init; } = ImmutableArray<int>.Empty;
    }
}
