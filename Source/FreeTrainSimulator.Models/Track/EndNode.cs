using FreeTrainSimulator.Common.Position;

using MemoryPack;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record EndNode : TrackNodeBase
    {
        [MemoryPackConstructor]
        public EndNode(in WorldLocation location, in Tile worldTile) : base(location, worldTile)
        {
        }
    }
}
