using FreeTrainSimulator.Common;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record ActivityModel : ActivityModelCore
    {
        public int InitialSpeed { get; init; }
        public EnumArray<int, FuelType> FuelLevels { get; init; }
        public int HazardProbability { get; init; }
    }
}
