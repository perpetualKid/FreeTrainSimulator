using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Models.Track
{
    public abstract partial record TrackNodeBase
    {
        private readonly WorldLocation location;
        private readonly Tile worldTile;
        public ref readonly WorldLocation Location => ref location;

        public ref readonly Tile WorldTile => ref worldTile;

        public int NodeIndex { get; init; }

        public int WorldId { get; init; }

        protected TrackNodeBase(in WorldLocation location, in Tile worldTile) 
        {
            this.location = location;
            this.worldTile = worldTile;
        }
    }
}
