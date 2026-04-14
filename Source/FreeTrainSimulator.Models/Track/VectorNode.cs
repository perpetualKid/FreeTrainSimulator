using System.Collections.Immutable;

using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// A track node representing a sequence of straight and/or curved track segments (vector sections)
    /// between two other nodes. This is the most common node type, forming the bulk of the track network.
    /// </summary>
    /// <remarks>
    /// Corresponds to a <c>TrVectorNode</c> in the MSTS <c>.tdb</c> file.
    /// The node spans from <see cref="TrackNodeBase.Location"/> (start) to <see cref="EndLocation"/> (end),
    /// with the geometry described by the ordered <see cref="VectorSections"/> array.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record VectorNode : TrackNodeBase, ITileCoordinateVector
    {
        private readonly WorldLocation endLocation;

        /// <summary>Ordered sequence of track geometry sections that compose this vector node.</summary>
        public ImmutableArray<VectorSectionNode> VectorSections { get; init; } = ImmutableArray<VectorSectionNode>.Empty;

        /// <summary>
        /// 3D world location of the far end of this vector node (end of its last <see cref="VectorSectionNode"/>).
        /// </summary>
        public ref readonly WorldLocation EndLocation => ref endLocation;

        /// <summary>Tile coordinate of the far end of this vector node.</summary>
        public ref readonly Tile OtherTile => ref endLocation.Tile;

        [MemoryPackConstructor]
        public VectorNode(in WorldLocation location, in Tile worldTile, in WorldLocation endLocation) : base(location, worldTile, Vector3.Zero)
        {
            this.endLocation = endLocation;
        }
    }
}
