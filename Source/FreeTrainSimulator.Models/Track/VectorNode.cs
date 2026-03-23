using System.Collections.Immutable;

using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record VectorNode : TrackNodeBase, ITileCoordinateVector
    {
        private readonly WorldLocation endLocation;

        public ImmutableArray<VectorSectionNode> VectorSections { get; init; } = ImmutableArray<VectorSectionNode>.Empty;

        /// <summary>
        /// 3D world location of the far end of this vector node (end of its last <see cref="VectorSectionNode"/>).
        /// </summary>
        public ref readonly WorldLocation EndLocation => ref endLocation;

        public ref readonly Tile OtherTile => ref endLocation.Tile;

        [MemoryPackConstructor]
        public VectorNode(in WorldLocation location, in Tile worldTile, in WorldLocation endLocation) : base(location, worldTile, Vector3.Zero)
        {
            this.endLocation = endLocation;
        }
    }
}
