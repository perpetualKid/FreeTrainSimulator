using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackSection
    {
        public int SectionIndex { get; init; }
        public float Gauge { get; init; }
        public float Length { get; init; }
        public bool Curved { get; init; }
        public float Radius { get; init; }
        public float Angle { get; init; }
    }
}
