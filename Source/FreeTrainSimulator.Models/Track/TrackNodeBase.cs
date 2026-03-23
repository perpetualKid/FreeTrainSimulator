using FreeTrainSimulator.Common.Position;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    public abstract partial record TrackNodeBase: ITileCoordinate
    {
        private readonly WorldLocation location;
        private readonly Tile worldTile;
        private readonly Vector3 direction;

        public ref readonly WorldLocation Location => ref location;

        public ref readonly Tile WorldTile => ref worldTile;

        public ref readonly Tile Tile => ref location.Tile;

        public ref readonly Vector3 Direction => ref direction;

        public int NodeIndex { get; init; }

        public int WorldId { get; init; }

        protected TrackNodeBase(in WorldLocation location, in Tile worldTile, in Vector3 direction) 
        {
            this.location = location;
            this.worldTile = worldTile;
            this.direction = direction;
        }
    }
}
