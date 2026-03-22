using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record VectorSectionNode : TrackNodeBase, ITileCoordinateVector
    {
        private readonly Vector3 direction;
        private readonly WorldLocation endLocation;

        public ref readonly Vector3 Direction => ref direction;

        /// <summary>
        /// 3D world location of the far end of this vector section, computed at import time.
        /// </summary>
        public ref readonly WorldLocation EndLocation => ref endLocation;

        public ref readonly Tile OtherTile => ref endLocation.Tile;

        public int Flag1 { get; init; }
        public int Flag2 { get; init; }
        public int ShapeIndex { get; init; }

        [MemoryPackConstructor]
        public VectorSectionNode(in WorldLocation location, in Tile worldTile, in Vector3 direction, in WorldLocation endLocation) : base(location, worldTile)
        {
            this.direction = direction;
            this.endLocation = endLocation;
        }
    }
}
