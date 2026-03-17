using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record JunctionNode : TrackNodeBase
    {
        public int ShapeIndex { get; init; }

        [MemoryPackConstructor]
        public JunctionNode(in WorldLocation location, in Tile worldTile) : base(location, worldTile)
        {
        }
    }
}