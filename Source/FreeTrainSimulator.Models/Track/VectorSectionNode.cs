using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record VectorSectionNode : TrackNodeBase
    {
        private readonly Vector3 direction;

        public ref readonly Vector3 Direction => ref direction;

        public int Flag1 { get; init; }
        public int Flag2 { get; init; }
        public int ShapeIndex { get; init; }

        [MemoryPackConstructor]
        public VectorSectionNode(in WorldLocation location, in Tile worldTile, in Vector3 direction) : base(location, worldTile)
        {
            this.direction = direction;
        }
    }
}
