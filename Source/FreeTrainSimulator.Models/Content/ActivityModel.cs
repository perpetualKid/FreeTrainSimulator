using System.Collections.Immutable;

using FreeTrainSimulator.Common;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record ActivityModel : ActivityModelHeader
    {
        public float InitialSpeed { get; init; }
        public EnumArray<int, FuelType> FuelLevels { get; init; }
        public int HazardProbability { get; init; }
        public ImmutableDictionary<string, string> Settings { get; init; } = ImmutableDictionary<string, string>.Empty; //arbitrary settings which are currently in route model but may not logically belong there
    }
}
