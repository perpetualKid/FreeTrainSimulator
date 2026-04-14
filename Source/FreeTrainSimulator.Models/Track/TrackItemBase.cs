using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// Abstract base for all track items placed along the track.
    /// Track items are point-objects attached to a specific track node at a given distance along it
    /// (signals, platforms, speedposts, crossovers, etc.).
    /// </summary>
    /// <remarks>
    /// Derived from the <c>TrItem</c> entries in the MSTS <c>.tdb</c> file.
    /// Each item has a world-space location and a reference back to the owning track node.
    /// </remarks>
    public abstract partial record TrackItemBase : ITileCoordinate
    {
        private readonly WorldLocation location;

        /// <summary>3D world location of this track item.</summary>
        public ref readonly WorldLocation Location => ref location;

        /// <summary>Tile coordinate derived from the item's <see cref="Location"/>.</summary>
        public ref readonly Tile WorldTile => ref location.Tile;

        /// <summary>Tile coordinate derived from the item's <see cref="Location"/>.</summary>
        public ref readonly Tile Tile => ref location.Tile;

        /// <summary>Unique index of this item within the <see cref="TrackDatabase.TrackItems"/> array.</summary>
        public int TrackItemIndex { get; init; }

        /// <summary>Index of the <see cref="TrackNodeBase"/> that this item belongs to.</summary>
        public int NodeIndex { get; init; }

        /// <summary>Distance in meters from the start of the owning track node to this item's position.</summary>
        public float SectionDistance { get; init; }

        /// <summary>MSTS-defined flags for this track item (interpretation varies by item type).</summary>
        public uint Flags { get; init; }

        protected TrackItemBase(in WorldLocation location)
        {
            this.location = location;
        }
    }
}
