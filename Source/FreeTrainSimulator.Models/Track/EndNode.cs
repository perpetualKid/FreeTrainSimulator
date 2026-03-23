using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record EndNode : TrackNodeBase
    {
        [MemoryPackConstructor]
        public EndNode(in WorldLocation location, in Tile worldTile, Vector3 direction) : base(location, worldTile, direction)
        {
        }
    }
}
