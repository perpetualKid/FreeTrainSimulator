using System.Collections.Immutable;

using FreeTrainSimulator.Common;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Full activity model extending <see cref="ActivityModelHeader"/> with detailed
    /// operational parameters such as initial speed, fuel levels, and hazard probability.
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record ActivityModel : ActivityModelHeader
    {
        /// <summary>Initial speed of the player train in meters per second at activity start.</summary>
        public float InitialSpeed { get; init; }
        /// <summary>Starting fuel levels indexed by <see cref="FuelType"/> (diesel, water, coal, etc.).</summary>
        public EnumArray<int, FuelType> FuelLevels { get; init; }
        /// <summary>Probability (0–100) that random hazard events will occur during the activity.</summary>
        public int HazardProbability { get; init; }
        /// <summary>Arbitrary key/value settings extracted from the activity file.</summary>
        public ImmutableDictionary<string, string> Settings { get; init; } = ImmutableDictionary<string, string>.Empty;
    }
}
