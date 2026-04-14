using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// A single straight or curved track segment within a <see cref="VectorNode"/>.
    /// Multiple vector sections compose the full geometry of a vector node.
    /// </summary>
    /// <remarks>
    /// Corresponds to a <c>TrVectorSection</c> in the MSTS <c>.tdb</c> file.
    /// The segment's geometry (length, radius, curvature) is determined by looking up
    /// <see cref="TrackNodeBase.NodeIndex"/> in the <see cref="TrackSectionModel.TrackSections"/> dictionary.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record VectorSectionNode : TrackNodeBase, ITileCoordinateVector
    {
        private readonly WorldLocation endLocation;

        /// <summary>
        /// 3D world location of the far end of this vector section, computed at import time.
        /// </summary>
        public ref readonly WorldLocation EndLocation => ref endLocation;

        /// <summary>Tile coordinate of the far end of this section.</summary>
        public ref readonly Tile OtherTile => ref endLocation.Tile;

        /// <summary>First MSTS flag value from the <c>TrVectorSection</c> entry. Purpose varies by context.</summary>
        public int Flag1 { get; init; }

        /// <summary>Second MSTS flag value from the <c>TrVectorSection</c> entry. Purpose varies by context.</summary>
        public int Flag2 { get; init; }

        /// <summary>Index into <see cref="TrackSectionModel.TrackShapes"/> that defines the 3D visual shape
        /// used for rendering this section.</summary>
        public int ShapeIndex { get; init; }

        [MemoryPackConstructor]
        public VectorSectionNode(in WorldLocation location, in Tile worldTile, in Vector3 direction, in WorldLocation endLocation) : 
            base(location, worldTile, direction)
        {
            this.endLocation = endLocation;
        }
    }
}
