using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// Abstract base for all track node types in the track database.
    /// A track node represents a topological element of the track network: an endpoint, a straight/curved vector section,
    /// or a junction (switch). Each node has a 3D world position, a direction vector, and a unique index.
    /// </summary>
    /// <remarks>
    /// Derived from the MSTS <c>TrackNode</c> entries in the <c>.tdb</c> file.
    /// Concrete subtypes are <see cref="EndNode"/>, <see cref="VectorNode"/>, and <see cref="JunctionNode"/>.
    /// </remarks>
    public abstract partial record TrackNodeBase: ITileCoordinate
    {
        private readonly WorldLocation location;
        private readonly Tile worldTile;
        private readonly Vector3 direction;

        /// <summary>3D world location of this track node (position of the node's origin).</summary>
        public ref readonly WorldLocation Location => ref location;

        /// <summary>Tile coordinate derived from the world file that placed this node.</summary>
        public ref readonly Tile WorldTile => ref worldTile;

        /// <summary>Tile coordinate derived from the node's <see cref="Location"/>.</summary>
        public ref readonly Tile Tile => ref location.Tile;

        /// <summary>Unit direction vector at the node's origin, indicating the track heading.</summary>
        public ref readonly Vector3 Direction => ref direction;

        /// <summary>Unique index of this node within the <see cref="TrackDatabase.TrackNodes"/> array.
        /// Matches the 1-based index used in the legacy MSTS TDB format.</summary>
        public int NodeIndex { get; init; }

        /// <summary>Unique identifier of the world file object that placed this node.</summary>
        public int WorldId { get; init; }

        protected TrackNodeBase(in WorldLocation location, in Tile worldTile, in Vector3 direction) 
        {
            this.location = location;
            this.worldTile = worldTile;
            this.direction = direction;
        }
    }
}
