using System.Collections.Immutable;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    public enum ShapeType
    {
        None,
        Tunnel,
        Road,
    }

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record TrackShape
    {
        public int ShapeIndex { get; init; }
        public string FileName { get; init; }
        public int MainRoute { get; init; }
        public float ClearanceDistance { get; init; }
        public ImmutableArray<TrackShapePath> TrackShapePaths { get; init; } = ImmutableArray<TrackShapePath>.Empty;
        public ShapeType ShapeType { get; init; }

    }
}
