using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackItemIndex
    {
        public ImmutableArray<int> TrackItems { get; init; } = ImmutableArray<int>.Empty; // item indices in order
    }
}
