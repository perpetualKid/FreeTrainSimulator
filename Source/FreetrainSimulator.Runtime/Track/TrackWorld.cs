using System;
using System.Linq;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Runtime.Track
{

    public class TrackWorld
    {
        // 1 m² — consistent with PointPrimitive.ProximityTolerance used elsewhere in the runtime
        private const double ProximityToleranceSquared = 1.0;
        // Linear equivalent of ProximityToleranceSquared, used to detect proximity to tile borders
        private const double ProximityTolerance = 1.0; // = Math.Sqrt(ProximityToleranceSquared)

        public EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType> ContentByTile { get; } = new EnumArray<ITileIndexedList<ITileCoordinate>, MapContentType>();

        public RuntimeDataResolver RuntimeData { get; }

        private TrackWorld(RuntimeDataResolver runtimeData)
        {
            RuntimeData = runtimeData;
        }

        public static TrackWorld Instance(Game game)
        {
            return game?.Services.GetService<TrackWorld>();
        }

        public static TrackWorld Reset(Game game, RuntimeDataResolver runtimeData)
        {
            game?.Services.RemoveService(typeof(TrackModel));
            TrackWorld instance = new TrackWorld(runtimeData);
            game.Services.AddService(instance);
            return instance;
        }

        /// <summary>
        /// Builds the spatial index from <paramref name="database"/>'s track items.
        /// <see cref="Models.Track.TrackItemBase"/> entries are excluded — they carry no valid world location.
        /// </summary>
        public void Initialize(Models.Track.TrackModel trackModel)
        {
            ArgumentNullException.ThrowIfNull(trackModel);
            ContentByTile[MapContentType.Empty] = new TileIndexedList<Models.Track.TrackItemBase>(trackModel.TrackDatabase.TrackItems.Where(item => item is not EmptyTrackItem));
        }

        /// <summary>
        /// Returns the <see cref="ModelTrackItemBase"/> closest to <paramref name="location"/> within the 1 m proximity threshold,
        /// or <see langword="null"/> if no candidate exists in that tile bucket within the threshold.
        /// Adjacent tile buckets are also searched when <paramref name="location"/> is within the tolerance of a tile border.
        /// </summary>
        public Models.Track.TrackItemBase TrackItemAt(in WorldLocation location, MapContentType contentType)
        {
            Models.Track.TrackItemBase nearest = null;
            double nearestDistance = ProximityToleranceSquared;

            SearchTileBucket(location.Tile, in location, contentType, ref nearest, ref nearestDistance);

            // When the query point is within ProximityTolerance of a tile border, items in the adjacent tile
            // may also be within tolerance — check those buckets too.
            // nearPosX and nearNegX are mutually exclusive (tile is 2048 m wide); same for the Z pair.
            // Corner tiles are nested inside the X branch to avoid redundant conjunction checks.
            bool nearPosZ = location.Location.Z > Tile.TileSizeOver2 - ProximityTolerance;
            bool nearNegZ = location.Location.Z < -(Tile.TileSizeOver2 - ProximityTolerance);

            if (location.Location.X > Tile.TileSizeOver2 - ProximityTolerance)
            {
                SearchTileBucket(new Tile(location.Tile.X + 1, location.Tile.Z), in location, contentType, ref nearest, ref nearestDistance);
                if (nearPosZ) SearchTileBucket(new Tile(location.Tile.X + 1, location.Tile.Z + 1), in location, contentType, ref nearest, ref nearestDistance);
                else if (nearNegZ) SearchTileBucket(new Tile(location.Tile.X + 1, location.Tile.Z - 1), in location, contentType, ref nearest, ref nearestDistance);
            }
            else if (location.Location.X < -(Tile.TileSizeOver2 - ProximityTolerance))
            {
                SearchTileBucket(new Tile(location.Tile.X - 1, location.Tile.Z), in location, contentType, ref nearest, ref nearestDistance);
                if (nearPosZ) SearchTileBucket(new Tile(location.Tile.X - 1, location.Tile.Z + 1), in location, contentType, ref nearest, ref nearestDistance);
                else if (nearNegZ) SearchTileBucket(new Tile(location.Tile.X - 1, location.Tile.Z - 1), in location, contentType, ref nearest, ref nearestDistance);
            }

            if (nearPosZ) SearchTileBucket(new Tile(location.Tile.X, location.Tile.Z + 1), in location, contentType, ref nearest, ref nearestDistance);
            else if (nearNegZ) SearchTileBucket(new Tile(location.Tile.X, location.Tile.Z - 1), in location, contentType, ref nearest, ref nearestDistance);

            return nearest;
        }

        private void SearchTileBucket(in Tile tile, in WorldLocation location, MapContentType contentType, ref Models.Track.TrackItemBase nearest, ref double nearestDistance)
        {
            foreach (Models.Track.TrackItemBase item in ContentByTile[contentType][tile])
            {
                double distance = WorldLocation.GetDistanceSquared(item.Location, location);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = item;
                }
            }
        }
    }
}
