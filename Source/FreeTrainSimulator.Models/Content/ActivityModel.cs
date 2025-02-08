using System.Collections.Frozen;

using FreeTrainSimulator.Common;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record ActivityModel : ActivityModelCore
    {
        public float InitialSpeed { get; init; }
        public EnumArray<int, FuelType> FuelLevels { get; init; }
        public int HazardProbability { get; init; }
        public FrozenDictionary<string, string> Settings { get; init; } = FrozenDictionary<string, string>.Empty; //arbitrary settings which are currently in route model but may not logically belong there
    }
}
