using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Models.Track
{
    public abstract partial record TrackItemBase : ITileCoordinate
    {
        private readonly WorldLocation location;
        public ref readonly WorldLocation Location => ref location;

        public ref readonly Tile WorldTile => ref location.Tile;

        public ref readonly Tile Tile => ref location.Tile;

        public int TrackItemIndex { get; init; }
        public int NodeIndex { get; init; }
        public float SectionDistance { get; init; }
        public uint Flags { get; init; }

        protected TrackItemBase(in WorldLocation location)
        {
            this.location = location;
        }
    }
}
