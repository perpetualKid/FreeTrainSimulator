using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{

    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record JunctionNode : TrackNodeBase
    {
        public int MainRoute { get; init; }

        public float ClearanceDistance { get; init; }

        public int ShapeIndex { get; init; }

        [MemoryPackConstructor]
        public JunctionNode(in WorldLocation location, in Tile worldTile, Vector3 direction) : base(location, worldTile, direction)
        {
        }
    }
}