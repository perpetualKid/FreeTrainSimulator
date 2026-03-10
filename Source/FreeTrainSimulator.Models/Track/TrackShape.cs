using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackShape
    {
        public int ShapeIndex { get; init; }
        public string FileName { get; init; }
        public int MainRoute { get; init; }
        public float ClearanceDistance { get; init; }
        public ShapeType ShapeType { get; init; }
    }
}
