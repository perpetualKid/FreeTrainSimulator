using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Models.Track
{
    public abstract partial record TrackNodeBase: ITileCoordinate
    {
        private readonly WorldLocation location;
        private readonly Tile worldTile;
        public ref readonly WorldLocation Location => ref location;

        public ref readonly Tile WorldTile => ref worldTile;

        ref readonly Tile ITileCoordinate.Tile => ref location.Tile;

        public int NodeIndex { get; init; }

        public int WorldId { get; init; }

        protected TrackNodeBase(in WorldLocation location, in Tile worldTile) 
        {
            this.location = location;
            this.worldTile = worldTile;
        }
    }
}
